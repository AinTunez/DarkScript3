using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SoulsFormats;
using static SoulsFormats.EMEVD;
using static SoulsFormats.EMEVD.Instruction;
using Newtonsoft.Json;

namespace DarkScript3
{
    public class EventReader
    {
        public EMEVD EMEVD;

        public EMEDF DOC;

        private BinaryReaderEx StringReader;

        bool HandleStringData = true;

        private List<string> LinkedFiles = new List<string>();

        public Dictionary<string, long> StringPositions = new Dictionary<string, long>();

        public Dictionary<string, string> TypeReplace = new Dictionary<string, string>()
        {
            ["Byte"] = "byte",
            ["UInt16"] = "ushort",
            ["UInt32"] = "uint",
            ["SByte"] = "sbyte",
            ["Int16"] = "short",
            ["Int32"] = "int",
            ["Single"] = "float",
            ["Long"] = "long",
            ["UInt64"] = "ulong",
            ["Text"] = "string"
        };

        public Dictionary<string, (int classIndex, int instrIndex)> Functions = new Dictionary<string, (int classIndex, int instrIndex)>();

        public EventReader(EMEVD evd, string docStreamPath)
        {
            EMEVD = evd;
            DOC = InitDocsFromResource(docStreamPath);
        }

        public EventReader()
        {

        }

        public string Parse()
        {
            StringBuilder code = new StringBuilder();
            if (HandleStringData)
            {
                StringReader = new BinaryReaderEx(false, EMEVD.StringData);
                LinkedFiles = new List<string>();
                foreach (long offset in EMEVD.LinkedFileOffsets)
                {
                    string linkedFile = StringReader.GetUTF16(offset);
                    LinkedFiles.Add(linkedFile);
                }
            }

            foreach (Event evt in EMEVD.Events)
            {
                try
                {
                    code.AppendLine(EventToString(evt, DOC));
                }
                catch (Exception ex)
                {
                    code.AppendLine($"ERROR (Event {evt.ID})\n");
                    code.AppendLine(ex.ToString().Replace("SoulScript.GUI.", ""));
                    code.AppendLine("");
                    return code.ToString().Trim();
                }
            }
            return code.ToString().Trim();
        }

        public EMEVD Pack(string code)
        {
            EMEVD evdOut = new EMEVD();
            evdOut.Compression = EMEVD.Compression;
            evdOut.Format = EMEVD.Format;

            #region Write String Data

            // remove comments
            code = Regex.Replace(code, @"#.*(\r?\n|$)", "$1");

            if (HandleStringData)
            {
                // will output to StringData[]
                BinaryWriterEx StringWriter = new BinaryWriterEx(false);

                // everything inside quotes is a string substitution
                StringPositions.Clear();
                MatchCollection stringMatches = Regex.Matches(code, "\".*\"");
                foreach (Match match in stringMatches)
                {
                    // write each string to the output stream and
                    // log the position at which it's written
                    if (!StringPositions.ContainsKey(match.Value))
                    {
                        StringPositions[match.Value] = StringWriter.Position;
                        string stringOut = match.Value.Trim('"');
                        StringWriter.WriteUTF16(stringOut, true);
                    }
                }

                // write each linked file to the output stream and log its position
                foreach (string linkedFile in LinkedFiles)
                {
                    evdOut.LinkedFileOffsets.Add(StringWriter.Position);
                    StringWriter.WriteUTF16(linkedFile, true);
                }

                // save the output stream to the EMEVD
                StringWriter.Pad(0x10);
                evdOut.StringData = StringWriter.FinishBytes();

                // replace each occurence of the string with its byte position
                foreach (KeyValuePair<string, long> kv in StringPositions)
                    code = code.Replace(kv.Key, kv.Value.ToString());
            }
            else
            {
                evdOut.LinkedFileOffsets = EMEVD.LinkedFileOffsets;
                evdOut.StringData = EMEVD.StringData;
            }

            #endregion

            // process each event
            IEnumerable<string> events = code.Split('@').Where(s => !string.IsNullOrWhiteSpace(s));
            foreach (string evt in events)
                evdOut.Events.Add(StringToEvent(evt));

            // validate and write
            return evdOut;
        }

