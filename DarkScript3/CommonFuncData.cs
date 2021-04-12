using System.Collections.Generic;
using System.IO;
using static SoulsFormats.EMEVD.Instruction;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    class CommonFuncData
    {
        public EMEDF DOC;
        public string ProjectPath;
        public Dictionary<long, List<ArgType>> commonFuncData = new Dictionary<long, List<ArgType>>();
        public Dictionary<string, EventScripter> FileDB = new Dictionary<string, EventScripter>();

        public CommonFuncData(string directory, EMEDF doc)
        {
            ProjectPath = directory;
            DOC = doc;
            string[] files = Directory.GetFiles(directory, "*.emevd, *");
            foreach (string file in files)
            {
                EventScripter scripter = new EventScripter(file);
                populateParamDB(scripter);
                FileDB[Path.GetFileName(file)] = scripter;
                string text = scripter.Unpack(commonFuncData);
                File.WriteAllText(file.Replace(".dcx", "") + ".js", text);
            }
        }

        private void populateParamDB(EventScripter scripter)
        {
            scripter.EVD.Events.ForEach((evt) =>
            {
                if (evt.Parameters.Count > 0 && !commonFuncData.ContainsKey(evt.ID))
                {
                    List<long> startBytes = new List<long>();
                    List<ArgType> args = new List<ArgType>();

                    evt.Parameters.ForEach(p =>
                    {
                        if (!startBytes.Contains(p.SourceStartByte))
                        {
                            startBytes.Add(p.SourceStartByte);
                            Instruction ins = evt.Instructions[(int)p.InstructionIndex];
                            EMEDF.InstrDoc insInfo = DOC[ins.Bank][ins.ID];
                            List<uint> positions = scripter.FuncBytePositions[insInfo];
                            uint argIndex = (uint)positions.IndexOf((uint)p.TargetStartByte);
                            args.Add((ArgType)insInfo.Arguments[argIndex].Type);
                        }
                    });
                    commonFuncData[evt.ID] = args;
                }
            });
        }
    }
}
