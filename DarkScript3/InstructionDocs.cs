using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using static SoulsFormats.EMEVD.Instruction;
using static SoulsFormats.EMEVD;
using SoulsFormats;

namespace DarkScript3
{
    /// <summary>
    /// Instruction metadata for a given game that's not specific to any EMEVD file, including names.
    /// </summary>
    public class InstructionDocs
    {
        public string ResourceString { get; set; }
        public EMEDF DOC { get; set; }

        public bool IsASCIIStringData => ResourceString.StartsWith("ds2");
        public bool AllowRestrictConditionGroups => ResourceString.StartsWith("ds1");

        public InstructionTranslator Translator { get; set; }

        // Instruction indices by display name. Used by fancy compiler (necessary?).
        public Dictionary<string, (int classIndex, int instrIndex)> Functions = new Dictionary<string, (int classIndex, int instrIndex)>();

        // Enums by display name
        public Dictionary<string, EMEDF.EnumDoc> Enums = new Dictionary<string, EMEDF.EnumDoc>();

        // Enum values by value display name. Used for autcomplete, and also in fancy compilation
        // where control/negate args etc. which must be read as ints.
        public Dictionary<string, int> EnumValues = new Dictionary<string, int>();

        // Callable objects by display name, both instructions and condition functions, for autocomplete/docbox purposes
        public Dictionary<string, List<EMEDF.ArgDoc>> AllArgs = new Dictionary<string, List<EMEDF.ArgDoc>>();

        // Mapping from function names to their newer versions.
        // This is populated automatically for Speffect/SpEffect before ER, and based on extra emedf metadata otherwise.
        public Dictionary<string, string> DisplayAliases = new Dictionary<string, string>();

        // DisplayAliases merged with the one from InstructionTranslator
        public Dictionary<string, string> AllAliases = new Dictionary<string, string>();

        // Byte offsets based on argument types. Used in decompilation and in building instructions.
        public Dictionary<EMEDF.InstrDoc, List<int>> FuncBytePositions = new Dictionary<EMEDF.InstrDoc, List<int>>();

        // Used for syntax highlighting.
        // These are hardcoded in unpack output as well as exported manually to JS, in order to set them on the Event class from JS.
        public static readonly List<string> GlobalConstants = new List<string>() { "Default", "End", "Restart" };

        // The names of enums to globalize, used in the below two, and also in documentation/tooltip display.
        // May be overridden in EMEDF itself.
        public List<string> EnumNamesForGlobalization = new List<string>
        {
            "ON/OFF",
            "ON/OFF/CHANGE",
            "Condition Group",
            "Condition State",
            "Disabled/Enabled",
        };

        // Used for syntax highlighting and defining constants in JS.
        // Populated from individual members of globalized enums.
        public Dictionary<string, int> GlobalEnumConstants = new Dictionary<string, int>();

        // Special cases for enum display names.
        private static readonly Dictionary<string, string> EnumReplacements = new Dictionary<string, string>
        {
            { "BOOL.TRUE", "true" },
            { "BOOL.FALSE", "false" },
        };

        // overrideDoc is only for tool purposes, like round trip testing. Use 'resource' to load everything in the main program.
        public InstructionDocs(string resource = "ds1-common.emedf.json", EMEDF overrideDoc = null)
        {
            DOC = overrideDoc;
            InitDocsFromResource(resource);
            Translator = InstructionTranslator.GetTranslator(this);

            if (Translator != null)
            {
                foreach (KeyValuePair<string, InstructionTranslator.FunctionDoc> pair in Translator.CondDocs)
                {
                    if (pair.Value.ConditionDoc.Hidden) continue;
                    AllArgs[pair.Key] = pair.Value.Args;
                    string funcName = pair.Value.Name;
                    if (Enums.TryGetValue(funcName, out EMEDF.EnumDoc doc) && !EnumNamesForGlobalization.Contains(doc.Name))
                    {
                        throw new Exception($"{funcName} is both an enum and a condition function");
                    }
                    // At this point, AllAliases is only functions
                    if (Functions.ContainsKey(funcName) || DisplayAliases.ContainsKey(funcName))
                    {
                        throw new Exception($"{funcName} is both a command and a condition function");
                    }
                }
                foreach (KeyValuePair<string, string> pair in Translator.DisplayAliases)
                {
                    string alias = pair.Key;
                    if (AllArgs.ContainsKey(alias))
                    {
                        throw new Exception($"{alias} is both a condition alias and a command/condition");
                    }
                    else if (AllAliases.ContainsKey(alias))
                    {
                        throw new Exception($"{alias} is both a condition alias and a command alias");
                    }
                    AllAliases[alias] = pair.Value;
                }
                foreach (KeyValuePair<string, InstructionTranslator.ShortVariant> pair in Translator.ShortDocs)
                {
                    if (AllArgs.ContainsKey(pair.Key) || AllAliases.ContainsKey(pair.Key))
                    {
                        throw new Exception($"{pair.Key} is both a short instruction and function/condition");
                    }
                    AllArgs[pair.Key] = pair.Value.Args;
                }
            }
        }
        
