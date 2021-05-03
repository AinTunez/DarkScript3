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

        // Enum values by value display name for fancy compilation, for control/negate args etc. which must be read as ints.
        // These should not be packed directly into instruction args, as most enums are not actually ints.
        public Dictionary<string, int> EnumValues = new Dictionary<string, int>();

        // Callable objects by display name, both instructions and condition functions
        public Dictionary<string, List<EMEDF.ArgDoc>> AllArgs = new Dictionary<string, List<EMEDF.ArgDoc>>();

        // Byte offsets based on argument types. Used in decompilation.
        public Dictionary<EMEDF.InstrDoc, List<uint>> FuncBytePositions = new Dictionary<EMEDF.InstrDoc, List<uint>>();

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
            ResourceString = resource;
            DOC = InitDocsFromResource(resource);
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

        private EMEDF InitDocsFromResource(string streamPath)
        {
            EMEDF DOC;
            if (File.Exists(streamPath))
                DOC = EMEDF.ReadFile(streamPath);
            else if (File.Exists(@"Resources\" + streamPath))
                DOC = EMEDF.ReadFile(@"Resources\" + streamPath);
            else
                DOC = EMEDF.ReadStream(streamPath);

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
                    string funcName = TitleCaseName(instr.Name);
                    Functions[funcName] = ((int)bank.Index, (int)instr.Index);
                    FuncBytePositions[instr] = GetArgBytePositions(instr.Arguments.Select(i => (ArgType)i.Type).ToList());
                    // Also filled in from conditions
                    AllArgs[funcName] = instr.Arguments.ToList();

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

            return DOC;
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
        /// Returns the textual representation of an event initializer's arguments.
        /// </summary>
        /// <param name="args"></param>
        /// <param name="insIndex"></param>
        /// <param name="paramNames"></param>
        /// <param name="argStruct"></param>
        /// <returns></returns>
        public string ArgumentStringInitializer(string[] args, int insIndex, Dictionary<Parameter, string> paramNames, IEnumerable<ArgType> argStruct)
        {
            List<uint> positions = GetArgBytePositions(argStruct, 6);
            for (int argIndex = 0; argIndex < argStruct.Count(); argIndex++)
            {
                uint bytePos = positions[argIndex];
                foreach (Parameter prm in paramNames.Keys)
                {
                    if (prm.InstructionIndex == insIndex && bytePos == prm.TargetStartByte)
                    {
                        args[argIndex + 2] = paramNames[prm];
                    }
                }
            }
            return string.Join(", ", args).Trim();
        }

        /// <summary>
        /// Returns the textual representation of an event's arguments.
        /// </summary>
        public string ArgumentString(string[] args, Instruction ins, int insIndex, Dictionary<Parameter, string> paramNames)
        {
            var insDoc = DOC[ins.Bank][ins.ID];
            for (int argIndex = 0; argIndex < args.Count(); argIndex++)
            {
                EMEDF.ArgDoc argDoc = insDoc.Arguments[argIndex];
                uint bytePos = FuncBytePositions[insDoc][argIndex];
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
                    args[argIndex] = argDoc.GetDisplayValue(args[argIndex]).ToString();
                }
            }
            if (args.Length > 0) return string.Join(", ", args).Trim();
            return "";
        }

        /// <summary>
        /// Returns a list of byte positions for an ordered list of argument types.
        /// </summary>
        public List<uint> GetArgBytePositions(IEnumerable<ArgType> argStruct, uint startPos = 0)
        {
            List<uint> positions = new List<uint>();
            uint bytePos = startPos;
            for (int i = 0; i < argStruct.Count(); i++)
            {
                long argType = (long)argStruct.ElementAt(i);
                uint defLength = (uint)ByteLengthFromType(argType);
                if (bytePos % defLength > 0)
                    bytePos += defLength - (bytePos % defLength);

                positions.Add(bytePos);
                bytePos += defLength;
            }
            return positions;
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
            if (doc == null) return null;
            List<string> names = new List<string> { doc.Name };
            foreach (ConditionData.BoolVersion b in doc.AllBools)
            {
                names.Add(b.Name);
            }
            if (doc.Compare != null)
            {
                names.Add(doc.Compare.Name);
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
