using System.Collections.Generic;
using System.IO;
using System.Linq;
using SoulsFormats;

namespace DarkScript3
{
    // This is not equivalent to binary EMELD. It's a simple name mapping.
    public class EMELDTXT
    {
        public Dictionary<long, string> Names { get; set; } = new();

        public static Dictionary<long, string> ResolveNames(string game, string emevdPath)
        {
            // Priority order: file txt, file emeld, resource txt
            string emeldPath = emevdPath.Replace(".emevd", ".emeld");
            // This is slightly awkward with dev vs release environment, but it's a lot of text to make an embedded resource.
            string emeldResource = @$"Resources\emeld_{game}\{Path.GetFileName(emeldPath)}";
            if (File.Exists(emeldPath + ".txt"))
            {
                // Maybe EMELDTXT doesn't need to be instantiated here at all :fatcat:
                return ReadFile(emeldPath + ".txt").Names;
            }
            else if (File.Exists(emeldPath))
            {
                try
                {
                    EMELD ELD = EMELD.Read(emeldPath);
                    Dictionary<long, string> eventNames = new();
                    foreach (EMELD.Event ev in ELD.Events)
                    {
                        eventNames[ev.ID] = ev.Name;
                    }
                    return eventNames;
                }
                catch
                {
                }
            }
            else if (File.Exists(emeldResource + ".txt"))
            {
                return ReadFile(emeldResource + ".txt").Names;
            }
            return new();
        }

        public static EMELDTXT ReadFile(string path)
        {
            EMELDTXT emeld = new();
            foreach (string line in File.ReadLines(path))
            {
                string[] parts = line.Split(new[] { ' ' }, 2);
                if (parts.Length == 2 && long.TryParse(parts[0], out long id))
                {
                    emeld.Names[id] = parts[1];
                }
            }
            return emeld;
        }

        public void WriteFile(string path)
        {
            File.WriteAllLines(path, Names.OrderBy(e => e.Key).Select(e => $"{e.Key} {e.Value}"));
        }

        public static Dictionary<string, EMELDTXT> FromTsv(string path)
        {
            // TSV formatted with [filename, event id, names...]
            Dictionary<string, EMELDTXT> emelds = new();
            foreach (string line in File.ReadLines(path))
            {
                string[] parts = line.Split('\t');
                if (!long.TryParse(parts[1], out long id))
                {
                    continue;
                }
                string filename = parts[0];
                if (!emelds.TryGetValue(filename, out EMELDTXT emeld))
                {
                    emelds[filename] = emeld = new EMELDTXT();
                }
                emeld.Names[id] = string.Join(" -- ", parts.Skip(2));
            }
            return emelds;
        }
    }
}
