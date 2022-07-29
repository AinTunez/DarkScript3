using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using SoulsFormats;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    /// <summary>
    /// Routines for generating files to check round trip conversion of packing and unpacking.
    /// </summary>
    public class RoundTripTool
    {
        private static readonly Dictionary<string, string> defaultGameDirs = new Dictionary<string, string>
        {
            ["ds1"] = @"C:\Program Files (x86)\Steam\steamapps\common\Dark Souls Prepare to Die Edition\DATA\event",
            ["ds1r"] = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event",
            ["ds3"] = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\event",
            ["sekiro"] = @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\event",
            ["er"] = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event",
        };
        private static readonly Dictionary<string, EMEVD.Game> gameTypes = new Dictionary<string, Game>
        {
            ["ds1"] = Game.DarkSouls1,
            ["bb"] = Game.Bloodborne,
            ["ds2"] = Game.Bloodborne,
            ["ds2scholar"] = Game.Bloodborne,
            ["ds3"] = Game.DarkSouls3,
            ["er"] = Game.Sekiro,
        };

        public static void Run(string[] args)
        {
            string game = args[0];
            string inDir = args[1];
            if (defaultGameDirs.TryGetValue(inDir, out string gameDir)) inDir = gameDir;
            string outDir = args[2];
            // Fancy recompilation
            List<string> emevdPaths = Directory.GetFiles(inDir, "*.emevd").Concat(Directory.GetFiles(inDir, "*.emevd.dcx")).ToList();
            InstructionDocs docs = new InstructionDocs($"{game}-common.emedf.json");
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            Dictionary<string, StringBuilder> contents = new Dictionary<string, StringBuilder>();
            string recordText(string type, string name, Func<object> func)
            {
                if (!contents.TryGetValue(type, out StringBuilder sb))
                {
                    contents[type] = sb = new StringBuilder();
                }
                try
                {
                    object ret = func();
                    if (ret is string text)
                    {
                        sb.AppendLine($"/* ------------------- {name} ------------------- */");
                        sb.AppendLine(text);
                        return text;
                    }
                    else
                    {
                        // Do nothing other than indicate success
                        return "";
                    }
                }
                catch (Exception e)
                {
                    sb.AppendLine($"/* ------------------- {name} ------------------- */");
                    sb.AppendLine($"/* {e} */");
                    Console.WriteLine(name + ": " + e + "\n");
                    return null;
                }
            }
            foreach (string emevdPath in emevdPaths)
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(emevdPath));
                if (game == "ds3" && name.StartsWith("m2")) continue;
                Console.WriteLine("--------------------------" + name);
                EventScripter scripter = new EventScripter(emevdPath, docs);
                string reg1 = recordText("reg1", name, () => scripter.Unpack());
                if (reg1 == null) continue;
                if (args.Contains("reg"))
                {
                    if (recordText("reg2", name + "-compile", () => scripter.Pack(reg1, name)) != null)
                    {
                        recordText("reg2", name, () => scripter.Unpack());
                    }
                }
                if (args.Contains("fancy"))
                {
                    EventCFG.CFGOptions options = args.Contains("min") ? EventCFG.CFGOptions.GetMin() : EventCFG.CFGOptions.GetDefault();
                    options.FailWarnings = true;
                    scripter = new EventScripter(emevdPath, docs);
                    FancyEventScripter fes = new FancyEventScripter(scripter, docs, options);
                    if (args.Contains("unit"))
                    {
                        string testCases = Resource.Text("test.js");
                        if (args.Contains("local"))
                        {
                            testCases = File.ReadAllText("test.js");
                        }
                        fes.Pack(testCases, "test.js");
                        string packUnit = scripter.Unpack();
                        Console.WriteLine(packUnit);
                        string repackUnit = fes.Repack(testCases);
                        if (!args.Contains("silent"))
                        {
                            Console.WriteLine(repackUnit);
                        }
                        if (args.Contains("validate"))
                        {
                            fes.Pack(repackUnit, "test_repack.js");
                            string packUnit2 = scripter.Unpack();
                            Console.WriteLine($"Repack matched: {packUnit == packUnit2}");
                            File.WriteAllText($@"{outDir}\test_reg3.js", packUnit);
                            File.WriteAllText($@"{outDir}\test_reg4.js", packUnit2);
                        }
                        return;
                    }
                    if (args.Contains("repackreg"))
                    {
                        recordText("fancy3", name, () => fes.Repack(reg1));
                    }
                    if (!args.Contains("repack") && !args.Contains("pack") && !args.Contains("fancy")) continue;
                    string fancy1 = recordText("fancy1", name, () => fes.Unpack());
                    if (fancy1 != null)
                    {
                        if (args.Contains("repack"))
                        {
                            recordText("fancy2", name, () => fes.Repack(fancy1));
                        }
                        if (args.Contains("pack"))
                        {
                            if (recordText("reg3", name + "-compile", () => fes.Pack(fancy1, name)) != null)
                            {
                                recordText("reg3", name, () => scripter.Unpack());
                            }
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, StringBuilder> entry in contents)
            {
                using (TextWriter writer = File.CreateText($@"{outDir}\{game}_{entry.Key}.js"))
                {
                    writer.Write(entry.Value);
                }
            }
        }
    }
}