        private string EventToString(Event evt, EMEDF DOC)
        {
            List<string> headerParams = new List<string>();
            Dictionary<long, List<Parameter>> eventParamList = new Dictionary<long, List<Parameter>>();

            for (int i = 0; i < evt.Instructions.Count; i++)
                eventParamList[i] = new List<Parameter>();

            foreach (Parameter p in evt.Parameters)
                eventParamList[p.InstructionIndex].Add(p);

            StringBuilder instructionsOut = new StringBuilder();

            for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
            {
                Instruction ins = evt.Instructions[insIndex];
                EMEDF.InstrDoc doc = DOC[ins.Bank][ins.ID];
                string funcName = UTIL.TitleCaseName(doc.Name);
                SortedDictionary<long, object> argList = UnpackArgsWithBytePositions(ins.ArgData, doc.Arguments.Select(arg => (ArgType)arg.Type));
                if (argList.Keys.Count != doc.Arguments.Length)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"ERROR reading event {evt.ID}");
                    sb.AppendLine("");
                    sb.AppendLine($"Event.Instructions[{insIndex}] has {argList.Keys.Count} arguments.");
                    sb.AppendLine($"Expected {doc.Arguments.Length} arguments for {ins.Bank}[{ins.ID}]");
                    throw new Exception(sb.ToString());
                }

                for (int k = 0; k < doc.Arguments.Length; k++)
                {
                    long pos = argList.Keys.ElementAt(k);
                    object arg = argList[pos];
                    EMEDF.ArgDoc argDoc = doc.Arguments[k];
                    bool isParam = false;
                    foreach (Parameter prm in eventParamList[insIndex])
                    {
                        if (prm.TargetStartByte == pos)
                        {
                            isParam = true;
                            argList[pos] = ParamString(prm);
                            string h = ParamString(prm, argDoc.Type);
                            if (!headerParams.Contains(h)) headerParams.Add(h);
                        }
                    }
                    if (isParam)
                    {
                        continue;
                    }
                    else if (HandleStringData && argDoc.Type == 8)
                    {
                        argList[pos] = $@"""{StringReader.GetUTF16((uint)arg)}""";
                    }
                    else if (argDoc.EnumName != null)
                    {
                        EMEDF.EnumDoc enm = DOC.Enums.First(e => e.Name == doc.Arguments[k].EnumName);
                        argList[pos] = enm.Values[argList[pos].ToString()];
                    }
                }

                string output = $"\t{funcName}({string.Join(", ", argList.Values)})";
                if (ins.Layer.HasValue)
                {
                    List<int> bitList = new List<int>();
                    for (int b = 0; b < 32; b++)
                        if ((ins.Layer.Value & (1 << b)) != 0)
                            bitList.Add(b);

                    output = $"{output}[{string.Join(", ", bitList)}]";
                }
                instructionsOut.AppendLine(output);
            }

            if (evt.ID == 711)
            {
                Console.WriteLine(JsonConvert.SerializeObject(evt.Parameters, Formatting.Indented));
            }

