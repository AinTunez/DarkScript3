using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DarkScript3
{
    public class NameMetadata
    {
        private Dictionary<string, Dictionary<string, string>> MapNames = new Dictionary<string, Dictionary<string, string>>();
        private Dictionary<string, Dictionary<string, string>> ModelNames = new Dictionary<string, Dictionary<string, string>>();
        private Dictionary<string, Dictionary<string, string>> CharaNames = new Dictionary<string, Dictionary<string, string>>();
        private Dictionary<string, Dictionary<string, string>> FlagNames = new Dictionary<string, Dictionary<string, string>>();

        public Dictionary<string, string> GetMapNames(string resourceStr) => LoadNames(MapNames, resourceStr, "MapName");
        public Dictionary<string, string> GetModelNames(string resourceStr) => LoadNames(ModelNames, resourceStr, "ModelName");
        public Dictionary<string, string> GetCharaNames(string resourceStr) => LoadNames(CharaNames, resourceStr, "CharaName");
        public Dictionary<string, string> GetEventFlagNames(string resourceStr) => LoadNames(FlagNames, resourceStr, "EventFlag");

        private static Dictionary<string, string> LoadNames(
            Dictionary<string, Dictionary<string, string>> allNames,
            string resourceStr,
            string txtType)
        {
            if (string.IsNullOrEmpty(resourceStr))
            {
                // Sometimes null may be chained into this method
                return new Dictionary<string, string>();
            }
            string gameName = InstructionDocs.GameNameFromResourceName(resourceStr);
            string namesFile = $"{gameName}-common.{txtType}.txt";

            lock (allNames)
            {
                if (!allNames.TryGetValue(namesFile, out Dictionary<string, string> names))
                {
                    // Initialize it so we don't try again
                    allNames[namesFile] = names = new Dictionary<string, string>();
                    try
                    {
                        // Best-effort
                        string fileText = Resource.Text(namesFile);
                        foreach (string line in fileText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                        {
                            if (line.StartsWith("#")) continue;
                            string[] parts = line.Split(new[] { ' ' }, 2);
                            if (parts.Length == 2 && !string.IsNullOrEmpty(parts[1]))
                            {
                                names[parts[0]] = parts[1];
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Resource not exist, usually
                    }
                }
                return names;
            }
        }
    }
}