        public bool IsVariableLength(EMEDF.InstrDoc doc)
        {
            if (ResourceString.StartsWith("ds2"))
                return doc == DOC[100130][1] || doc == DOC[100070][0];
            else if (ResourceString.StartsWith("ds1"))
                return doc == DOC[2000][0];
            else
                return doc == DOC[2000][0] || doc == DOC[2000][6];
        }

        private void InitDocsFromResource(string streamPath)
        {
            if (DOC == null)
            {
                if (File.Exists(streamPath))
                    DOC = EMEDF.ReadFile(streamPath);
                else if (File.Exists(@"Resources\" + streamPath))
                    DOC = EMEDF.ReadFile(@"Resources\" + streamPath);
                else
                    DOC = EMEDF.ReadStream(streamPath);
            }

            ResourceString = Path.GetFileName(streamPath);

            Dictionary<string, List<string>> aliasesByEnum = new Dictionary<string, List<string>>();
            if (DOC.DarkScript?.EnumAliases != null)
            {
                foreach (string alias in DOC.DarkScript.EnumAliases.Keys)
                {
                    string[] parts = alias.Split('.');
                    if (parts.Length != 2) throw new Exception($"Invalid emedf: badly formatted alias {alias}");
                    string enumName = parts[0];
                    if (!aliasesByEnum.TryGetValue(enumName, out List<string> aliases))
                    {
                        aliasesByEnum[enumName] = aliases = new List<string>();
                    }
                    aliases.Add(alias);
                }
            }
            if (DOC.DarkScript?.GlobalEnums != null)
            {
                // Possible override here. Note that changing these later on
                // will be fairly difficult.
                EnumNamesForGlobalization = DOC.DarkScript.GlobalEnums;
            }
            foreach (EMEDF.EnumDoc enm in DOC.Enums)
            {
                enm.DisplayName = Regex.Replace(enm.Name, @"[^\w]", "");
                Enums[enm.DisplayName] = enm;

                string prefix = EnumNamesForGlobalization.Contains(enm.Name) ? "" : $"{enm.DisplayName}.";
                enm.DisplayValues = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> pair in enm.Values)
                {
                    string name = prefix + Regex.Replace(pair.Value, @"[^\w]", "");
                    name = EnumReplacements.TryGetValue(name, out string displayName) ? displayName : name;
                    enm.DisplayValues[pair.Key] = name;
                    // As part of this, update the global dictionary
                    EnumValues[name] = int.Parse(pair.Key);
                }
                if (aliasesByEnum.TryGetValue(enm.DisplayName, out List<string> aliases))
                {
                    enm.ExtraValues = new Dictionary<string, int>();
                    foreach (string alias in aliases)
                    {
                        if (EnumValues.ContainsKey(alias))
                        {
                            throw new Exception($"Invalid emedf: {alias} cannot be an alias, as it's already preset in {enm.DisplayName}");
                        }
                        string[] parts = alias.Split('.');
                        enm.ExtraValues[parts[1]] = DOC.DarkScript.EnumAliases[alias];
                    }
                }
                if (EnumNamesForGlobalization.Contains(enm.Name))
                {
                    foreach (KeyValuePair<string, string> pair in enm.DisplayValues)
                    {
                        // This has duplicate names between ON/OFF and OFF/OFF/CHANGE, but they should map to the same respective value.
                        GlobalEnumConstants[pair.Value] = int.Parse(pair.Key);
                    }
                }
            }
            Dictionary<string, string> replaces = DOC.DarkScript?.Replacements;
            void checkDuplicates(string type, IEnumerable<long> ids)
            {
                List<long> idList = ids.ToList();
                HashSet<long> idSet = new HashSet<long>(idList);
                if (idList.Count != idSet.Count)
                {
                    foreach (long id in idSet) idList.Remove(id);
                    throw new Exception($"Invalid emedf: duplicate {type} ids {string.Join(",", idList)}");
                }
            }
            checkDuplicates("bank", DOC.Classes.Select(i => i.Index));
            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                checkDuplicates($"{bank.Index} instruction", bank.Instructions.Select(i => i.Index));
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    string name = instr.Name;
                    if (replaces != null)
                    {
                        foreach (KeyValuePair<string, string> replace in replaces)
                        {
                            name = name.Replace(replace.Key, replace.Value);
                        }
                    }
                    instr.DisplayName = TitleCaseName(name);
                    if (Functions.TryGetValue(instr.DisplayName, out (int, int) existing))
                    {
                        throw new Exception($"Invalid emedf: {instr.DisplayName} refers to both {FormatInstructionID(bank.Index, instr.Index)} and {FormatInstructionID(existing.Item1, existing.Item2)}");
                    }
                    Functions[instr.DisplayName] = ((int)bank.Index, (int)instr.Index);
                    FuncBytePositions[instr] = GetArgBytePositions(instr.Arguments.Select(i => (ArgType)i.Type).ToList());
                    // Also filled in from conditions
                    AllArgs[instr.DisplayName] = instr.Arguments.ToList();

                    foreach (var arg in instr.Arguments)
                    {
                        // Arg name does not really matter, the only important thing is that it's a valid JS identifier
                        // Uniqueness is enforced later
                        arg.DisplayName = TitleCaseName(arg.Name.Replace("Class", "Class Name").Replace("/", " "), true);
                        if (arg.EnumName != null)
                        {
                            string enumDisplayName = Regex.Replace(arg.EnumName, @"[^\w]", "");
                            if (!Enums.TryGetValue(enumDisplayName, out EMEDF.EnumDoc enumDoc))
                            {
                                throw new Exception($"Invalid emedf: bad enum reference in {FormatInstructionID(bank.Index, instr.Index)} arg {arg.Name}: {arg.EnumName} ({enumDisplayName})");
                            }
                            arg.EnumDoc = enumDoc;
                        }
                    }
                }
            }
            if (DOC.DarkScript == null)
            {
                // With no custom metadata specified, preserve old behavior of Speffect standing in for SpEffect
                foreach (string name in Functions.Keys)
                {
                    if (name.Contains("SpEffect"))
                    {
                        DisplayAliases[name.Replace("SpEffect", "Speffect")] = name;
                    }
                }
            }
            else
            {
                if (DOC.DarkScript.Aliases != null)
                {
                    foreach (KeyValuePair<string, string> pair in DOC.DarkScript.Aliases)
                    {
                        // Don't TitleCaseName here, especially in case we change title case logic
                        string alias = pair.Key;
                        if (Functions.ContainsKey(alias))
                        {
                            throw new Exception($"Alias {pair.Key} ({alias}) specified in EMEDF is already a function name");
                        }
                        (int bank, int id) = ParseInstructionID(pair.Value);
                        EMEDF.InstrDoc instrDoc = DOC[bank]?[id];
                        if (instrDoc == null)
                        {
                            throw new Exception($"Alias {alias} refers to non-existent instruction {pair.Value}");
                        }
                        DisplayAliases[alias] = instrDoc.DisplayName;
                    }
                }
                InitializeArgTypes();
            }
            AllAliases = DisplayAliases.ToDictionary(e => e.Key, e => e.Value);
        }

