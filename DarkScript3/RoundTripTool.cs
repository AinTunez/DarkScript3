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
    /// 
    /// Also hacky decompile command line interface.
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
            ["ac6"] = @"C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event",
        };
        private static readonly Dictionary<string, Game> gameTypes = new Dictionary<string, Game>
        {
            ["ds1"] = Game.DarkSouls1,
            ["bb"] = Game.Bloodborne,
            ["ds2"] = Game.Bloodborne,
            ["ds2scholar"] = Game.Bloodborne,
            ["ds3"] = Game.DarkSouls3,
            ["er"] = Game.Sekiro,
            ["ac6"] = Game.Sekiro,
        };

        private static readonly bool AC6Test = false;

        public static void Decompile(string[] args)
        {
            // Hacky decompile interface for basic dumps
            // TODO: Add more options in the future, also find a way to add headers here
            List<string> expectedArgs = new List<string> { "game", "indir", "outdir" };
            Dictionary<string, string> argDict = expectedArgs.ToDictionary(k => k, k => "");


            string showArgs(IEnumerable<string> names) => string.Join(" ", names.Select(k => $"-{k}"));
            string expectArg = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    if (expectArg != null)
                    {
                        throw new ArgumentException($"No value given for -{expectArg}, found {arg} instead");
                    }
                    string argName = arg.Substring(1);
                    if (argName == "decompile")
                    {
                        continue;
                    }
                    if (!argDict.ContainsKey(argName))
                    {
                        throw new ArgumentException($"Unknown arg -{argName}, expected one of {showArgs(expectedArgs)}");
                    }
                    expectArg = argName;
                }
                else if (expectArg != null)
                {
                    argDict[expectArg] = arg;
                    expectArg = null;
                }
                else
                {
                    throw new ArgumentException($"Unexpected argument {arg}");
                }
            }
            if (expectArg != null)
            {
                throw new ArgumentException($"No value given for -{expectArg}");
            }

            if (AC6Test)
            {
                argDict["indir"] = defaultGameDirs[argDict["game"]];
            }

            List<string> emptyArgs = argDict.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToList();
            if (emptyArgs.Count > 0)
            {
                throw new ArgumentException($"Missing args {showArgs(emptyArgs)}");
            }
            string game = argDict["game"];
            string inDir = argDict["indir"];
            string outDir = argDict["outdir"];
            if (!Directory.Exists(inDir))
            {
                throw new DirectoryNotFoundException($"Directory does not exist: {inDir}");
            }
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }
            InstructionDocs docs = new InstructionDocs($"{game}-common.emedf.json");
            if (AC6Test)
            {
                docs.DOC.DarkScript = null;
                docs.DOC.WriteFile("unkac6-common.emedf.json");
            }
            List<string> emevdPaths = Directory.GetFiles(inDir, "*.emevd").Concat(Directory.GetFiles(inDir, "*.emevd.dcx")).ToList();
            foreach (string emevdPath in emevdPaths)
            {
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(emevdPath));
                if (game == "ds3" && name.StartsWith("m2")) continue;
                EventCFG.CFGOptions options = EventCFG.CFGOptions.GetDefault();
                options.FailWarnings = true;
                EventScripter scripter = new EventScripter(emevdPath, docs);
                // TODO: Work for AC6
                FancyEventScripter fes = new FancyEventScripter(scripter, docs, options);
                string outPath = Path.Combine(outDir, Path.GetFileName(emevdPath) + ".js");

                // TODO could add arg for this but probably unnecessary
                if (File.Exists(outPath) && false)
                {
                    Console.WriteLine($"{emevdPath} -> {outPath} already exists, skipping it");
                    continue;
                }
                try
                {
                    string output = fes.Unpack();
                    File.WriteAllText(outPath, output);
                    Console.WriteLine($"{emevdPath} -> wrote {outPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{emevdPath} -> {ex}");
                }
            }
        }

        public static void Run(string[] args)
        {
            string game = args[0];
            string inDir = args[1];
            if (defaultGameDirs.TryGetValue(inDir, out string gameDir)) inDir = gameDir;
            string outDir = args[2];
            // Fancy recompilation
            string pat = "*";
            // pat = "m21_00_00_00";
            List<string> emevdPaths = Directory.GetFiles(inDir, $"{pat}.emevd").Concat(Directory.GetFiles(inDir, $"{pat}.emevd.dcx")).ToList();
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
            bool testHeader = true;
            foreach (string emevdPath in emevdPaths)
            {
                if (args.Contains("undcx"))
                {
                    byte[] f = DCX.Decompress(emevdPath);
                    File.WriteAllBytes($@"{inDir}\dcx\{Path.GetFileNameWithoutExtension(emevdPath)}", f);
                }
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(emevdPath));
                if (game == "ds3" && name.StartsWith("m2")) continue;
                Console.WriteLine($"-------------------------- {game} {name}");
                EventScripter scripter = new EventScripter(emevdPath, docs);
                if (testHeader)
                {
                    bool ds2 = game.StartsWith("ds2");
                    byte[] data = scripter.EVD.StringData;
                    string val = HeaderData.Escape(data, ds2);
                    byte[] original = HeaderData.Unescape(val, ds2);
                    if (!data.SequenceEqual(original)) throw new Exception($"Mismatched header:\n{val}");
                }
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
