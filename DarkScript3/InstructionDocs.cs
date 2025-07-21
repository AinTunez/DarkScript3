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
        public string ResourceGame => GameNameFromResourceName(ResourceString);

        public InstructionTranslator Translator { get; set; }

        // Instruction indices by display name. Used by fancy compiler (necessary?).
        public Dictionary<string, (int classIndex, int instrIndex)> Functions = new();

        // Enums by display name
        public Dictionary<string, EMEDF.EnumDoc> Enums = new();

        // Enum values by value display name. Used for autcomplete, and also in fancy compilation
        // where control/negate args etc. which must be read as ints.
        public Dictionary<string, int> EnumValues = new();

        // Callable objects by display name, both instructions and condition functions, for autocomplete/docbox purposes
        public Dictionary<string, List<EMEDF.ArgDoc>> CallArgs = new();

        // Map from typed init display name to index of Event ID arg
        public Dictionary<string, int> TypedInitIndex = new();

        // Mapping from function names to their newer versions.
        // This is populated automatically for Speffect/SpEffect before ER, and based on extra emedf metadata otherwise.
        public Dictionary<string, string> DisplayAliases = new();

        // DisplayAliases merged with the one from InstructionTranslator
        public Dictionary<string, string> AllAliases = new();

        // Byte offsets based on argument types. Used in decompilation and in building instructions.
        public Dictionary<EMEDF.InstrDoc, List<int>> FuncBytePositions = new();

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
        public Dictionary<string, int> GlobalEnumConstants = new();

        // Instructions which are messed up in vanilla files with manual fixes.
        // Only used for invalid AC6 files without corresponding maps.
        // Otherwise, alternate versions of instructions would need to be part of load/save.
        private HashSet<(int, int)> ManualFixInstructions = new();

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
                    CallArgs[pair.Key] = pair.Value.Args;
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
                    if (CallArgs.ContainsKey(alias))
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
                    if (CallArgs.ContainsKey(pair.Key) || AllAliases.ContainsKey(pair.Key))
                    {
                        throw new Exception($"{pair.Key} is both a short instruction and function/condition");
                    }
                    CallArgs[pair.Key] = pair.Value.Args;
                }
            }
        }
        
        public bool IsVariableLength(EMEDF.InstrDoc doc)
        {
            return doc.Arguments.Count > 0 && doc.Arguments[doc.Arguments.Count - 1].Vararg;
        }

        private void InitDocsFromResource(string streamPath)
        {
            if (DOC == null)
            {
                // Previously this checked streamPath then Resources\streamPath, but this makes it very easy to
                // leave copies of emedfs around. Use custom emedf for that, which is always an absolute path.
                string resolvedPath = Path.Combine("Resources", streamPath);
                if (File.Exists(resolvedPath))
                {
                    DOC = EMEDF.ReadFile(resolvedPath);
                }
#if DEBUG
                else if (File.Exists(Path.Combine("DarkScript3", resolvedPath)))
                {
                    DOC = EMEDF.ReadFile(Path.Combine("DarkScript3", resolvedPath));
                }
#endif
                else
                {
                    DOC = EMEDF.ReadStream(streamPath);
                }
            }

            ResourceString = Path.GetFileName(streamPath);

            bool displayNames = true;
#if DEBUG
            if (ResourceString.Contains("jp"))
            {
                displayNames = false;
            }
#endif
            if (ResourceString.Contains("ac6"))
            {
                ManualFixInstructions = new() { (2008, 2), (2017, 2), (2004, 1010) };
            }
            string toDisplayName(string name)
            {
                if (!displayNames) return name;
                return Regex.Replace(name, @"[^\w]", "");
            }
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
                enm.DisplayName = toDisplayName(enm.Name);
                Enums[enm.DisplayName] = enm;

                string prefix = EnumNamesForGlobalization.Contains(enm.Name) ? "" : $"{enm.DisplayName}.";
                enm.DisplayValues = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> pair in enm.Values)
                {
                    string name = prefix + toDisplayName(pair.Value);
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
                    if (Functions.TryGetValue(instr.DisplayName, out (int, int) existing) && displayNames)
                    {
                        throw new Exception($"Invalid emedf: {instr.DisplayName} refers to both {FormatInstructionID(bank.Index, instr.Index)} and {FormatInstructionID(existing.Item1, existing.Item2)}");
                    }
                    FuncBytePositions[instr] = GetArgBytePositions(instr.Arguments.Select(i => (ArgType)i.Type).ToList());
                    Functions[instr.DisplayName] = ((int)bank.Index, (int)instr.Index);
                    // Also filled in from conditions
                    CallArgs[instr.DisplayName] = instr.Arguments.ToList();

                    // Alternate init
                    // This is a bit ad-hoc currently. TODO model this better if it's not good
                    if (instr.Name.StartsWith("Initialize") && IsVariableLength(instr))
                    {
                        int idIndex = instr.Arguments.FindIndex(a => a.Name == "Event ID");
                        if (idIndex == -1)
                        {
                            throw new Exception($"Invalid emedf: {instr.DisplayName} requires parameter named \"Event ID\"");
                        }
                        string altInit = "$" + instr.DisplayName;
                        Functions[altInit] = ((int)bank.Index, (int)instr.Index);
                        CallArgs[altInit] = instr.Arguments.ToList();
                        TypedInitIndex[altInit] = idIndex;
                    }

                    foreach (var arg in instr.Arguments)
                    {
                        // Arg name does not really matter, the only important thing is that it's a valid JS identifier
                        // Uniqueness is enforced later
                        arg.DisplayName = ArgCaseName(arg.Name);
                        if (arg.EnumName != null)
                        {
                            string enumDisplayName = toDisplayName(arg.EnumName);
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
            int index = 0;
            foreach (EMEDF.DarkScriptType metaType in DOC.DarkScript.MetaTypes)
            {
                if (metaType.Name == null && (metaType.MultiNames == null || metaType.MultiNames.Count == 0))
                {
                    throw new Exception($"EMEDF validation failure: Meta type defined without applicable arg name");
                }
                metaType.Priority = index++;
                if (metaType.Name == null)
                {
                    // Dynamic names are less specific
                    metaType.Priority -= 1000;
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
            Dictionary<string, string> aliasNames = new();
            if (DOC.DarkScript.MetaAliases != null)
            {
                foreach (KeyValuePair<string, List<string>> entry in DOC.DarkScript.MetaAliases)
                {
                    foreach (string name in entry.Value)
                    {
                        if (exactTypes.ContainsKey(name))
                        {
                            throw new Exception($"EMEDF validation failure: type alias {name}->{entry.Key} has a duplicate type definition");
                        }
                        exactTypes[name] = exactTypes[entry.Key];
                        aliasNames[name] = entry.Key;
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
                            // This used to find by name, but this doesn't work with aliases (e.g. for Warp Entity Type + Warp Destination Entity ID),
                            // so rely on the index of the first found name.
                            bool matchesAny(EMEDF.ArgDoc a, string argName) => a.Name == argName || aliasNames.TryGetValue(a.Name, out string alias) && alias == argName;
                            int multiIndex = instr.Arguments.FindIndex(a => matchesAny(a, applicable.MultiNames[0]));
                            if (multiIndex >= 0)
                            {
                                EMEDF.ArgDoc multiArg = instr.Arguments[multiIndex];
                                multiArg.MetaType = applicable;
                                for (int i = 1; i < applicable.MultiNames.Count; i++)
                                {
                                    int extraIndex = multiIndex + i;
                                    if (extraIndex >= instr.Arguments.Count)
                                    {
                                        break;
                                    }
                                    multiArg = instr.Arguments[extraIndex];
                                    if (matchesAny(multiArg, applicable.MultiNames[i]))
                                    {
                                        multiArg.MetaType = applicable;
                                    }
                                    else
                                    {
                                        break;
                                    }
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
        public static int ByteLengthFromType(long t)
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

        internal static ArgType ArgTypeFromType(long type)
        {
            return type == 8 ? ArgType.UInt32 : (ArgType)type;
        }

        /// <summary>
        /// Returns a list of byte positions for an ordered list of argument types, plus a final entry for the overall length.
        /// </summary>
        internal static List<int> GetArgBytePositions(IEnumerable<ArgType> argStruct, int startPos = 0)
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

        private static string FormatUnknownType(byte[] args, int arg)
        {
            int i = arg * 4;
            int ival = BitConverter.ToInt32(args, i);
            if (ival == 0) return "0";
            short sval1 = BitConverter.ToInt16(args, i);
            short sval2 = BitConverter.ToInt16(args, i + 2);
            string numStr = sval1 == ival && sval2 == 0 ? $"{ival}" : $"{ival} | {sval1} {sval2}";
            float fval = BitConverter.ToSingle(args, i);
            if (Math.Abs(fval) >= 0.00001 && Math.Abs(fval) < 10000)
            {
                numStr += $" | {fval}f";
            }
            return $"{args[i]:X2} {args[i + 1]:X2} {args[i + 2]:X2} {args[i + 3]:X2} | {numStr}";
        }

        public static string InstrDebugStringFull(Instruction ins, string name, int insIndex = -1, Dictionary<Parameter, string> paramNames = null)
        {
            byte[] args = ins.ArgData;
            if (args.Length % 4 != 0) throw new Exception($"Irregular length {InstrDebugString(ins)}");
            string paramStr = "";
            if (paramNames.Count > 0)
            {
                paramStr = string.Join("", paramNames.Where(e => e.Key.InstructionIndex == insIndex).Select(e => $" ^({e.Key.TargetStartByte} <- {e.Value})"));
            }
            string argStr = string.Join(", ", Enumerable.Range(0, args.Length / 4).Select(i => FormatUnknownType(args, i)));
            return $"{name} {FormatInstructionID(ins.Bank, ins.ID)} ({argStr}){paramStr}";
        }

        public static ScriptAst.Instr InstrDebugObject(Instruction ins, string name, int insIndex = -1, Dictionary<Parameter, string> paramNames = null)
        {
            byte[] args = ins.ArgData;
            List<object> argStrs = Enumerable.Range(0, args.Length / 4).Select(i => (object)FormatUnknownType(args, i)).ToList();
            if (paramNames.Count > 0)
            {
                List<string> paramStrs = paramNames.Where(e => e.Key.InstructionIndex == insIndex).Select(e => $" ^({e.Key.TargetStartByte} <- {e.Value})").ToList();
                if (paramStrs.Count > 0)
                {
                    argStrs.Add(string.Join("", paramStrs));
                }
            }
            string cmdStr = FormatInstructionID(ins.Bank, ins.ID);
            ScriptAst.Instr instr = new()
            {
                Inner = ins,
                Cmd = cmdStr,
                Name = $"{name} {cmdStr}",
                Args = argStrs,
            };
            if (ins.Layer is uint layer)
            {
                instr.Layers = new ScriptAst.Layers { Mask = layer };
            }
            return instr;
        }

        public static string InstrDebugString(Instruction ins)
        {
            return $"{FormatInstructionID(ins.Bank, ins.ID)} {string.Join(" ", ins.ArgData.Select(b => $"{b:X2}"))}";
        }

        public static string InstrDocDebugString(EMEDF.InstrDoc doc)
        {
            return string.Join(", ", doc.Arguments.Select(ArgDocDebugString));
        }

        public static string ArgDocDebugString(EMEDF.ArgDoc argDoc)
        {
            string extra = argDoc.Vararg ? "*" : "";
            string type = $"{TypeString(argDoc.Type)}{extra}";
            return $"{type} {argDoc.DisplayName}";
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

        public static long FixEventID(long id)
        {
            // Special case in games before Elden Ring
            if (id == -1)
            {
                return id;
            }
            // Negatives become uint
            // It was previously incorrectly decompiled as int, causing issues in Elden Ring
            return (uint)id;
        }

        /// <summary>
        /// Returns a dictionary containing the textual names of an event's parameters, including analyzed names/types.
        /// </summary>
        public Dictionary<Parameter, string> InferredParamNames(Event evt, InitData.Links links)
        {
            if (links != null)
            {
                InitData.Lookup lookup = links.Main.TryGetEvent(evt.ID, out InitData.EventInit eventInit);
                if (lookup == InitData.Lookup.Found)
                {
                    Dictionary<Parameter, string> altParamNames = new();
                    // Populate the dictionary in argument order (parameters normally appear this way, but try to be safe)
                    foreach (InitData.InitArg arg in eventInit.Args)
                    {
                        foreach (Parameter p in evt.Parameters)
                        {
                            if (p.SourceStartByte == arg.Offset && p.ByteCount == arg.Width)
                            {
                                altParamNames[p] = arg.Name;
                            }
                        }
                    }
                    if (altParamNames.Count == evt.Parameters.Count)
                    {
                        return altParamNames;
                    }
                    // Otherwise, somehow parameter didn't correspond to any analyzed ones
                }
                // Otherwise, invalid arg usage (shouldn't happen with vanilla scripts)
            }
            return ParamNames(evt);
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
        public ScriptAst.Instr UnpackArgsWithParams<T>(
            Instruction ins,
            int insIndex,
            EMEDF.InstrDoc doc,
            Dictionary<Parameter, T> paramNames,
            Func<EMEDF.ArgDoc, object, object> formatArgFunc,
            bool allowArgMismatch = false,
            InitData.Links links = null)
        {
            List<object> args;
            int expectedParams = paramNames.Keys.Count(p => p.InstructionIndex == insIndex);
            List<int> positions = null;
            bool namedInit = false;
            if (IsVariableLength(doc))
            {
                // TODO: Handling for uint event ids, actually use the docs
                IEnumerable<ArgType> argStruct = Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4);
                args = UnpackArgsSafe(ins.ArgData, argStruct);
                // To use special init, it has to have no params and init data for the event id
                List<object> initArgs = null;
                int idIndex = doc.Arguments.FindIndex(a => a.Name == "Event ID");
                long eventId = -1;
                if (idIndex >= 0 && idIndex < args.Count)
                {
                    eventId = FixEventID((int)args[idIndex]);
                    // Keep it int only if it's -1 (appears in vanilla scripts before ER)
                    args[idIndex] = eventId == -1 ? -1 : (uint)eventId;
                }
                if (links != null && expectedParams == 0 && eventId >= 0)
                {
                    InitData.Lookup lookup = links.TryGetEvent(eventId, out InitData.EventInit eventInit);
                    if (lookup == InitData.Lookup.Found)
                    {
                        int argOffset = (idIndex + 1) * 4;
                        if (eventInit.ArgLength == 0)
                        {
                            argStruct = new[] { ArgType.Int32 };
                        }
                        else
                        {
                            argStruct = eventInit.Args.Select(a => ArgTypeFromType(a.ArgDoc.Type));
                        }
                        byte[] initArgData = new byte[ins.ArgData.Length - argOffset];
                        Array.Copy(ins.ArgData, argOffset, initArgData, 0, ins.ArgData.Length - argOffset);
                        try
                        {
                            initArgs = UnpackArgsSafe(initArgData, argStruct);
                        }
                        catch (Exception)
                        {
                            // This should also be an error on repack, since it doesn't produce typed init
                        }
                        if (initArgs != null)
                        {
                            if (eventInit.ArgLength == 0)
                            {
                                if ((int)initArgs[0] == 0)
                                {
                                    initArgs.Clear();
                                }
                                else
                                {
                                    // Otherwise, it's an error. Require empty init as 0 to avoid bytes changing
                                    initArgs = null;
                                }
                            }
                            else if (initArgData.Length > eventInit.ArgLength)
                            {
                                // This is also an error, initArgData.Length - eventInit.ArgLength excess bytes.
                                // Can it be returned to caller?
                                initArgs = null;
                            }
                            else
                            {
                                for (int argIndex = 0; argIndex < initArgs.Count; argIndex++)
                                {
                                    EMEDF.ArgDoc argDoc = eventInit.Args[argIndex].ArgDoc;
                                    initArgs[argIndex] = formatArgFunc(argDoc, initArgs[argIndex]);
                                }
                            }
                        }
                    }
                }
                if (initArgs != null)
                {
                    namedInit = true;
                    // Args before event id
                    initArgs.InsertRange(0, args.Take(idIndex + 1));
                    args = initArgs;
                }
                else
                {
                    // This previously offset positions by 6 (???)
                    positions = GetArgBytePositions(argStruct);
                    if (expectedParams > 0)
                    {
                        for (int argIndex = 0; argIndex < argStruct.Count(); argIndex++)
                        {
                            int bytePos = positions[argIndex];
                            foreach (Parameter prm in paramNames.Keys)
                            {
                                if (prm.InstructionIndex == insIndex && bytePos == prm.TargetStartByte)
                                {
                                    args[argIndex] = paramNames[prm];
                                    expectedParams--;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                positions = FuncBytePositions[doc];
                List<ArgType> argStruct = doc.Arguments.Select(a => ArgTypeFromType(a.Type)).ToList();
                try
                {
                    args = UnpackArgsSafe(ins.ArgData, argStruct);
                }
                catch when (ManualFixAC6(ins, out List<object> manualArgs) || allowArgMismatch)
                {
                    if (manualArgs != null)
                    {
                        args = manualArgs;
                    }
                    else
                    {
                        args = new List<object>();
                        // Try to preserve as many valid args as possible in compatibility mode
                        while (args.Count == 0 && argStruct.Count > 1)
                        {
                            argStruct.RemoveAt(argStruct.Count - 1);
                            try
                            {
                                args = UnpackArgsSafe(ins.ArgData, argStruct);
                            }
                            catch { }
                        }
                    }
                }
                int expectedLength = positions[argStruct.Count];
                if (ins.ArgData.Length > expectedLength && !allowArgMismatch)
                {
                    throw new ArgumentException($"{ins.ArgData.Length - expectedLength} excess bytes of arg data at position {expectedLength}");
                }
                for (int argIndex = 0; argIndex < args.Count; argIndex++)
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
#if DEBUG
                        // For testing emedf completeness, non-fancy only
                        if (argDoc.EnumDoc != null && args[argIndex] is not string)
                        {
                            // Console.WriteLine($"Invalid value for {argDoc.EnumName}: {args[argIndex]}");
                        }
#endif
                    }
                }
            }
            if (positions != null && expectedParams != 0)
            {
                throw new ArgumentException($"Invalid parameter positions: couldn't match"
                    + $" params [{string.Join(", ", paramNames.Keys.Where(p => p.InstructionIndex == insIndex).Select(p => p.TargetStartByte))}]"
                    + $" against positions [{string.Join(", ", positions)}], {expectedParams} remaining");
            }
            string funcName = doc.DisplayName;
            if (namedInit)
            {
                funcName = "$" + funcName;
            }
            ScriptAst.Instr instr = new()
            {
                Inner = ins,
                Cmd = FormatInstructionID(ins.Bank, ins.ID),
                Name = funcName,
                Args = args,
            };
            if (ins.Layer is uint layer)
            {
                instr.Layers = new ScriptAst.Layers { Mask = layer };
            }
            return instr;
        }

        public bool ManualFixAC6(Instruction ins, out List<object> args)
        {
            args = null;
            if (!ManualFixInstructions.Contains((ins.Bank, ins.ID)))
            {
                return false;
            }
            try
            {
                if (ins.Bank == 2008 && ins.ID == 2 && ins.ArgData.Length == 24)
                {
                    // SetCameraVibration missing hdVibrationId probably, like Elden Ring
                    args = UnpackArgsSafe(ins.ArgData, new[] { ArgType.Int32, ArgType.Int32, ArgType.UInt32, ArgType.Int32, ArgType.Single, ArgType.Single });
                    args.Insert(1, 0);
                    return true;
                }
                else if (ins.Bank == 2017 && ins.ID == 2 && ins.ArgData.Length == 4)
                {
                    // RegisterMiningShipAnimationForRetry probably swapped out entities previously
                    args = UnpackArgsSafe(ins.ArgData, new[] { ArgType.UInt32 });
                    args.Add(0);
                    return true;
                }
                else if (ins.Bank == 2004 && ins.ID == 1010 && ins.ArgData.Length == 12)
                {
                    // AttachCharacterToCharacter missing dummypoly id, probably rider id as that's often unspecified
                    args = UnpackArgsSafe(ins.ArgData, new[] { ArgType.UInt32, ArgType.UInt32, ArgType.Int32 });
                    args.Insert(2, -1);
                    return true;
                }
            }
            catch { }
            return false;
        }

        public bool LooksLikeCommonFunc(long id)
        {
            // Heuristics
            if (ResourceString.StartsWith("ds3") || ResourceString.StartsWith("sekiro"))
            {
                return id >= 20000000 && id < 30000000;
            }
            else if (ResourceString.StartsWith("er") || ResourceString.StartsWith("nr"))
            {
                return (id >= 90000000 && id < 100000000) || (id >= 9000000 && id < 10000000);
            }
            else if (ResourceString.StartsWith("ac6"))
            {
                return id >= 5000 && id < 6000;
            }
            else if (ResourceString.StartsWith("bb"))
            {
                // From either m29 or common
                return (id >= 12900000 && id < 12910000) || (id >= 7000 && id < 8000) || (id >= 9200 && id <= 9350 && id != 9215);
            }
            return false;
        }

        // Overly specific method for extracting multiple arg values, only knowing the function name.
        // For IDE-like functionality in the editor.
        // This is for MetaType.MultiNames specifically, so it doesn't need to support inits.
        // Returns null if all args could not be found. All args must be non-null.
        public List<int> GetArgsAsInts(List<string> args, string funcName, List<string> argFilter)
        {
            if (!AllAliases.TryGetValue(funcName, out string name))
            {
                name = funcName;
            }
            if (CallArgs.TryGetValue(name, out List<EMEDF.ArgDoc> argDocs) && argDocs.Count > 0)
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

        // Wrapper for CallArgs which processes typed inits
        public bool LookupArgDocs(InitData.DocID docId, InitData.Links links, out List<EMEDF.ArgDoc> args)
        {
            return LookupArgDocs(docId, links, out args, out _);
        }

        public bool LookupArgDocs(InitData.DocID docId, InitData.Links links, out List<EMEDF.ArgDoc> args, out InitData.EventInit init)
        {
            init = null;
            if (docId.Func == null)
            {
                args = null;
                return false;
            }
            string name = AllAliases.TryGetValue(docId.Func, out string realName) ? realName : docId.Func;
            if (CallArgs.TryGetValue(name, out args))
            {
                if (links != null
                    && TypedInitIndex.TryGetValue(name, out int idIndex)
                    && links.TryGetEvent(docId.Event, out InitData.EventInit eventInit) == InitData.Lookup.Found)
                {
                    args = args.Take(idIndex + 1).ToList();
                    args.AddRange(eventInit.Args.Select(a => a.ArgDoc));
                    init = eventInit;
                }
                return true;
            }
            else if ((name == "Event" || name == "$Event")
                && links != null
                && links.TryGetEvent(docId.Event, out InitData.EventInit eventInit) == InitData.Lookup.Found)
            {
                args = eventInit.Args.Select(a => a.ArgDoc).ToList();
                init = eventInit;
                return true;
            }
            return false;
        }

        // Called after ParseFuncAtRange, for autocomplete/tooltips
        public EMEDF.ArgDoc GetHeuristicArgDoc(InitData.DocID docId, int funcArg, InitData.Links links)
        {
            if (funcArg >= 0 && LookupArgDocs(docId, links, out List<EMEDF.ArgDoc> args) && args.Count > 0)
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
            return null;
        }

        // TODO: Make this an extension method?
        // Altered version of Instruction.UnpackArgs (where else should this be used?)
        internal static List<object> UnpackArgsSafe(byte[] argData, IEnumerable<ArgType> argStruct, bool bigEndian = false)
        {
            var result = new List<object>();
            using (var ms = new MemoryStream(argData))
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

        private static readonly HashSet<string> Acronyms = new()
        {
            // Do these need to be configured by game?
            "AI", "HP", "SE", "SP", "SFX", "FFX", "NPC", "BGM", "PS5",
            // New in AC6
            "FE", "ESD",
        };

        #region Misc

        public static string ArgCaseName(string s)
        {
            return TitleCaseName(s.Replace("Class", "Class Name").Replace("/", " "), true);
        }

        public static string TitleCaseName(string s, bool camelCase = false)
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
                    // This could also apply to AISound and ObjAct, but replacements are used for that for now
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
