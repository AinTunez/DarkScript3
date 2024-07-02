using Newtonsoft.Json;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkScript3
{
    public class HeaderData
    {
        public string GameDocs { get; set; }
        public DCX.Type Compression { get; set; }
        public EMEVD.Game Game { get; set; }
        public byte[] StringData { get; set; }
        public List<long> LinkedFileOffsets { get; set; }
        public string Version { get; set; }
        // Should only be used by ScriptSettings
        public Dictionary<string, string> ExtraSettings { get; set; }

        private HeaderData() { }

        public static HeaderData Create(EventScripter scripter, InstructionDocs docs, Dictionary<string, string> extraSettings)
        {
            return new HeaderData
            {
                GameDocs = docs.ResourceString,
                Compression = scripter.EVD.Compression,
                Game = scripter.EVD.Format,
                StringData = scripter.EVD.StringData,
                LinkedFileOffsets = scripter.EVD.LinkedFileOffsets,
                ExtraSettings = extraSettings,
                Version = ProgramVersion.VERSION,
            };
        }

        public void Write(StringBuilder sb, InstructionDocs docs)
        {
            sb.AppendLine("// ==EMEVD==");
            sb.AppendLine($"// @docs    {GameDocs}");
            sb.AppendLine($"// @compress    {Compression}");
            sb.AppendLine($"// @game    {Game}");
            sb.AppendLine($"// @string    {Escape(StringData, docs.IsASCIIStringData)}");
            sb.AppendLine($"// @linked    [{string.Join(",", LinkedFileOffsets)}]");
            foreach (KeyValuePair<string, string> extra in ExtraSettings)
            {
                sb.AppendLine($"// @{extra.Key}    {extra.Value}");
            }
            // Hardcode this just to make sure we update it always
            sb.AppendLine($"// @version    {ProgramVersion.VERSION}");
            sb.AppendLine("// ==/EMEVD==");
            sb.AppendLine("");
        }

        public static bool Read(string text, out HeaderData data)
        {
            data = null;
            Dictionary<string, string> headers = GetHeaderValues(text);
            List<string> emevdFileHeaders = new List<string> { "docs", "compress", "game", "string", "linked" };
            if (!emevdFileHeaders.All(name => headers.ContainsKey(name))) return false;
            string dcx = headers["compress"];
            if (!Enum.TryParse(dcx, out DCX.Type compression))
            {
                // We also have to account for historical DCX.Type names, which mostly correspond
                // to DCX.DefaultType.
                if (Enum.TryParse(dcx, out DCX.DefaultType defaultComp))
                {
                    compression = (DCX.Type)defaultComp;
                }
                else if (dcx == "SekiroKRAK" || dcx == "SekiroDFLT")
                {
                    // This is turned into SekiroDFLT when it's actually written out. Store it as KRAK in the header
                    // just in case it's supported one day.
                    compression = (DCX.Type)DCX.DefaultType.Sekiro;
                }
                else
                {
                    throw new Exception($"Unknown compression type in file header {headers["compress"]}");
                }
            }
            if (!Enum.TryParse(headers["game"], out EMEVD.Game game))
            {
                throw new Exception($"Unknown game type in file header {headers["game"]}");
            }
            string linkedStr = headers["linked"].TrimStart('[').TrimEnd(']');
            List<long> linked = Regex.Split(linkedStr, @"\s*,\s*")
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Select(o => long.Parse(o))
                .ToList();
            string docs = headers["docs"];
            data = new HeaderData
            {
                GameDocs = docs,
                Compression = compression,
                Game = game,
                // IsASCIIStringData, but InstructionDocs may not exist at this point
                StringData = Unescape(headers["string"], docs.StartsWith("ds2")),
                LinkedFileOffsets = linked,
                ExtraSettings = headers,
                Version = headers.TryGetValue("version", out string version) ? version : null,
            };
            return true;
        }

        public static string Trim(string text)
        {
            return Regex.Replace(text, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==", "");
        }

        private static Dictionary<string, string> GetHeaderValues(string fileText)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            // Some example lines are:
            // ==EMEVD==
            // @docs    sekiro-common.emedf.json
            // @game    Sekiro
            // ...
            // ==/EMEVD==
            Match headerText = Regex.Match(fileText, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==");
            if (headerText.Success)
            {
                string[] result = Regex.Split(headerText.Value, @"(\r\n|\r|\n)\s*");
                foreach (string headerLine in result.ToArray())
                {
                    Match lineMatch = Regex.Match(headerLine, @"^//\s+@(\w+)\s+(.*)");
                    if (lineMatch.Success)
                    {
                        ret[lineMatch.Groups[1].Value] = lineMatch.Groups[2].Value.Trim();
                    }
                }
            }
            return ret;
        }

        public static byte[] Unescape(string text, bool ds2)
        {
            if (text.StartsWith('"'))
            {
                text = JsonConvert.DeserializeObject<string>(text);
            }
            return ds2 ? Encoding.ASCII.GetBytes(text) : Encoding.Unicode.GetBytes(text);
        }

        public static string Escape(byte[] data, bool ds2)
        {
            string text = ds2 ? Encoding.ASCII.GetString(data) : Encoding.Unicode.GetString(data);
            text = JsonConvert.SerializeObject(text);
            return text;
        }
    }
}