            StringBuilder header = new StringBuilder();
            header.AppendLine("@" + evt.RestBehavior.ToString());
            header.AppendLine("def Event" + evt.ID + "(" + string.Join(", ", headerParams) + "):");
            return header.ToString() + instructionsOut.ToString();
        }

        private Event StringToEvent(string input)
        {
            // basic event setup
            Event evt = new Event();

            string[] inputLines = input.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            string[] headerLines = inputLines.Take(2).ToArray();
            string[] instructionLines = inputLines.Skip(2).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();

            string restBehaviorString = headerLines[0].Replace("@", "").Trim();
            evt.RestBehavior = (Event.RestBehaviorType)Enum.Parse(typeof(Event.RestBehaviorType), restBehaviorString);

            string idString = Regex.Match(headerLines[1], @"def\s+Event(\d+)\s*\(").Groups[1].Value;
            evt.ID = long.Parse(idString);
            
            int pStart = headerLines[1].IndexOf("(") + 1;
            int pEnd = headerLines[1].LastIndexOf(")");
            string[] paramStrings = new string[0];
            if (pEnd > pStart)
            {
                string sub = headerLines[1].Substring(pStart, pEnd - pStart);
                paramStrings = Regex.Split(sub, @"\s*,\s*");
            }

            // create a container to hold data about parameter 
            // substitutions and initialize each entry

            var paramLinesList = new Dictionary<string, List<(long instructionIndex, long targetStartPos)>>();
            foreach (string str in paramStrings)
            {
                string argName = Regex.Split(str, @"\s*:\s*")[0];
                paramLinesList[argName] = new List<(long insIndex, long targetStartPos)>();
            }

            //convert each remaining line of text to a valid Instruction

            for (int lineNum = 0; lineNum < instructionLines.Length; lineNum++)
            {
                string insText = instructionLines[lineNum];
                string funcName = insText.Substring(0, insText.IndexOf("(")).Trim();
                if (!Functions.ContainsKey(funcName))
                    throw new Exception("Invalid function name: " + funcName);

                (int classIndex, int instrIndex) func = Functions[funcName];
                Instruction ins = new Instruction(func.classIndex, func.instrIndex);
                EMEDF.InstrDoc insDoc = DOC[func.classIndex][func.instrIndex];

                //parse layers
                MatchCollection matches = new Regex(@"\)\s*\[(.*)\]\s*$").Matches(insText);
                if (matches.Count > 0)
                {
                    string layerText = matches[0].Groups[1].Value;
                    IEnumerable<int> nums = Regex.Split(layerText, @"\s*,\s*").Select(s => int.Parse(s));
                    ins.Layer = 0;
                    foreach (int num in nums)
                        ins.Layer |= (uint) 1 << num;
                }

                //read the arguments and handle functions that have none

                int start = insText.IndexOf("(");
                int end = insText.LastIndexOf(")") + 1;
                string fullArgString = insText.Substring(start, end - start).Trim();
                fullArgString = fullArgString.Substring(1, fullArgString.Length - 2);

                if (fullArgString.Length > 0)
                {
                    string[] rawArgStrings = Regex.Split(fullArgString, @"\s*,\s*");
                    List<object> argOut = new List<object>();

                    // keep track of the byte position while writing args
                    // in case there's a parameter substitution
                    int bytePos = 0;

                    for (int argIndex = 0; argIndex < rawArgStrings.Length; argIndex++)
                    {
                        string argString = rawArgStrings[argIndex];
                        EMEDF.ArgDoc argDoc = insDoc.Arguments[argIndex];
                        int defLength = ByteLengthFromDoc(argDoc);

                        // goddamn PADDING
                        if (bytePos % defLength > 0)
                            bytePos += defLength - (bytePos % defLength);

                        if (argString.StartsWith("ARG"))
                        {
                            // use the default value and log the byte position
                            if (paramLinesList.ContainsKey(argString))
                            {
                                paramLinesList[argString].Add((lineNum, bytePos));
                                argOut.Add(ArgFromDefault(argDoc));
                            }
                            else
                            {
                                throw new Exception($"{argString} is not defined as a parameter in event {evt.ID}");
                            }
                        }
                        else if (argDoc.EnumName != null)
                        {
                            // convert the argument to its numeric enum equivalent
                            argOut.Add(ArgFromEnum(argString, argDoc));
                        }
                        else
                        {
                            // process the argument normally
                            argOut.Add(ArgFromString(argString, argDoc));
                        }
                        bytePos += defLength;
                    }
                    ins.PackArgs(argOut);
                }
                evt.Instructions.Add(ins);
            }

            // use the previously-generated list of parameters
            // to write them to the new Event

            foreach (string eventParamString in paramLinesList.Keys)
            {
                int[] nums = eventParamString.Split('_').Skip(1).Select(n => int.Parse(n)).ToArray();
                foreach ((long instructionIndex, long targetStartPos) in paramLinesList[eventParamString])
                {
                    if (instructionIndex >= evt.Instructions.Count)
                    {
                        throw new Exception($"{evt.ID} : Invalid instruction index {instructionIndex}");
                    }
                    else
                    {
                        Parameter prm = new Parameter(instructionIndex, targetStartPos, nums[0], nums[1]);
                        evt.Parameters.Add(prm);
                    }
                }
            }
            return evt;
        }

        private string ParamString(Parameter prm, long? t = null)
        {
            long start = prm.SourceStartByte;
            int length = prm.ByteCount;
            string output = $"ARG_{start}_{length}";
            if (t.HasValue) output = $"{output}: {TypeReplace[((ArgType)t.Value).ToString()]}";
            return output;
        }

        private int ByteLengthFromDoc(EMEDF.ArgDoc argDoc)
        {
            if (argDoc.Type == 0) return 1; //u8
            if (argDoc.Type == 1) return 2; //u16
            if (argDoc.Type == 2) return 4; //u32
            if (argDoc.Type == 3) return 1; //s8
            if (argDoc.Type == 4) return 2; //s16
            if (argDoc.Type == 5) return 4; //s32
            if (argDoc.Type == 6) return 4; //f32
            if (argDoc.Type == 8) return 4; //string position
            throw new Exception("Invalid type in argument definition.");
        }

        private object ArgFromDefault(EMEDF.ArgDoc argDoc)
        {
            if (argDoc.Type == 0) return (byte)argDoc.Default; //u8
            if (argDoc.Type == 1) return (ushort)argDoc.Default; //u16
            if (argDoc.Type == 2) return (uint)argDoc.Default; //u32
            if (argDoc.Type == 3) return (sbyte)argDoc.Default; //s8
            if (argDoc.Type == 4) return (short)argDoc.Default; //s16
            if (argDoc.Type == 5) return (int)argDoc.Default; //s32
            if (argDoc.Type == 6) return (float)argDoc.Default; //f32
            if (argDoc.Type == 8) return (uint)0; //string position
            throw new Exception("Invalid type in argument definition.");
        }

        private object ArgFromEnum(string argString, EMEDF.ArgDoc argDoc)
        {
            EMEDF.EnumDoc enm = DOC.Enums.First(e => e.Name == argDoc.EnumName);
            KeyValuePair<string, string> newArgPair = enm.Values.FirstOrDefault(kv => kv.Value == argString);
            if (newArgPair.Equals(default(KeyValuePair<string, string>)))
                throw new Exception("Could not parse \"" + argString + "\" to enum \"" + enm.Name + "\"");
            return ArgFromString(newArgPair.Key, argDoc);
        }

        private object ArgFromString(string argString, EMEDF.ArgDoc argDoc)
        {
            try
            {
                if (argDoc.Type == 0) return byte.Parse(argString); // u8
                if (argDoc.Type == 1) return ushort.Parse(argString); // u16
                if (argDoc.Type == 2) return uint.Parse(argString); // u32
                if (argDoc.Type == 3) return sbyte.Parse(argString); // s8
                if (argDoc.Type == 4) return short.Parse(argString); // s16
                if (argDoc.Type == 5) return int.Parse(argString); // s32
                if (argDoc.Type == 6) return float.Parse(argString); // f32
                if (argDoc.Type == 7) return ulong.Parse(argString); // u64, possibly unused
                if (argDoc.Type == 8) return uint.Parse(argString); // string positions (uint32)
                throw new Exception();
            }
            catch
            {
                throw new Exception("Error parsing \"" + argString + "\" into " + ((ArgType)argDoc.Type));
            }
        }

        private SortedDictionary<long, object> UnpackArgsWithBytePositions(byte[] args, IEnumerable<ArgType> argStruct)
        {
            SortedDictionary<long, object> argDict = new SortedDictionary<long, object>();

            using (MemoryStream ms = new MemoryStream(args))
            {
                BinaryReaderEx br = new BinaryReaderEx(false, ms);
                foreach (ArgType arg in argStruct)
                {
                    switch (arg)
                    {
                        case ArgType.Byte:
                            argDict[br.Position] = br.ReadByte(); break;
                        case ArgType.UInt16:
                            br.Pad(2);
                            argDict[br.Position] = br.ReadUInt16(); break;
                        case ArgType.Text:
                        case ArgType.UInt32:
                            br.Pad(4);
                            argDict[br.Position] = br.ReadUInt32(); break;
                        case ArgType.SByte:
                            argDict[br.Position] = br.ReadSByte(); break;
                        case ArgType.Int16:
                            br.Pad(2);
                            argDict[br.Position] = br.ReadInt16(); break;
                        case ArgType.Int32:
                            br.Pad(4);
                            argDict[br.Position] = br.ReadInt32(); break;
                        case ArgType.Single:
                            br.Pad(4);
                            argDict[br.Position] = br.ReadSingle(); break;
                        case ArgType.Int64:
                            br.Pad(8);
                            argDict[br.Position] = br.ReadUInt64(); break;
                        default:
                            throw new NotImplementedException($"Unimplemented argument type: {arg}");
                    }
                }

                return argDict;
            }
        }

        private EMEDF InitDocsFromResource(string streamPath)
        {
            EMEDF DOC = EMEDF.ReadStream(streamPath);
            foreach (EMEDF.EnumDoc enm in DOC.Enums)
            {
                foreach (KeyValuePair<string, string> pair in enm.Values.ToList())
                {
                    string val = enm.Values[pair.Key];
                    enm.Values[pair.Key] = val.Replace(" ", "");
                }
            }

            foreach (EMEDF.ClassDoc bank in DOC.Classes)
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    string funcName = UTIL.TitleCaseName(instr.Name);
                    Functions[funcName] = ((int)bank.Index, (int)instr.Index);
                }
            }
            return DOC;
        }

    }
}
