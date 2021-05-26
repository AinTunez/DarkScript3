using System.Collections.Generic;
using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using static SoulsFormats.EMEVD.Instruction;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    /// <summary>
    /// Instruction metadata for a given game that's not specific to any EMEVD file, including names.
    /// </summary>
    public class InstructionDocs
    {
        public string ResourceString { get; set; }

        public EMEDF DOC { get; set; } = new EMEDF();

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

        // Callable objects by display name, both instructions and condition functions
        public Dictionary<string, List<EMEDF.ArgDoc>> AllArgs = new Dictionary<string, List<EMEDF.ArgDoc>>();

        // Byte offsets based on argument types. Used in decompilation and in building instructions.
        public Dictionary<EMEDF.InstrDoc, List<int>> FuncBytePositions = new Dictionary<EMEDF.InstrDoc, List<int>>();

        // Used for syntax highlighting.
        // These are hardcoded in unpack output as well as exported manually to JS, in order to set them on the Event class from JS.
        public static readonly List<string> GlobalConstants = new List<string>() { "Default", "End", "Restart" };

        // The names of enums to globalize, used in the below two, and also in documentation/tooltip display.
        public static readonly List<string> EnumNamesForGlobalization = new List<string>
        {
            "ON/OFF",
            "ON/OFF/CHANGE",
            "Condition Group",
            "Condition State",
            "Disabled/Enabled",
        };

        // Used for syntax highlighting and defining constants in JS. Globalized enum values are added to this.
        public Dictionary<string, int> GlobalEnumConstants = new Dictionary<string, int>();

        // Special cases for enum display names.
        private static readonly Dictionary<string, string> EnumReplacements = new Dictionary<string, string>
        {
            { "BOOL.TRUE", "true" },
            { "BOOL.FALSE", "false" },
        };

        public InstructionDocs(string resource = "ds1-common.emedf.json")
        {
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
                    if (Functions.ContainsKey(funcName))
                    {
                        throw new Exception($"{funcName} is both a command and a condition function");
                    }
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
            if (File.Exists(streamPath))
                DOC = EMEDF.ReadFile(streamPath);
            else if (File.Exists(@"Resources\" + streamPath))
                DOC = EMEDF.ReadFile(@"Resources\" + streamPath);
            else
                DOC = EMEDF.ReadStream(streamPath);

            ResourceString = Path.GetFileName(streamPath);

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
                if (EnumNamesForGlobalization.Contains(enm.Name))
                {
                    foreach (KeyValuePair<string, string> pair in enm.DisplayValues)
                    {
                        // This has duplicate names between ON/OFF and OFF/OFF/CHANGE, but they should map to the same respective value.
                        GlobalEnumConstants[pair.Value] = int.Parse(pair.Key);
                    }
                }
            }
            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    instr.DisplayName = TitleCaseName(instr.Name);
                    Functions[instr.DisplayName] = ((int)bank.Index, (int)instr.Index);
                    FuncBytePositions[instr] = GetArgBytePositions(instr.Arguments.Select(i => (ArgType)i.Type).ToList());
                    // Also filled in from conditions
                    AllArgs[instr.DisplayName] = instr.Arguments.ToList();

                    foreach (var arg in instr.Arguments)
                    {
                        arg.DisplayName = CamelCaseName(arg.Name.Replace("Class", "Class Name"));
                        if (arg.EnumName != null)
                        {
                            string enumDisplayName = Regex.Replace(arg.EnumName, @"[^\w]", "");
                            arg.EnumDoc = Enums[enumDisplayName];
                        }
                    }
                }
            }
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

        public static string InstrDebugString(Instruction ins)
        {
            return $"{ins.Bank}[{ins.ID}] {string.Join(" ", ins.ArgData.Select(b => $"{b:x2}"))}";
        }

        public static string InstrDocDebugString(EMEDF.InstrDoc doc)
        {
            string showType(EMEDF.ArgDoc argDoc)
            {
                string extra = argDoc.Vararg ? "*" : (argDoc.Optional ? "?" : "");
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
                args = ins.UnpackArgs(argStruct);
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
                    args = ins.UnpackArgs(argStruct);
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
                            args = ins.UnpackArgs(argStruct);
                        }
                        catch { }
                    }
                }
                int expectedLength = positions[argStruct.Count];
                if (ins.ArgData.Length > expectedLength && !allowArgMismatch)
                {
                    throw new ArgumentException($"{ins.ArgData.Length - expectedLength} excess bytes of arg data at position {expectedLength}");
                }
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
                        }
                    }
                    if (!isParam)
                    {
                        args[argIndex] = formatArgFunc(argDoc, args[argIndex]);
                    }
                }
            }
            return args;
        }

        public List<string> GetAltFunctionNames(string func)
        {
            if (Translator == null) return null;

            ConditionData.ConditionDoc doc = null;
            if (Functions.TryGetValue(func, out (int, int) indices))
            {
                if (Translator.Selectors.TryGetValue(
                    InstructionTranslator.InstructionID(indices.Item1, indices.Item2),
                    out InstructionTranslator.ConditionSelector selector))
                {
                    doc = selector.Cond;
                }
            }
            else if (Translator.CondDocs.TryGetValue(func, out InstructionTranslator.FunctionDoc funcDoc))
            {
                doc = funcDoc.ConditionDoc;
            }
            if (doc == null || doc.Hidden) return null;
            List<string> names = new List<string> { doc.Name };
            foreach (ConditionData.BoolVersion b in doc.AllBools)
            {
                names.Add(b.Name);
            }
            foreach (ConditionData.CompareVersion c in doc.AllCompares)
            {
                names.Add(c.Name);
            }
            names.Remove(func);
            return names;
        }

        private static readonly List<string> Acronyms = new List<string>()
        {
            "AI","HP","SE","SP","SFX","FFX","NPC"
        };

        #region Misc

        public static string TitleCaseName(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;

            string[] words = Regex.Replace(s, @"[^\w\s]", "").Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0)
                {
                    continue;
                }
                else if (Acronyms.Contains(words[i].ToUpper()))
                {
                    words[i] = words[i].ToUpper();
                    continue;
                }
                else if (words[i] == "SpEffect")
                {
                    continue;
                }

                char firstChar = char.ToUpper(words[i][0]);
                string rest = "";
                if (words[i].Length > 1)
                {
                    rest = words[i].Substring(1).ToLower();
                }
                words[i] = firstChar + rest;
            }
            string output = Regex.Replace(string.Join("", words), @"[^\w]", "");
            return output;
        }

        public static string CamelCaseName(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            string name = TitleCaseName(s);
            char firstChar = char.ToLower(name[0]);
            if (name.Length > 1)
                return firstChar + name.Substring(1);
            else
                return firstChar.ToString();
        }

        #endregion
    }
}
