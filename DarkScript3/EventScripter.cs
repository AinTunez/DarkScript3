using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using SoulsFormats;
using Microsoft.ClearScript.V8;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;
using static SoulsFormats.EMEVD.Instruction;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    public class EventScripter
    {
        public EMEVD EVD = new EMEVD();

        public EMEDF DOC { get; set; } = new EMEDF();

        private V8ScriptEngine v8 = new V8ScriptEngine();

        public Dictionary<string, (int classIndex, int instrIndex)> Functions = new Dictionary<string, (int classIndex, int instrIndex)>();

        public Dictionary<EMEDF.InstrDoc, List<uint>> FuncBytePositions = new Dictionary<EMEDF.InstrDoc, List<uint>>();

        public List<string> GlobalConstants = new List<string>() { "Default", "End", "Restart" };

        public Dictionary<string, string> EnumReplacements = new Dictionary<string, string>
        {
            { "BOOL.TRUE", "true" }, 
            { "BOOL.FALSE", "false" }
        };

        public List<string> EnumNamesForGlobalization = new List<string>
        {
            "ONOFF",
            "ONOFFCHANGE",
            "ConditionGroup",
            "ConditionState",
            "DisabledEnabled",
        };

        private List<string> LinkedFiles = new List<string>();

        public EventScripter(string file, string resource = "ds1-common.emedf.json")
        {
            EVD = EMEVD.Read(file);
            InitAll(resource);
        }

        public EventScripter(EMEVD evd = null, string resource = "ds1-common.emedf.json")
        {
            if (evd != null) EVD = evd;
        }

        public Instruction MakeInstruction(Event evt, int bank, int index, object[] args)
        {
            EMEDF.InstrDoc doc = DOC[bank][index];
            if (args.Length < doc.Arguments.Length)
                throw new Exception($"Instruction {bank}[{index}] ({doc.Name}) requires {doc.Arguments.Length} arguments.");

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is bool)
                    args[i] = (bool) args[i] ? 1 : 0;
                else if (args[i] is string)
                {
                    if (doc == DOC[2000][0])
                        throw new Exception("Event initializers cannot be dependent on parameters.");

                    IEnumerable<int> nums = (args[i] as string).Substring(1).Split('_').Select(s => int.Parse(s));
                    if (nums.Count() != 2)
                        throw new Exception("Invalid parameter string: {" + args[i] + "}");

                    int sourceStartByte = nums.ElementAt(0);
                    int length = nums.ElementAt(1);
                    uint targetStartByte = FuncBytePositions[doc][i];

                    Parameter p = new Parameter(evt.Instructions.Count, targetStartByte, sourceStartByte, length);
                    evt.Parameters.Add(p);
                    evt.Parameters = evt.Parameters.OrderBy(prm => prm.SourceStartByte).ToList();

                    args[i] = doc.Arguments[i].Default;
                }
            }

            List<object> properArgs = new List<object>();
            if (bank == 2000 && index == 0)
            {
                properArgs.Add(Convert.ToInt32(args[0]));
                properArgs.Add(Convert.ToUInt32(args[1]));
                if (args.Length > 2)
                    for (int i = 2; i < args.Length; i++)
                        properArgs.Add(Convert.ToInt32(args[i]));
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
            return ins;
        }

        public Instruction MakeInstruction(Event evt, int bank, int index, uint layer, object[] args)
        {
            Instruction ins = MakeInstruction(evt, bank, index, args);
            ins.Layer = layer;
            return ins;
        }

        public void InitAll(string resource)
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

            if (resource == null)
            {
                var chooser = new GameChooser();
                chooser.ShowDialog();
                DOC = InitDocsFromResource(chooser.GameDocs);
            }
            else
            {
                DOC = InitDocsFromResource(resource);
            }
        }

        private EMEDF InitDocsFromResource(string streamPath)
        {
            EMEDF DOC = EMEDF.ReadStream(streamPath);
            foreach (EMEDF.EnumDoc enm in DOC.Enums)
            {
                enm.Name = Regex.Replace(enm.Name, @"[^\w]", "");
                if (EnumNamesForGlobalization.Contains(enm.Name))
                {
                    foreach (var pair in enm.Values.ToList())
                    {
                        string val = Regex.Replace(pair.Value, @"[^\w]", "");
                        enm.Values[pair.Key] = val;
                        EnumReplacements[$"{enm.Name}.{val}"] = val;
                        GlobalConstants.Add(val);
                        string code = $"const {val} = {int.Parse(pair.Key)};";
                        if (enm.Name != "ONOFF") //handled by ON/OFF/CHANGE
                            v8.Execute(code);
                    }
                } else
                {
                    StringBuilder code = new StringBuilder();
                    code.AppendLine($"const {enm.Name} = {{");
                    foreach (var pair in enm.Values.ToList())
                    {
                        string val = Regex.Replace(pair.Value, @"[^\w]", "");
                        enm.Values[pair.Key] = val;
                        code.AppendLine($"{val}:{pair.Key},");
                    }
                    code.AppendLine("};");
                    v8.Execute(code.ToString());
                }
            }

            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    string funcName = TitleCaseName(instr.Name);
                    Functions[funcName] = ((int)bank.Index, (int)instr.Index);
                    FuncBytePositions[instr] = GetArgBytePositions(instr.Arguments.Select(i => (ArgType)i.Type).ToList());
                    
                    foreach (var arg in instr.Arguments)
                    {
                        arg.Name = CamelCaseName(arg.Name.Replace("Class", "Class Name"));
                        if (arg.EnumName != null)
                            arg.EnumName = Regex.Replace(arg.EnumName, @"[^\w]", "");
                    }

                    var args = instr.Arguments.Select(a => a.Name);
                    string argNames = string.Join(", ", args);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"function {funcName} ({argNames}) {{");
                    foreach (var arg in args)
                    {
                        sb.AppendLine($"if ({arg} === void 0)");
                        sb.AppendLine($@"throw 'Argument \""{arg}\"" in instruction \""{funcName}\"" not properly set.'");
                    }
                    sb.AppendLine($"return _Instruction({bank.Index}, {instr.Index}, Array.from(arguments));");
                    sb.AppendLine("}");

                    v8.Execute(sb.ToString());
                }
            }
            return DOC;
        }

        public EMEVD Pack(string code)
        {
            EVD.Events.Clear();
            v8.Execute($"(function() {{ {code} }})();");
            return EVD;
        }

        public string Unpack()
        {
            InitLinkedFiles();

            StringBuilder code = new StringBuilder();
            foreach (var evt in EVD.Events)
            {
                string id = evt.ID.ToString();
                string restBehavior = evt.RestBehavior.ToString();

                //each parameter's string representation
                Dictionary<Parameter, string> paramNames = ParamNames(evt);
                IEnumerable<string> argNameList = paramNames.Values.Distinct();
                string evtArgs = string.Join(", ", argNameList);

                code.AppendLine($"Event({id}, {restBehavior}, function({evtArgs}) {{");

                for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
                {
                    Instruction ins = evt.Instructions[insIndex];
                    EMEDF.InstrDoc doc = DOC[ins.Bank][ins.ID];
                    string funcName = TitleCaseName(doc.Name);

                    IEnumerable<ArgType> argStruct = doc.Arguments.Select(arg => (ArgType)arg.Type);
                    string[] args = default;
                    string argString = "";

                    try
                    {
                        if (doc == DOC[2000][0])
                        {
                            argStruct = Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4);
                            args = ins.UnpackArgs(argStruct).Select(a => a.ToString()).ToArray();
                            argString = ArgumentStringInitializer(args, insIndex, paramNames, argStruct);
                        }
                        else
                        {
                            args = ins.UnpackArgs(argStruct).Select(a => a.ToString()).ToArray();
                            argString = ArgumentString(args, ins, insIndex, paramNames);
                        }
                    }
                    catch (Exception ex)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($@"ERROR: Unable to unpack arguments for ""{funcName}""");
                        sb.AppendLine(ex.ToString());
                        throw new Exception(sb.ToString());
                    }

                    if (ins.Layer.HasValue)
                    {
                        string str = LayerString(ins.Layer.Value);
                        if (argString.Length > 0)
                            argString = $"{argString}, {str}";
                        else
                            argString = str;
                    }

                    string lineOfCode = $"\t{TitleCaseName(doc.Name)}({argString});";
                    code.AppendLine(lineOfCode);

                }
                code.AppendLine("});");
                code.AppendLine("");
            }
            return code.ToString();
        }

        private void InitLinkedFiles()
        {
            var reader = new BinaryReaderEx(false, EVD.StringData);
            foreach (long offset in EVD.LinkedFileOffsets)
            {
                string linkedFile = reader.GetUTF16(offset);
                LinkedFiles.Add(linkedFile);
            }
        }

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

        public string LayerString(uint layerValue)
        {
            List<int> bitList = new List<int>();
            for (int b = 0; b < 32; b++)
                if ((layerValue & (1 << b)) != 0)
                    bitList.Add(b);

            return $"$LAYERS({string.Join(", ", bitList)})";
        }

        public Dictionary<Parameter, string> ParamNames (Event evt)
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

                if (!isParam && argDoc.EnumName != null)
                {
                    var enm = DOC.Enums.First(e => e.Name == argDoc.EnumName);
                    string enumString = $"{enm.Name}.{enm.Values[args[argIndex]]}";
                    if (EnumReplacements.ContainsKey(enumString)) enumString = EnumReplacements[enumString];
                    args[argIndex] = enumString;
                }
            }
            if (args.Length > 0) return string.Join(", ", args).Trim();
            return "";
        }

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

        static List<string> Acronyms = new List<string>()
        {
            "AI","HP","SE","SP","SFX","FFX","NPC"
        };

        public static string TitleCaseName(string s)
        {
            Console.WriteLine(s);
            if (string.IsNullOrEmpty(s)) return s;

            string[] words = Regex.Replace(s, @"[^\w\s]","").Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length == 0) continue;
                else if (Acronyms.Contains(words[i].ToUpper()))
                {
                    words[i] = words[i].ToUpper();
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
            Console.WriteLine(output);
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
    }
}
