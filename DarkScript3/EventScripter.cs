using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using Microsoft.Extensions.Logging;
using SoulsFormats;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    /// <summary>
    /// Packs and unpacks EMEVD files for a given game in a simple format.
    /// 
    /// To pack or unpack in the fancier format (can be detected by presence of $Event), use FancyEventScripter.
    /// </summary>
    public class EventScripter
    {
        private InstructionDocs docs;

        public readonly string EmevdPath;
        public string JsFileName => $"{Path.GetFileName(EmevdPath)}.js";
        public string EmevdFileName => $"{Path.GetFileName(EmevdPath)}";
        public string EmevdFileDir => $"{Path.GetDirectoryName(EmevdPath)}";

        private readonly string LoadPath;

        public EMEVD EVD = new EMEVD();
        public List<JSScriptException> PackWarnings = new();

        private Dictionary<long, string> EventNames = null;

        private V8ScriptEngine v8 = new V8ScriptEngine();

        // These are accessed from JS, in code below.
        // Also used for automatic skip amount calculation
        public long CurrentEventID = -1;
        public int CurrentInsIndex = -1;
        public string CurrentInsName = "";
        private InitData.Links PackLinks;

        public EventScripter(string file, InstructionDocs docs, EMEVD evd = null, string loadPath = null)
        {
            this.docs = docs;
            EmevdPath = file;
            LoadPath = loadPath ?? file;
            EVD = evd ?? EMEVD.Read(LoadPath);
            // Use evd missing as a hint for unpacking required. Otherwise can be lazily initialized.
            if (evd == null)
            {
                GetEventNames();
            }
            InitAll();
        }

        /// <summary>
        /// Called by JS to add instructions to the event currently being edited.
        /// </summary>
        public Instruction MakeInstruction(Event evt, int bank, int index, long layer, object[] args, bool namedInit)
        {
            CurrentEventID = evt.ID;
            // TODO: Why is this done at the start? Nothing seems to use it, at least.
            CurrentInsIndex = evt.Instructions.Count + 1;

            try
            {
                EMEDF.InstrDoc doc = docs.DOC[bank][index];
                string instr() => $"Instruction {bank}[{index}] ({doc.DisplayName})";
                bool isVar = namedInit || docs.IsVariableLength(doc);
                if (!namedInit && args.Length < doc.Arguments.Count)
                {
                    throw new Exception($"{instr()} requires {doc.Arguments.Count} arguments, given {args.Length}.");
                }
                if (!isVar && args.Length > doc.Arguments.Count)
                {
                    throw new Exception($"{instr()} given {doc.Arguments.Count} arguments, only permits {args.Length}.");
                }

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is bool)
                    {
                        args[i] = (bool)args[i] ? 1 : 0;
                    }
                    else if (args[i] is string pstr)
                    {
                        // AC6 9810200 requires nested inits, but it's disallowed from named init still
                        if (namedInit)
                        {
                            throw new Exception("Linked event initializers cannot be dependent on parameters.");
                        }

                        if (!InitData.TryParseParam(pstr, out int sourceStartByte, out int length))
                        {
                            throw new Exception($"Invalid parameter string: {pstr}");
                        }

                        int targetStartByte = docs.FuncBytePositions[doc][i];

                        Parameter p = new Parameter(evt.Instructions.Count, targetStartByte, sourceStartByte, length);
                        evt.Parameters.Add(p);
                        // This will effectively be quadratic. Can it be done after all instructions are processed?
                        evt.Parameters = evt.Parameters.OrderBy(prm => prm.SourceStartByte).ToList();

                        args[i] = doc.Arguments[i].Default;
                        if (!isVar)
                        {
                            int argWidth = InstructionDocs.ByteLengthFromType(doc.Arguments[i].Type);
                            if (argWidth != length && !InitData.SideNames.Contains(doc.Arguments[i].Name))
                            {
                                string issue = length > argWidth ? "Other arguments" : "This argument";
                                AddPackWarning($"Parameter {pstr} has width {length} but is used in {doc.DisplayName} arg #{i + 1} with width {argWidth}. {issue} may be corrupted as a result.");
                            }
                        }
                    }
                }

                List<object> properArgs = new List<object>();
                if (namedInit)
                {
                    int idIndex = doc.Arguments.FindIndex(a => a.Name == "Event ID");
                    if (idIndex == -1)
                    {
                        throw new Exception($"Internal error: {instr()} used with named parameters but missing Event ID argument.");
                    }
                    int prefixLength = idIndex + 1;
                    if (args.Length < prefixLength)
                    {
                        throw new Exception($"{instr()} requires at least {prefixLength} arguments, given {args.Length}.");
                    }
                    for (int i = 0; i < idIndex; i++)
                    {
                        properArgs.Add(ConvertToType(args[i], doc.Arguments[i].Type));
                    }
                    long eventId = InstructionDocs.FixEventID(Convert.ToInt64(args[idIndex]));
                    properArgs.Add((uint)eventId);
                    if (PackLinks == null)
                    {
                        throw new Exception($"Typed init is used but initialization data is unavailable. Try reopening the file to add the required dependencies");
                    }
                    InitData.Lookup lookup = PackLinks.TryGetEvent(eventId, out InitData.EventInit eventInit);
                    if (lookup != InitData.Lookup.Found)
                    {
                        string error = lookup switch
                        {
                            InitData.Lookup.Duplicate => "has multiple definitions",
                            InitData.Lookup.Unconverted => "is declared using X0_4-style parameters",
                            InitData.Lookup.Error => "definition has ambiguous arguments:\n" + string.Join("\n", eventInit.Errors),
                            _ => "has no definition",
                        };
                        throw new Exception($"{instr()} cannot be resolved: event {eventId} {error}");
                    }
                    int initArgCount = args.Length - prefixLength;
                    if (initArgCount != eventInit.Args.Count)
                    {
                        throw new Exception($"{instr()} for event {eventId} requires {prefixLength} initial arguments plus {eventInit.Args.Count} event parameters, given {args.Length}.");
                    }
                    if (eventInit.Args.Count == 0)
                    {
                        properArgs.Add(0);
                    }
                    else
                    {
                        for (int i = 0; i < eventInit.Args.Count; i++)
                        {
                            properArgs.Add(ConvertToType(args[prefixLength + i], eventInit.Args[i].ArgDoc.Type));
                        }
                    }
                }
                else if (isVar)
                {
                    int idIndex = doc.Arguments.FindIndex(a => a.Name == "Event ID");
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (i == idIndex)
                        {
                            // This is required to encode uint values, as there are some uint range ids,
                            // -1 in some games, and negative int versions in previous versions.
                            long eventId = InstructionDocs.FixEventID(Convert.ToInt64(args[idIndex]));
                            properArgs.Add((int)eventId);
                        }
                        else
                        {
                            properArgs.Add(Convert.ToInt32(args[i]));
                        }
                    }
                    // Duplicate some of the logic above for doublechecking init routines aren't confused
                    // This may be sticky/annoying in cases where resolution is incorrect or types mismatch
                    if (PackLinks != null && idIndex != -1)
                    {
                        long eventId = InstructionDocs.FixEventID(Convert.ToInt64(args[idIndex]));
                        // TODO: Identity file it was found in?
                        if (PackLinks.TryGetEvent(eventId, out InitData.EventInit eventInit) == InitData.Lookup.Found
                            && eventInit.Args.Count > 0)
                        {
                            AddPackWarning($"{doc.DisplayName} used instead of ${doc.DisplayName} when event {eventId} is declared with named parameters");
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < doc.Arguments.Count; i++)
                    {
                        properArgs.Add(ConvertToType(args[i], doc.Arguments[i].Type));
                    }
                }
                Instruction ins = new Instruction(bank, index, properArgs);
                if (layer >= 0)
                {
                    ins.Layer = (uint)layer;
                }
                evt.Instructions.Add(ins);
                CurrentEventID = -1;
                CurrentInsIndex = -1;
                return ins;
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"EXCEPTION\nCould not write instruction at Event {CurrentEventID} at index {CurrentInsIndex}.\n");
                sb.AppendLine($"INSTRUCTION\n{CurrentInsName} | {bank}[{index}]\n");
                sb.AppendLine(ex.Message);
                throw new Exception(sb.ToString());
            }
        }

        private void AddPackWarning(string msg)
        {
            // Can be done only while packing
            PackWarnings.Add(JSScriptException.FromV8Stack(msg + "\n" + v8.GetStackTrace()));
        }

        public void LookupArgs(long eventId, object[] args)
        {
            if (args.Length == 0)
            {
                return;
            }
            if (args.All(a => ((string)a).StartsWith('X')))
            {
                return;
            }
            if (PackLinks == null)
            {
                throw new Exception($"Internal error: Missing linked initialization data for event {eventId}");
            }
            InitData.Lookup lookup = PackLinks.Main.TryGetEvent(eventId, out InitData.EventInit eventInit);
            // Many of these may be internal errors
            if (lookup == InitData.Lookup.Found)
            {
                if (args.Length != eventInit.Args.Count)
                {
                    // This should be an internal error
                    throw new Exception($"Parsed {eventInit.Args.Count} args in declared event {eventId}, found {args.Length} during evaluation: [{string.Join(", ", args)}]");
                }
            }
            else
            {
                string error = lookup switch
                {
                    InitData.Lookup.NotFound => "Event not declared using id in source code",
                    InitData.Lookup.Duplicate => "Duplicate definitions found",
                    InitData.Lookup.Unconverted => "Event is declared using X0_4-style parameters",
                    InitData.Lookup.Error => "\n" + string.Join("\n", eventInit.Errors),
                    _ => "",
                };
                throw new Exception($"Cannot use named parameters in event {eventId}: " + error);
            }
            for (int i = 0; i < args.Length; i++)
            {
                string arg = (string)args[i];
                // TODO: See what's allowed with parsing these from source. Are X args allowed here?
                InitData.InitArg initArg = eventInit.Args[i];
                args[i] = $"X{initArg.Offset}_{initArg.Width}";
            }
        }

        private static object ConvertToType(object val, long type)
        {
            switch (type) {
                case 0: return Convert.ToByte(val); //u8
                case 1: return Convert.ToUInt16(val); //u16
                case 2: return Convert.ToUInt32(val); //u32
                case 3: return Convert.ToSByte(val); //s8
                case 4: return Convert.ToInt16(val); //s16
                case 5: return Convert.ToInt32(val); //s32
                case 6: return Convert.ToSingle(val); //f32
                case 8: return Convert.ToUInt32(val); //string position
                default: throw new Exception($"Invalid type {val?.GetType()} in argument definition");
            }
        }

        /// <summary>
        /// Called by JS to edit an earlier instruction to skip to the current instruction's index.
        /// </summary>
        public void FillSkipPlaceholder(Event evt, int fillIndex)
        {
            int skipTarget = evt.Instructions.Count;
            if (evt == null || fillIndex < 0 || fillIndex >= skipTarget)
            {
                throw new Exception($"Invalid or unspecified skip placeholder index in Event {CurrentEventID} ({evt?.ID}) at index {CurrentInsIndex}");
            }
            // This is a bit fragile, we can't do much checking without maintaining more state.
            Instruction ins = evt.Instructions[fillIndex];
            // 99 as fill-in value in script.js. It is checked afterwards that all of these are filled.
            if (ins.ArgData.Length == 0 || ins.ArgData[0] != 99)
            {
                throw new Exception($"Unexpected instruction {InstructionDocs.InstrDebugString(ins)} in skip placeholder in Event {CurrentEventID}, from indices {fillIndex}->{skipTarget}");
            }
            // 0-line skip is from e.g. fillIndex = 5, to skipTarget = 6
            // 4-line skip (the entire event *after* the first instruction) is from fillIndex = 0 to skipTarget = 5
            int skipCount = skipTarget - fillIndex - 1;
            if (skipCount < 0 || skipCount > byte.MaxValue)
            {
                throw new Exception($"Skip too long in Event {CurrentEventID} from indices {fillIndex}->{skipTarget}, must be <256 lines. Use labels or split up the event.");
            }
            ins.ArgData[0] = (byte)skipCount;
        }

        public int ConvertFloatToIntBytes(double input)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes((float)input), 0);
        }

        /// <summary>
        /// Sets up the JavaScript environment.
        /// </summary>
        private void InitAll()
        {
            v8.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
            v8.DocumentSettings.SearchPath = Path.GetDirectoryName(EmevdPath);

            v8.AddHostObject("$$$_host", new HostFunctions());
            v8.AddHostObject("EVD", EVD);
            v8.AddHostType("EMEVD", typeof(EMEVD));
            v8.AddHostObject("Scripter", this);
            v8.AddHostType("EVENT", typeof(Event));
            v8.AddHostType("INSTRUCTION", typeof(Instruction));
            v8.AddHostType("PARAMETER", typeof(Parameter));
            v8.AddHostType("REST", typeof(Event.RestBehaviorType));
            v8.AddHostType("Console", typeof(Console));
            v8.Execute(Resource.Text("script.js"));

            StringBuilder code = new StringBuilder();
            foreach (KeyValuePair<string, int> pair in docs.GlobalEnumConstants)
            {
                code.AppendLine($"const {pair.Key} = {pair.Value};");
            }
            EMEDF DOC = docs.DOC;
            foreach (EMEDF.EnumDoc enm in DOC.Enums)
            {
                if (docs.EnumNamesForGlobalization.Contains(enm.Name)) continue;
                if (enm.Name == "BOOL") continue;
                HashSet<string> vals = new HashSet<string>();
                code.AppendLine($"const {enm.DisplayName} = {{");
                foreach (KeyValuePair<string, string> pair in enm.Values)
                {
                    string valName = Regex.Replace(pair.Value, @"[^\w]", "");
                    if (vals.Contains(valName)) throw new Exception($"Internal error: enum {enm.DisplayName} has duplicate value names {valName}");
                    vals.Add(valName);
                    code.AppendLine($"{valName}: {pair.Key},");
                }
                if (enm.ExtraValues != null)
                {
                    foreach (KeyValuePair<string, int> pair in enm.ExtraValues)
                    {
                        code.AppendLine($"{pair.Key}: {pair.Value},");
                    }
                }
                code.AppendLine("};");
            }

            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    string funcName = instr.DisplayName;

                    // TODO: Consider requiring all arg docs to be uniquely named in InstructionDocs.
                    List<string> args = new List<string>();
                    foreach (EMEDF.ArgDoc argDoc in instr.Arguments)
                    {
                        string name = argDoc.DisplayName;
                        while (args.Contains(name))
                        {
                            name += "_";
                        }
                        args.Add(name);
                    }
                    string argNames = string.Join(", ", args);

                    code.AppendLine($"function {funcName} ({argNames}) {{");
                    code.AppendLine($@"   Scripter.CurrentInsName = ""{funcName}"";");
                    foreach (string arg in args)
                    {
                        code.AppendLine($"    if ({arg} === void 0)");
                        code.AppendLine($@"           throw '!!! Argument \""{arg}\"" in instruction \""{funcName}\"" is undefined or missing.';");
                    }
                    code.AppendLine($@"  var ins = _Instruction({bank.Index}, {instr.Index}, Array.from(arguments));");
                    code.AppendLine("    Scripter.CurrentInsName = \"\";");
                    code.AppendLine("    return ins;");
                    code.AppendLine("}");
                    if (funcName.StartsWith("Initialize") && docs.IsVariableLength(instr))
                    {
                        // May be missing varargs parameter
                        code.AppendLine($"function ${funcName} ({argNames}) {{");
                        code.AppendLine($@"   Scripter.CurrentInsName = ""${funcName}"";");
                        code.AppendLine($@"  var ins = _Instruction({bank.Index}, {instr.Index}, Array.from(arguments), true);");
                        code.AppendLine("    Scripter.CurrentInsName = \"\";");
                        code.AppendLine("    return ins;");
                        code.AppendLine("}");
                    }
                }
            }
            foreach (KeyValuePair<string, string> alias in docs.DisplayAliases)
            {
                code.AppendLine($"const {alias.Key} = {alias.Value};");
            }
            try
            {
                v8.Execute(code.ToString());
            }
            catch (Exception ex) when (ex is IScriptEngineException scriptException)
            {
                throw new Exception($"Error processing {docs.ResourceString}: {scriptException.ErrorDetails}");
            }
        }

        public string EventName(long id)
        {
            return EventNames.TryGetValue(id, out string name) ? name : null;
        }

        public Dictionary<long, string> GetEventNames()
        {
            EventNames ??= EMELDTXT.ResolveNames(docs.ResourceGame, LoadPath);
            return EventNames;
        }

        public InitData.Links LoadLinks(InitData initData)
        {
            List<string> names = new();
            foreach (string linkPath in GetLinkedFiles())
            {
                string name = InitData.GetEmevdName(linkPath);
                // The main place it actually is invalid is common_macro
                if (!InitData.ValidLinkedNames.Contains(name)) continue;
                names.Add(name);
                UpdateLinkedFile(initData, name);
            }
            return new InitData.Links(InitData.GetEmevdName(EmevdPath), initData, names);
        }

        public void UpdateLinksForUnpack(InitData.Links links)
        {
            if (links == null)
            {
                return;
            }
            links.Main = InitData.FromEmevd(docs, EVD, EmevdPath, EventNames);
            if (links.Main.IsAuthoritative())
            {
                links.UpdateInitData();
            }
        }

        public void UpdateLinksBeforePack(InitData.Links links, string code, InitData.FileInit mainInit = null)
        {
            if (mainInit == null)
            {
                FancyJSCompiler.CompileOutput output = new FancyJSCompiler(JsFileName, code, EventCFG.CFGOptions.GetDefault())
                    .Compile(docs, FancyJSCompiler.Mode.ParseOnly);
                mainInit = output.MainInit;
            }
            links.Main = mainInit;
        }

        public void UpdateLinksAfterPack(InitData.Links links)
        {
            if (links?.Main == null)
            {
                return;
            }
            if (links.Main.IsAuthoritative() && links.Main.SetSourceInfo(EmevdPath + ".js"))
            {
                links.UpdateInitData();
            }
        }

        internal void ForceUpdateLinks(InitData.Links links)
        {
            if (links?.Main == null)
            {
                return;
            }
            links.Main.ForceAuthoritative = true;
            links.UpdateInitData();
        }

        public List<string> GetMissingLinkFiles()
        {
            List<string> names = new();
            foreach (string linkPath in GetLinkedFiles())
            {
                string name = InitData.GetEmevdName(linkPath);
                // The main place it actually is invalid is common_macro
                if (!InitData.ValidLinkedNames.Contains(name)) continue;
                if (!InitData.TryGetLinkedFilePath(EmevdFileDir, name, out _))
                {
                    names.Add(name);
                }
            }
            return names;
        }

        private void UpdateLinkedFile(InitData initData, string name)
        {
            if (!InitData.TryGetLinkedFilePath(initData.BaseDir, name, out string linkPath))
            {
                throw new Exception($"Missing linked file {name} in {initData.BaseDir}");
            }
            if (initData.Files.TryGetValue(name, out InitData.FileInit linkedInit) && !linkedInit.IsStale())
            {
                return;
            }
            if (linkPath.EndsWith(".js"))
            {
                string code = File.ReadAllText(linkPath);
                code = HeaderData.Trim(code);
                // Options shouldn't matter here, as non-defaults make compilation more strict. This may throw if error.
                FancyJSCompiler.CompileOutput output = new FancyJSCompiler(linkPath, code, EventCFG.CFGOptions.GetDefault())
                    .Compile(docs, FancyJSCompiler.Mode.ParseOnly);
                linkedInit = output.MainInit;
            }
            else
            {
                EMEVD linkedEmevd = EMEVD.Read(linkPath);
                Dictionary<long, string> linkedNames = EMELDTXT.ResolveNames(docs.ResourceGame, linkPath);
                linkedInit = InitData.FromEmevd(docs, linkedEmevd, linkPath, linkedNames);
            }
            linkedInit.Linked = true;
            initData.Files[name] = linkedInit;
        }

        /// <summary>
        /// Executes the selected code to generate the EMEVD.
        /// </summary>
        public EMEVD Pack(string code, InitData.Links links, InitData.FileInit mainInit = null)
        {
            if (links != null)
            {
                UpdateLinksBeforePack(links, code, mainInit);
            }
            PackLinks = links;
            PackWarnings.Clear();
            EVD.Events.Clear();
            v8.DocumentSettings.Loader.DiscardCachedDocuments();
            try
            {
                DocumentInfo docInfo = new DocumentInfo(JsFileName) { Category = ModuleCategory.Standard };
                v8.Execute(docInfo, code);
            }
            catch (Exception ex) when (ex is IScriptEngineException scriptException)
            {
                throw JSScriptException.FromV8(scriptException);
            }
            return EVD;
        }

        /// <summary>
        /// Generates JS source code from the EMEVD.
        /// </summary>
        public string Unpack(InitData.Links links, bool compatibilityMode = false)
        {
            GetEventNames();
            UpdateLinksForUnpack(links);
            StringBuilder code = new StringBuilder();
            foreach (Event evt in EVD.Events)
            {
                UnpackEvent(evt, code, links, compatibilityMode);
            }
            return code.ToString();
        }

        public void UnpackEvent(Event evt, StringBuilder code, InitData.Links links, bool compatibilityMode = false, bool addEventName = true)
        {
            CurrentEventID = evt.ID;

            string id = CurrentEventID.ToString();
            string restBehavior = evt.RestBehavior.ToString();

            Dictionary<Parameter, string> paramNames = docs.InferredParamNames(evt, links);
            IEnumerable<string> argNameList = paramNames.Values.Distinct();
            string evtArgs = string.Join(", ", argNameList);

            if (addEventName)
            {
                string eventName = EventName(CurrentEventID);
                if (eventName != null) code.AppendLine($"// {eventName}");
            }
            code.AppendLine($"Event({id}, {restBehavior}, function({evtArgs}) {{");
            for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
            {
                CurrentInsIndex = insIndex;
                Instruction ins = evt.Instructions[insIndex];
                EMEDF.InstrDoc doc = docs.DOC[ins.Bank]?[ins.ID];
                if (doc == null)
                {
#if DEBUG
                    // Partial mode
                    // This is fine for reversing emedfs but deleting unknown commands is really bad for real usage
                    {
                        code.AppendLine(ScriptAst.SingleIndent + InstructionDocs.InstrDebugStringFull(ins, "Nodoc", insIndex, paramNames));
                        continue;
                    }
#endif
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($@"Unable to read instruction at Event {CurrentEventID} at index {CurrentInsIndex}.");
                    sb.AppendLine($@"Unknown instruction id: {InstructionDocs.InstrDebugString(ins)}");
                    throw new Exception(sb.ToString());
                }
                string funcName = doc.DisplayName;

                ScriptAst.Instr instr;
                try
                {
                    instr = docs.UnpackArgsWithParams(
                        ins, insIndex, doc, paramNames,
                        (argDoc, val) => argDoc.GetDisplayValue(val),
                        compatibilityMode,
                        links);
                }
                catch (Exception ex)
                {
#if DEBUG
                    // Partial mode
                    {
                        code.AppendLine(ScriptAst.SingleIndent + InstructionDocs.InstrDebugStringFull(ins, "Baddoc", insIndex, paramNames));
                        continue;
                    }
#endif
                    var sb = new StringBuilder();
                    sb.AppendLine($@"Unable to unpack arguments for {funcName}({InstructionDocs.InstrDocDebugString(doc)}) at Event {CurrentEventID} at index {CurrentInsIndex}.");
                    sb.AppendLine($@"Instruction arg data: {InstructionDocs.InstrDebugString(ins)}");
                    sb.AppendLine(ex.Message);
                    throw new Exception(sb.ToString());
                }
                code.AppendLine(ScriptAst.SingleIndent + instr);
            }
            code.AppendLine("});");
            code.AppendLine("");

            CurrentInsIndex = -1;
            CurrentEventID = -1;
        }

        /// <summary>
        /// Sets up the list of linked files.
        /// </summary>
        private List<string> GetLinkedFiles()
        {
            List<string> LinkedFiles = new();
            var reader = new BinaryReaderEx(false, EVD.StringData);
            if (docs.IsASCIIStringData)
            {
                foreach (long offset in EVD.LinkedFileOffsets)
                {
                    string linkedFile = reader.GetASCII(offset);
                    LinkedFiles.Add(linkedFile);
                }
            }
            else
            {
                foreach (long offset in EVD.LinkedFileOffsets)
                {
                    string linkedFile = reader.GetUTF16(offset);
                    LinkedFiles.Add(linkedFile);
                }
            }
            return LinkedFiles;
        }
    }
}