        private void InitializeArgTypes()
        {
            // Hardcode: PlaceName should take absolute value
            // Check if any Character Entity ID should be humans and/or self only
            // ObjAct event flag?
            if (DOC?.DarkScript?.MetaTypes == null) return;
            Dictionary<string, List<EMEDF.DarkScriptType>> exactTypes = new Dictionary<string, List<EMEDF.DarkScriptType>>();
            foreach (EMEDF.DarkScriptType metaType in DOC.DarkScript.MetaTypes)
            {
                if (metaType.Name == null && (metaType.MultiNames == null || metaType.MultiNames.Count == 0))
                {
                    throw new Exception($"EMEDF validation failure: Meta type defined without applicable arg name");
                }
                string name = metaType.Name ?? metaType.MultiNames[0];
                if (!exactTypes.TryGetValue(name, out List<EMEDF.DarkScriptType> types))
                {
                    exactTypes[name] = types = new List<EMEDF.DarkScriptType>();
                }
                // Also preprocess enums
                if (metaType.OverrideTypes != null)
                {
                    if (metaType.OverrideEnum == null
                        || !Enums.TryGetValue(metaType.OverrideEnum, out EMEDF.EnumDoc enumDoc)
                        || metaType.MultiNames == null)
                    {
                        throw new Exception($"EMEDF validation failure: {name} has type overrides but no enum {metaType.OverrideEnum}, with multi-names {metaType.MultiNames != null}");
                    }
                    foreach (EMEDF.DarkScriptTypeOverride over in metaType.OverrideTypes.Values)
                    {
                        if (!enumDoc.DisplayValues.TryGetValue(over.Value.ToString(), out string displayVal))
                        {
                            throw new Exception($"EMEDF validation failure: {over.Value} not defined in {metaType.OverrideEnum} for {name}");
                        }
                        over.DisplayValue = displayVal;
                    }
                }
                types.Add(metaType);
            }
            bool printIds = false;
            if (DOC.DarkScript.MetaAliases != null)
            {
                foreach (KeyValuePair<string, List<string>> entry in DOC.DarkScript.MetaAliases)
                {
                    foreach (string name in entry.Value)
                    {
                        exactTypes[name] = exactTypes[entry.Key];
                    }
                }
            }
            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                string bankStr = bank.Index.ToString();
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    string cmdStr = FormatInstructionID(bank.Index, instr.Index);
                    foreach (EMEDF.ArgDoc arg in instr.Arguments)
                    {
                        // If there is an exact match, use it
                        // If "ID" is present, just warn, though could also use a suffixed match
                        // If there is a bank/instruction filter, apply that as well.
                        // The last applicable match takes precedence (generic -> specific order).
                        if (arg.MetaType != null)
                        {
                            continue;
                        }
                        if (!exactTypes.TryGetValue(arg.Name, out List<EMEDF.DarkScriptType> types))
                        {
                            if (printIds && (arg.Name.EndsWith(" ID") || arg.Name.Contains(" ID ")))
                            {
                                Console.WriteLine($"No specification for ID arg name {cmdStr} {instr.Name}: {arg.Name}");
                            }
                            continue;
                        }
                        EMEDF.DarkScriptType applicable = null;
                        foreach (EMEDF.DarkScriptType cand in types)
                        {
                            if (cand.Cmds == null || cand.Cmds.Contains(bankStr) || cand.Cmds.Contains(cmdStr))
                            {
                                applicable = cand;
                            }
                        }
                        if (applicable == null)
                        {
                            if (printIds)
                            {
                                Console.WriteLine($"No matching command for arg name {cmdStr} {instr.Name}: {arg.Name}");
                            }
                            continue;
                        }
                        if (printIds)
                        {
                            Console.WriteLine($"Type {applicable.DataType}[{string.Join(",", applicable.AllTypes)}] for {cmdStr} {instr.Name}: {arg.Name}");
                        }
                        if (applicable.MultiNames == null)
                        {
                            arg.MetaType = applicable;
                        }
                        else
                        {
                            // If it's a multi-arg thing, propagate it to the other applicable args.
                            // Normally, the main arg should be the first one, and the rest should be consecutive.
                            // TriggerAISound reverses the order, so it can't be used for type-aware autocomplete currently.
                            // It should also not overlap with any other meta types, but this is not enforced.
                            foreach (string argName in applicable.MultiNames)
                            {
                                EMEDF.ArgDoc multiArg = instr.Arguments.Where(a => a.Name == argName).FirstOrDefault();
                                if (multiArg == null)
                                {
                                    break;
                                }
                                else
                                {
                                    multiArg.MetaType = applicable;
                                }
                            }
                        }
                    }
                }
            }
        }

        public static string FormatInstructionID(long bank, long index)
        {
            return $"{bank}[{index.ToString().PadLeft(2, '0')}]";
        }

        public static (int, int) ParseInstructionID(string id)
        {
            string[] parts = id.TrimEnd(']').Split(new[] { '[' }, 2);
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        public static string GameNameFromResourceName(string resourceStr)
        {
            // This is for the purpose of loading metadata based on game.
            // Assume naming like ds1-common.emedf.json and ds1-common.MapName.txt
            // This will also just pass through the name of the game by itself.
            return resourceStr.Split(new[] { '-', '.' }, 2)[0];
        }

        /// <summary>
        /// Returns the byte length of an ArgType.
        /// </summary>
        private static int ByteLengthFromType(long t)
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
        /// Returns a list of byte positions for an ordered list of argument types, plus a final entry for the overall length.
        /// </summary>
        private static List<int> GetArgBytePositions(IEnumerable<ArgType> argStruct, int startPos = 0)
        {
            List<int> positions = new List<int>();
            int bytePos = startPos;
            for (int i = 0; i < argStruct.Count(); i++)
            {
                long argType = (long)argStruct.ElementAt(i);
                int defLength = ByteLengthFromType(argType);
                if (bytePos % defLength > 0)
                    bytePos += defLength - (bytePos % defLength);

                positions.Add(bytePos);
                bytePos += defLength;
            }
            // Final int padding
            if (bytePos % 4 > 0)
                bytePos += 4 - (bytePos % 4);
            positions.Add(bytePos);

            return positions;
        }

        /// <summary>
        /// Returns JS source code to set an Instruction's layer.
        /// </summary>
        public static string LayerString(uint layerValue)
        {
            List<int> bitList = new List<int>();
            for (int b = 0; b < 32; b++)
                if ((layerValue & (1 << b)) != 0)
                    bitList.Add(b);

            return $"$LAYERS({string.Join(", ", bitList)})";
        }

        public static string InstrDebugStringFull(Instruction ins)
        {
            byte[] args = ins.ArgData;
            if (args.Length % 4 != 0) throw new Exception($"Irregular length {InstrDebugString(ins)}");
            string multiformat(int i)
            {
                string numStr = $"{BitConverter.ToInt32(args, i)}, {BitConverter.ToInt16(args, i)} {BitConverter.ToInt16(args, i + 2)}";
                float val = BitConverter.ToSingle(args, i);
                if (Math.Abs(val) >= 0.00001 && Math.Abs(val) < 10000)
                {
                    numStr += $", {val}";
                }
                return $"{args[i]:x2} {args[i + 1]:x2} {args[i + 2]:x2} {args[i + 3]:x2}, {numStr}";
            }
            return $"Nodoc {ins.Bank}[{ins.ID}]{args.Length / 4} ({string.Join(" | ", Enumerable.Range(0, args.Length / 4).Select(multiformat))})";
        }

        public static string InstrDebugString(Instruction ins)
        {
            return $"{ins.Bank}[{ins.ID}] {string.Join(" ", ins.ArgData.Select(b => $"{b:x2}"))}";
        }

        public static string InstrDocDebugString(EMEDF.InstrDoc doc)
        {
            string showType(EMEDF.ArgDoc argDoc)
            {
                string extra = argDoc.Vararg ? "*" : "";
                return $"{((ArgType)argDoc.Type).ToString().ToLowerInvariant()}{extra}";
            }
            return string.Join(", ", doc.Arguments.Select(argDoc => $"{showType(argDoc)} {argDoc.DisplayName}"));
        }

        public static string TypeString(long type)
        {
            if (type == 0) return "byte";
            if (type == 1) return "ushort";
            if (type == 2) return "uint";
            if (type == 3) return "sbyte";
            if (type == 4) return "short";
            if (type == 5) return "int";
            if (type == 6) return "float";
            if (type == 8) return "uint";
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

        /// <summary>
        /// Creates an argument list for an instruction, with parameters and formatting as well.
        /// </summary>
        public List<object> UnpackArgsWithParams<T>(
            Instruction ins,
            int insIndex,
            EMEDF.InstrDoc doc,
            Dictionary<Parameter, T> paramNames,
            Func<EMEDF.ArgDoc, object, object> formatArgFunc,
            bool allowArgMismatch = false)
        {
            List<object> args;
            if (IsVariableLength(doc))
            {
                IEnumerable<ArgType> argStruct = Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4);
                args = UnpackArgsSafe(ins, argStruct);
                // Note: this offsetting of params is likely not the case for variable-length DS2 commands.
                List<int> positions = GetArgBytePositions(argStruct, 6);
                for (int argIndex = 0; argIndex < argStruct.Count(); argIndex++)
                {
                    int bytePos = positions[argIndex];
                    foreach (Parameter prm in paramNames.Keys)
                    {
                        if (prm.InstructionIndex == insIndex && bytePos == prm.TargetStartByte)
                        {
                            args[argIndex + 2] = paramNames[prm];
                        }
                    }
                }
            }
            else
            {
                List<int> positions = FuncBytePositions[doc];
                List<ArgType> argStruct = doc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
                try
                {
                    args = UnpackArgsSafe(ins, argStruct);
                }
                catch when (allowArgMismatch)
                {
                    args = new List<object>();
                    // Try to preserve as many valid args as possible in compatibility mode
                    while (argStruct.Count > 1)
                    {
                        argStruct.RemoveAt(argStruct.Count - 1);
                        try
                        {
                            args = UnpackArgsSafe(ins, argStruct);
                        }
                        catch { }
                    }
                }
                int expectedLength = positions[argStruct.Count];
                if (ins.ArgData.Length > expectedLength && !allowArgMismatch)
                {
                    throw new ArgumentException($"{ins.ArgData.Length - expectedLength} excess bytes of arg data at position {expectedLength}");
                }
                int expectedParams = paramNames.Keys.Count(p => p.InstructionIndex == insIndex);
                for (int argIndex = 0; argIndex < args.Count(); argIndex++)
                {
                    EMEDF.ArgDoc argDoc = doc.Arguments[argIndex];
                    int bytePos = positions[argIndex];
                    bool isParam = false;
                    foreach (Parameter prm in paramNames.Keys)
                    {
                        if (prm.InstructionIndex == insIndex && bytePos == prm.TargetStartByte)
                        {
                            isParam = true;
                            args[argIndex] = paramNames[prm];
                            expectedParams--;
                        }
                    }
                    if (!isParam)
                    {
                        args[argIndex] = formatArgFunc(argDoc, args[argIndex]);
                    }
                }
                if (expectedParams != 0)
                {
                    throw new ArgumentException($"Invalid parameter positions: couldn't match"
                        + $" params [{string.Join(", ", paramNames.Keys.Where(p => p.InstructionIndex == insIndex).Select(p => p.TargetStartByte))}]"
                        + $" against positions [{string.Join(", ", positions)}]");
                }
            }
            return args;
        }

        public bool LooksLikeCommonFunc(int id)
        {
            // Heuristics
            if (ResourceString.StartsWith("ds3") || ResourceString.StartsWith("sekiro"))
            {
                return id >= 20000000 && id < 30000000;
            }
            else if (ResourceString.StartsWith("er"))
            {
                return (id >= 90000000 && id < 100000000) || (id >= 9000000 && id < 10000000);
            }
            return false;
        }

        // Overly specific method for extracting multiple arg values, only knowing the function name.
        // For IDE-like functionality in the editor.
        // Returns null if all args could not be found. All args must be non-null.
        public List<int> GetArgsAsInts(List<string> args, string funcName, List<string> argFilter)
        {
            if (!AllAliases.TryGetValue(funcName, out string name))
            {
                name = funcName;
            }
            if (AllArgs.TryGetValue(name, out List<EMEDF.ArgDoc> argDocs) && argDocs.Count > 0)
            {
                List<int> res = new List<int>();
                foreach (string argName in argFilter)
                {
                    int index = argDocs.FindIndex(a => a.Name == argName);
                    if (index == -1 || index >= args.Count)
                    {
                        return null;
                    }
                    EMEDF.ArgDoc argDoc = argDocs[index];
                    string arg = args[index].Trim();
                    if (argDoc.EnumDoc != null && EnumValues.TryGetValue(arg, out int enumValue))
                    {
                        res.Add(enumValue);
                    }
                    else if (int.TryParse(arg, out int intVal))
                    {
                        res.Add(intVal);
                    }
                    else
                    {
                        // No obvious value.
                        // At this point we may want to resolve const variables or basic arithmetic expressions, to support those.
                        return null;
                    }
                }
                return res;
            }
            return null;
        }

        public EMEDF.ArgDoc GetHeuristicArgDoc(string funcName, int funcArg)
        {
            if (funcName != null && funcArg >= 0)
            {
                if (!AllAliases.TryGetValue(funcName, out string name))
                {
                    name = funcName;
                }
                if (AllArgs.TryGetValue(name, out List<EMEDF.ArgDoc> args) && args.Count > 0)
                {
                    if (funcArg < args.Count)
                    {
                        return args[funcArg];
                    }
                    else if (args.Last().Vararg)
                    {
                        // This doesn't do anything at present - generic entity/flag etc autocomplete in future,
                        // but initializations should actually have types at some point.
                        return args.Last();
                    }
                }
            }
            return null;
        }

        // TODO: Make this an extension method?
        // Altered version of Instruction.UnpackArgs (where else should this be used?)
        private static List<object> UnpackArgsSafe(Instruction ins, IEnumerable<ArgType> argStruct, bool bigEndian = false)
        {
            var result = new List<object>();
            using (var ms = new MemoryStream(ins.ArgData))
            {
                var br = new BinaryReaderEx(bigEndian, ms);
                foreach (ArgType arg in argStruct)
                {
                    switch (arg)
                    {
                        case ArgType.Byte:
                            result.Add(br.ReadByte()); break;
                        case ArgType.UInt16:
                            AssertZeroPad(br, 2);
                            result.Add(br.ReadUInt16()); break;
                        case ArgType.UInt32:
                            AssertZeroPad(br, 4);
                            result.Add(br.ReadUInt32()); break;
                        case ArgType.SByte:
                            result.Add(br.ReadSByte()); break;
                        case ArgType.Int16:
                            AssertZeroPad(br, 2);
                            result.Add(br.ReadInt16()); break;
                        case ArgType.Int32:
                            AssertZeroPad(br, 4);
                            result.Add(br.ReadInt32()); break;
                        case ArgType.Single:
                            AssertZeroPad(br, 4);
                            result.Add(br.ReadSingle()); break;

                        default:
                            throw new NotImplementedException($"Unimplemented argument type: {arg}");
                    }
                }
                AssertZeroPad(br, 4);
            }
            return result;
        }

        private static void AssertZeroPad(BinaryReaderEx br, int align)
        {
            if (br.Stream.Position % align > 0)
            {
                br.AssertPattern(align - (int)(br.Stream.Position % align), 0);
            }
        }

        public bool GetConditionFunctionNames(string func, out string mainName, out List<string> altNames)
        {
            mainName = null;
            altNames = null;
            if (Translator == null)
            {
                return false;
            }

            ConditionData.ConditionDoc doc = null;
            if (Functions.TryGetValue(func, out (int, int) indices))
            {
                if (Translator.Selectors.TryGetValue(
                    FormatInstructionID(indices.Item1, indices.Item2),
                    out InstructionTranslator.ConditionSelector selector))
                {
                    doc = selector.Cond;
                }
            }
            else if (Translator.CondDocs.TryGetValue(func, out InstructionTranslator.FunctionDoc funcDoc))
            {
                doc = funcDoc.ConditionDoc;
            }
            if (doc == null || doc.Hidden)
            {
                return false;
            }
            mainName = doc.Name;
            altNames = new List<string>();
            foreach (ConditionData.BoolVersion b in doc.AllBools)
            {
                altNames.Add(b.Name);
            }
            foreach (ConditionData.CompareVersion c in doc.AllCompares)
            {
                altNames.Add(c.Name);
            }
            return true;
        }

        private static readonly List<string> Acronyms = new List<string>()
        {
            // Do these need to be configured by game?
            "AI","HP","SE","SP","SFX","FFX","NPC","BGM","PS5"
        };

        #region Misc

        private static string TitleCaseName(string s, bool camelCase = false)
        {
            if (string.IsNullOrEmpty(s)) return s;

            string[] words = Regex.Replace(s, @"[^\w\s]", "").Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0)
                {
                    continue;
                }
                if (Acronyms.Contains(words[i].ToUpper()))
                {
                    words[i] = words[i].ToUpper();
                }
                else if (words[i] == "SpEffect")
                {
                    // Leave as-is
                }
                else
                {
                    char firstChar = char.ToUpper(words[i][0]);
                    string rest = "";
                    if (words[i].Length > 1)
                    {
                        rest = words[i].Substring(1).ToLower();
                    }
                    words[i] = firstChar + rest;
                }
                if (camelCase && i == 0)
                {
                    // Basic heuristic: keep going until we hit non-uppercase, but only within the first word
                    int j;
                    for (j = 0; j < words[i].Length; j++)
                    {
                        char ch = words[i][j];
                        if (char.ToLower(ch) == ch) break;
                    }
                    words[i] = words[i].Substring(0, j).ToLower() + words[i].Substring(j);
                }
            }
            string output = Regex.Replace(string.Join("", words), @"[^\w]", "");
            return output;
        }

        #endregion
    }
}
