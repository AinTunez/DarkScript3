using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using SoulsFormats;
using Microsoft.ClearScript.V8;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using System.IO;
using static SoulsFormats.EMEVD.Instruction;
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

        public EMEVD EVD = new EMEVD();

        public EMELD ELD = new EMELD();

        private V8ScriptEngine v8 = new V8ScriptEngine();

        public int CurrentEventID = -1;
        public int CurrentInsIndex = -1;
        public string CurrentInsName = "";

        private List<string> LinkedFiles = new List<string>();

        public EventScripter(EMEVD evd, InstructionDocs docs)
        {
            EVD = evd;
            this.docs = docs;
            InitAll();
        }

        public EventScripter(string file, InstructionDocs docs)
        {
            EVD = EMEVD.Read(file);
            if (File.Exists(file.Replace(".emevd", ".emeld")))
            {
                try
                {
                    ELD = EMELD.Read(file.Replace(".emevd", ".emeld"));
                }
                catch
                {

                }
            }
            this.docs = docs;
            InitAll();
        }

        /// <summary>
        /// Called by JS to add instructions to the event currently being edited.
        /// </summary>
        public Instruction MakeInstruction(Event evt, int bank, int index, object[] args)
        {
            CurrentEventID = (int)evt.ID;
            CurrentInsIndex = evt.Instructions.Count + 1;

            try
            {
                EMEDF.InstrDoc doc = docs.DOC[bank][index];
                bool isVar = docs.IsVariableLength(doc);
                if (args.Length < doc.Arguments.Length)
                {
                    throw new Exception($"Instruction {bank}[{index}] ({doc.Name}) requires {doc.Arguments.Length} arguments.");
                }

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] is bool)
                        args[i] = (bool)args[i] ? 1 : 0;
                    else if (args[i] is string)
                    {
                        if (isVar)
                            throw new Exception("Event initializers cannot be dependent on parameters.");

                        IEnumerable<int> nums = (args[i] as string).Substring(1).Split('_').Select(s => int.Parse(s));
                        if (nums.Count() != 2)
                            throw new Exception("Invalid parameter string: {" + args[i] + "}");

                        int sourceStartByte = nums.ElementAt(0);
                        int length = nums.ElementAt(1);
                        uint targetStartByte = docs.FuncBytePositions[doc][i];

                        Parameter p = new Parameter(evt.Instructions.Count, targetStartByte, sourceStartByte, length);
                        evt.Parameters.Add(p);
                        evt.Parameters = evt.Parameters.OrderBy(prm => prm.SourceStartByte).ToList();

                        args[i] = doc.Arguments[i].Default;
                    }
                }

                List<object> properArgs = new List<object>();
                if (isVar)
                {
                    foreach (object arg in args)
                    {
                        properArgs.Add(Convert.ToInt32(arg));
                    }
                }
                else
                {
                    for (int i = 0; i < doc.Arguments.Length; i++)
                    {
                        EMEDF.ArgDoc argDoc = doc.Arguments[i];
                        if (argDoc.Type == 0) properArgs.Add(Convert.ToByte(args[i])); //u8
                        else if (argDoc.Type == 1) properArgs.Add(Convert.ToUInt16(args[i])); //u16
                        else if (argDoc.Type == 2) properArgs.Add(Convert.ToUInt32(args[i])); //u32
                        else if (argDoc.Type == 3) properArgs.Add(Convert.ToSByte(args[i])); //s8
                        else if (argDoc.Type == 4) properArgs.Add(Convert.ToInt16(args[i])); //s16
                        else if (argDoc.Type == 5) properArgs.Add(Convert.ToInt32(args[i])); //s32
                        else if (argDoc.Type == 6) properArgs.Add(Convert.ToSingle(args[i])); //f32
                        else if (argDoc.Type == 8) properArgs.Add(Convert.ToUInt32(args[i])); //string position
                        else throw new Exception("Invalid type in argument definition.");
                    }
                }
                Instruction ins = new Instruction(bank, index, properArgs);
                evt.Instructions.Add(ins);
                CurrentEventID = -1;
                CurrentInsIndex = -1;
                return ins;
            } catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"EXCEPTION\nCould not write instruction at Event {CurrentEventID}, index {CurrentInsIndex}.\n");
                sb.AppendLine($"INSTRUCTION\n{CurrentInsName} | {bank}[{index}]\n");
                sb.AppendLine(ex.Message);
                //sb.AppendLine("");
                //sb.AppendLine(ex.StackTrace);
                throw new Exception(sb.ToString());
            }
        }

        /// <summary>
        /// Called by JS to add instructions to the event currently being edited.
        /// </summary>
        public Instruction MakeInstruction(Event evt, int bank, int index, uint layer, object[] args)
        {
            Instruction ins = MakeInstruction(evt, bank, index, args);
            ins.Layer = layer;
            return ins;
        }

        public void Import(string filePath)
        {
            try
            {
                v8.Execute(File.ReadAllText(filePath));
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($@"Error importing {Path.GetFileName(filePath)}. Details below.\n");
                sb.AppendLine(ex.ToString());
                throw new Exception(sb.ToString());
            }
        }

        /// <summary>
        /// Sets up the JavaScript environment.
        /// </summary>
        public void InitAll()
        {
            v8.AddHostObject("$$$_host", new HostFunctions());
            v8.AddHostObject("EVD", EVD);
            v8.AddHostType("EMEVD", typeof(EMEVD));
            v8.AddHostObject("Scripter", this);
            v8.AddHostType("Object", typeof(object));
            v8.AddHostType("EVENT", typeof(Event));
            v8.AddHostType("INSTRUCTION", typeof(Instruction));
            v8.AddHostType("PARAMETER", typeof(Parameter));
            v8.AddHostType("REST", typeof(Event.RestBehaviorType));
            v8.AddHostType("Console", typeof(Console));
            v8.Execute(Resource.Text("script.js"));

            foreach (KeyValuePair<string, int> pair in docs.GlobalEnumConstants)
            {
                v8.Execute($"const {pair.Key} = {pair.Value};");
            }
            EMEDF DOC = docs.DOC;
            foreach (EMEDF.EnumDoc enm in DOC.Enums)
            {
                if (InstructionDocs.EnumNamesForGlobalization.Contains(enm.Name)) continue;
                if (enm.Name == "BOOL") continue;
                HashSet<string> vals = new HashSet<string>();
                StringBuilder code = new StringBuilder();
                code.AppendLine($"const {enm.DisplayName} = {{");
                foreach (var pair in enm.Values.ToList())
                {
                    string valName = Regex.Replace(pair.Value, @"[^\w]", "");
                    if (vals.Contains(valName)) throw new Exception($"Internal error: enum {enm.DisplayName} has duplicate value names {valName}");
                    vals.Add(valName);
                    code.AppendLine($"{valName}: {pair.Key},");
                }
                code.AppendLine("};");
                v8.Execute(code.ToString());
            }

            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    string funcName = InstructionDocs.TitleCaseName(instr.Name);

                    var args = instr.Arguments.Select(a => a.DisplayName);
                    string argNames = string.Join(", ", args);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"function {funcName} ({argNames}) {{");
                    sb.AppendLine($@"   Scripter.CurrentInsName = ""{funcName}"";");
                    foreach (var arg in args)
                    {
                        sb.AppendLine($"    if ({arg} === void 0)");
                        sb.AppendLine($@"           throw '!!! Argument \""{arg}\"" in instruction \""{funcName}\"" is undefined.';");
                    }
                    sb.AppendLine($@"  var ins = _Instruction({bank.Index}, {instr.Index}, Array.from(arguments));");
                    sb.AppendLine("    Scripter.CurrentInsName = \"\";");
                    sb.AppendLine("    return ins;");
                    sb.AppendLine("}");
                    // Console.WriteLine(sb.ToString());
                    v8.Execute(sb.ToString());

                    // TODO: Add aliases to emedfs rather than hardcoding them here
                    if (funcName.Contains("SpEffect"))
                    {
                        v8.Execute($"const {funcName.Replace("SpEffect", "Speffect")} = {funcName};");
                    }
                }
            }
        }

        public string EventName(long id)
        {
            var evt = ELD.Events.FirstOrDefault(e => e.ID == id);
            if (evt != null) return evt.Name;
            return null;
        }

        /// <summary>
        /// Executes the selected code to generate the EMEVD.
        /// </summary>
        public EMEVD Pack(string code, string documentName = null)
        {
            // TODO: Catch scripting exception here so FancyEventScripter can use it
            EVD.Events.Clear();
            v8.Execute(documentName ?? "User Script", true, $"(function() {{ {code} }})();");
            return EVD;
        }

        /// <summary>
        /// Generates JS source code from the EMEVD.
        /// </summary>
        public string Unpack()
        {
            InitLinkedFiles();
            StringBuilder code = new StringBuilder();
            foreach (Event evt in EVD.Events)
            {
                UnpackEvent(evt, code);
            }
            return code.ToString();
        }

        public void UnpackEvent(Event evt, StringBuilder code)
        {
            CurrentEventID = (int)evt.ID;

            string id = evt.ID.ToString();
            string restBehavior = evt.RestBehavior.ToString();

            Dictionary<Parameter, string> paramNames = ParamNames(evt);
            IEnumerable<string> argNameList = paramNames.Values.Distinct();
            string evtArgs = string.Join(", ", argNameList);

            string eventName = EventName(evt.ID);
            if (eventName != null) code.AppendLine($"// {eventName}");
            code.AppendLine($"Event({id}, {restBehavior}, function({evtArgs}) {{");
            for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
            {
                CurrentInsIndex = insIndex;
                Instruction ins = evt.Instructions[insIndex];
                EMEDF.InstrDoc doc = docs.DOC[ins.Bank]?[ins.ID];
                if (doc == null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($@"Unable to read instruction at Event {CurrentEventID}, Index {CurrentInsIndex}.");
                    sb.AppendLine($@"Instruction {ins.Bank}[{ins.ID}] does not exist.");
                    throw new Exception(sb.ToString());
                }
                string funcName = InstructionDocs.TitleCaseName(doc.Name);

                IEnumerable<ArgType> argStruct = doc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type);

                string[] args = default;
                string argString = "";
                try
                {
                    if (docs.IsVariableLength(doc))
                    {
                        argStruct = Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4);
                        args = ins.UnpackArgs(argStruct).Select(a => a.ToString()).ToArray();
                        argString = docs.ArgumentStringInitializer(args, insIndex, paramNames, argStruct);
                    }
                    else
                    {
                        args = ins.UnpackArgs(argStruct).Select(a => a.ToString()).ToArray();
                        argString = docs.ArgumentString(args, ins, insIndex, paramNames);
                    }
                }
                catch (Exception ex)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($@"Unable to unpack arguments for {funcName} at Event {CurrentEventID}, Index {CurrentInsIndex}." + Environment.NewLine);
                    sb.AppendLine(ex.Message);
                    // I can't use this. unfork SoulsFormats
                    /*ExcessDataException edx = (ex as ExcessDataException);
                    if (edx != null)
                    {
                        string data = BitConverter.ToString(ins.ArgData.Skip((int)edx.BytePosition).ToArray()).Replace("-", " ");
                        sb.AppendLine($"EXCESS DATA: {{ {data} }}");
                    }*/
                    throw new Exception(sb.ToString());
                }

                if (ins.Layer.HasValue)
                {
                    string str = InstructionDocs.LayerString(ins.Layer.Value);
                    if (argString.Length > 0)
                        argString = $"{argString}, {str}";
                    else
                        argString = str;
                }

                string lineOfCode = $"{InstructionDocs.TitleCaseName(doc.Name)}({argString});";
                code.AppendLine("\t" + lineOfCode);
            }
            code.AppendLine("});");
            code.AppendLine("");

            CurrentInsIndex = -1;
            CurrentEventID = -1;
        }

        /// <summary>
        /// Sets up the list of linked files.
        /// </summary>
        private void InitLinkedFiles()
        {
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
        }

        /// <summary>
        /// Returns the byte length of an ArgType.
        /// </summary>
        private int ByteLengthFromType(long t)
        {
            if (t == 0) return 1; //u8
            if (t == 1) return 2; //u16
            if (t == 2) return 4; //u32
            if (t == 3) return 1; //s8
            if (t == 4) return 2; //s16
            if (t == 5) return 4; //s32
            if (t == 6) return 4; //f32
            if (t == 8) return 4; //string position
            throw new Exception("Invalid type in argument definition.");
        }

        /// <summary>
        /// Returns a dictionary containing the textual names of an event's parameters.
        /// </summary>
        public Dictionary<Parameter, string> ParamNames(Event evt)
        {
            Dictionary<long, List<Parameter>> paramValues = new Dictionary<long, List<Parameter>>();
            for (int i = 0; i < evt.Parameters.Count; i++)
            {
                Parameter prm = evt.Parameters[i];
                if (!paramValues.ContainsKey(prm.SourceStartByte))
                    paramValues[prm.SourceStartByte] = new List<Parameter>();

                paramValues[prm.SourceStartByte].Add(prm);
            }

            Dictionary<Parameter, string> paramNames = new Dictionary<Parameter, string>();

            int ind = 0;
            foreach (var kv in paramValues)
            {
                foreach (var p in kv.Value)
                {
                    paramNames[p] = $"X{p.SourceStartByte}_{p.ByteCount}";
                }
                ind++;
            }
            return paramNames;
        }
    }
}
