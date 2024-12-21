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

        public static void CommandLine(IEnumerable<string> args)
        {
#if DEBUG
            Console.WriteLine($"### {string.Join(" ", args)}");
#endif
            // Hacky decompile interface for basic dumps
            List<string> expectedArgs = new() { "game", "indir", "outdir" };
            Dictionary<string, string> flagArgs = new()
            {
                ["compile"] = "Process all JS files in the directory.",
                ["decompile"] = "Process all emevd files in the directory.",
                ["repack"] = "With -compile, output only JS files converted to MattScript and typed inits.",
                ["reparse"] = "With -compile, output only JS files reparsed and formatted with no other changes.",
                ["force"] = "Overwrite existing files.",
                ["nofancy"] = "Disallow MattScript for both compile and decompile. Required for DS2.",
                ["noinit"] = "Disallow analyzing or outputting typed inits.",
                ["tolerant"] = "Ignore some errors and process the next file instead of exiting immediately.",
                ["ptde"] = "With -compile, limit usable condition groups for DS1 PTDE.",
                ["incremental"] = "Update init data as files are processed, needed for repack which depends on common_func.",
                ["silent"] = "Suppress output aside from errors and warnings.",
            };
            if (args.Contains("-help"))
            {
                Console.WriteLine(
@$"Usage: DarkScript3.exe /cmd [mode] [args...]

Mode is either -compile or -decompile.
Warning: The command line interface may have results which mismatch the GUI editor.

These arguments are supported:
-game GAME
    Processes the files using Resources/GAME-common.emedf.json,
    such as ds1, ds2, ds2scholar, bb, ds3, sekiro, er, ac6.
-indir DIR
    Reads all files from the given directory.
-outdir DIR
    Outputs all files to the given directory. This may be the same directory as -indir.");
                foreach ((string arg, string desc) in flagArgs)
                {
                    Console.WriteLine($"-{arg}");
                    Console.WriteLine($"    {desc}");
                }
                Console.WriteLine();
                return;
            }
            Dictionary<string, string> argDict = expectedArgs.ToDictionary(k => k, k => "");
            Dictionary<string, bool> flags = flagArgs.Keys.ToDictionary(k => k, k => false);

            string showArgs(IEnumerable<string> names) => string.Join(" ", names.Select(k => $"-{k}"));
            string expectArg = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    if (expectArg != null)
                    {
                        throw new ArgumentException($"No value given for -{expectArg}. Use -help for usage info.");
                    }
                    string argName = arg.Substring(1);
                    if (flags.ContainsKey(argName))
                    {
                        flags[argName] = true;
                        continue;
                    }
                    if (!argDict.ContainsKey(argName))
                    {
                        throw new ArgumentException($"Unknown arg -{argName}. Use -help for usage info.");
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
                    throw new ArgumentException($"Unexpected argument {arg}. Use -help for usage info.");
                }
            }
            if (expectArg != null)
            {
                throw new ArgumentException($"No value given for -{expectArg}. Use -help for usage info.");
            }

            List<string> emptyArgs = argDict.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToList();
            if (emptyArgs.Count > 0)
            {
                throw new ArgumentException($"Missing args {showArgs(emptyArgs)}. Use -help for usage info.");
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
            bool silent = flags["silent"];

            InstructionDocs docs = new InstructionDocs($"{game}-common.emedf.json");
            EventCFG.CFGOptions options = EventCFG.CFGOptions.GetDefault();
            if (flags["ptde"])
            {
                options.RestrictConditionGroupCount = true;
            }
            InitData initData = new InitData() { BaseDir = inDir };
            bool skipExisting(string outPath)
            {
                if (File.Exists(outPath) && !flags["force"])
                {
                    if (!silent) Console.WriteLine($"{outPath} already exists, skipping it (use -force to override)");
                    return true;
                }
                return false;
            }
            string getSource(HeaderData headerData, string code, bool wasTrimmed)
            {
                StringBuilder sb = new StringBuilder();
                headerData.Write(sb, docs);
                if (wasTrimmed && code.Length > 0)
                {
                    // Text processing for repack is slightly different from in-editor but this seems close enough.
                    sb.AppendLine(code);
                }
                else
                {
                    sb.Append(code);
                }
                return sb.ToString();
            }
            string filter = null;
            int errors = 0;
            if (flags["decompile"])
            {
                List<string> emevdPaths = Directory.GetFiles(inDir, "*.emevd").Concat(Directory.GetFiles(inDir, "*.emevd.dcx")).ToList();
                foreach (string emevdPath in emevdPaths)
                {
                    if (game == "ds3" && Path.GetFileName(emevdPath).StartsWith("m2")) continue;
                    if (filter != null && InitData.GetEmevdName(emevdPath) != filter) continue;
                    EventScripter scripter = new EventScripter(emevdPath, docs);
                    FancyEventScripter fes = flags["nofancy"] ? null : new FancyEventScripter(scripter, docs, options);
                    InitData.Links links = flags["noinit"] ? null : scripter.LoadLinks(initData);
                    string outPath = Path.Combine(outDir, Path.GetFileName(emevdPath) + ".js");

                    if (!silent) Console.WriteLine($"Decompiling {emevdPath} -> {outPath}");

                    if (skipExisting(outPath))
                    {
                        continue;
                    }
                    try
                    {
                        string output;
                        if (fes == null)
                        {
                            output = scripter.Unpack(links);
                        }
                        else
                        {
                            output = fes.Unpack(links);
                        }
                        HeaderData headerData = HeaderData.Create(scripter, docs, new());
                        File.WriteAllText(outPath, getSource(headerData, output, wasTrimmed: false));
                    }
                    catch (Exception ex) when (flags["tolerant"])
                    {
                        Console.WriteLine($"Compiling {emevdPath} -> {ex}");
                        errors++;
                    }
                    if (flags["incremental"])
                    {
                        scripter.ForceUpdateLinks(links);
                    }
                }
            }
            else
            {
                List<string> jsPaths = Directory.GetFiles(inDir, "*.emevd.js").Concat(Directory.GetFiles(inDir, "*.emevd.dcx.js")).ToList();
                foreach (string jsPath in jsPaths)
                {
                    if (filter != null && InitData.GetEmevdName(jsPath) != filter) continue;
                    string code = File.ReadAllText(jsPath);
                    if (!HeaderData.Read(code, out HeaderData headerData))
                    {
                        throw new Exception($"{jsPath} is missing valid header data");
                    }
                    code = HeaderData.Trim(code);
                    // Does this require setting loadPath? Or matter at all?
                    string emevdPath = jsPath.Replace(".js", "");
                    EventScripter scripter = new EventScripter(emevdPath, docs, headerData.CreateEmevd());
                    FancyEventScripter fes = flags["nofancy"] ? null : new FancyEventScripter(scripter, docs, options);
                    InitData.Links links = flags["noinit"] ? null : scripter.LoadLinks(initData);
                    string outPath = Path.Combine(outDir, Path.GetFileName(emevdPath));
                    FancyJSCompiler.CompileOutput compileOutput = null;
                    try
                    {
                        if (flags["repack"] || flags["reparse"])
                        {
                            if (fes == null)
                            {
                                throw new Exception("-nofancy is incompatible with -repack and -reparse");
                            }
                            outPath += ".js";
                            if (skipExisting(outPath))
                            {
                                continue;
                            }
                            // Note jsPath and outPath may match. This would require -force, so allow it probably.
                            if (!silent) Console.WriteLine($"Rewriting {jsPath} -> {outPath}");
                            string output;
                            if (flags["repack"])
                            {
                                output = fes.Repack(code, links, out compileOutput);
                                if (links != null)
                                {
                                    // Normally not set in repack as it's tentative
                                    links.Main = compileOutput.MainInit;
                                }
                            }
                            else
                            {
                                output = fes.Pass(code, out compileOutput);
                            }
                            File.WriteAllText(outPath, getSource(headerData, output, wasTrimmed: true));
                        }
                        else
                        {
                            if (skipExisting(outPath))
                            {
                                continue;
                            }
                            if (!silent) Console.WriteLine($"Compiling {jsPath} -> {outPath}");
                            EMEVD output;
                            if (fes == null)
                            {
                                output = scripter.Pack(code, links);
                            }
                            else
                            {
                                output = fes.Pack(code, links, out compileOutput);
                            }
                            output.Write(outPath);
                        }
                        if (compileOutput != null)
                        {
                            // This ignores pack-only warnings, but those should usually show up during repack.
                            int warns = compileOutput.Warnings.Count;
                            if (warns > 0)
                            {
                                Console.WriteLine($"Compiling {jsPath} -> {warns} warning{(warns == 1 ? "" : "s")}");
                                foreach (ScriptAst.CompileError warn in compileOutput.Warnings)
                                {
                                    Console.WriteLine(warn.Message);
                                }
                            }
                        }
                    }
                    catch (Exception ex) when (flags["tolerant"])
                    {
                        Console.WriteLine($"Compiling {jsPath} -> {ex}");
                        errors++;
                    }
                    if (flags["incremental"])
                    {
                        scripter.ForceUpdateLinks(links);
                    }
                }
            }
            if (errors > 0)
            {
                throw new Exception($"{errors} error{(errors == 1 ? "" : "s")} encountered");
            }
        }

        // Flags should exclude modes and value flags
        private static void RecompileTest(string game, string outJs, string outEmevd, List<string> flags)
        {
            // Dump for diffing
            List<string> outArgs = flags.ToList();
            outArgs.AddRange(new[] { "-compile", "-game", game, "-indir", outJs, "-outdir", outEmevd });
            CommandLine(outArgs);
            outArgs = new() { "-decompile", "-game", game, "-indir", outEmevd, "-outdir", outEmevd, "-nofancy", "-noinit" };
            if (flags.Contains("-force"))
            {
                outArgs.Add("-force");
            }
            CommandLine(outArgs);
        }

        // Flags should exclude modes and value flags
        private static void DecompileTest(string game, string emevdDir, string outDir, List<string> flags)
        {
            // Generates 6 directories: [regular, fancy, fancy repack] * [decompiled js, recompiled emevd]
            foreach (string type in new[] { "reg", "fancy", "repack" })
            {
                string outJs = $"{outDir}_{type}";
                List<string> outArgs = flags.ToList();
                outArgs.AddRange(new[] { "-game", game, "-outdir", outJs });
                if (type == "reg")
                {
                    outArgs.AddRange(new[] { "-decompile", "-nofancy", "-indir", emevdDir });
                    CommandLine(outArgs);
                }
                else if (type == "fancy")
                {
                    outArgs.AddRange(new[] { "-decompile", "-indir", emevdDir });
                    CommandLine(outArgs);
                }
                else if (type == "repack")
                {
                    outArgs.AddRange(new[] { "-compile", "-repack", "-indir", $"{outDir}_fancy" });
                    CommandLine(outArgs);
                }
                RecompileTest(game, outJs, outJs + "_out", flags);
                if (flags.Contains("-nofancy"))
                {
                    break;
                }
            }
        }

        private static void RepackTest(string game, string inDir, string outDir, List<string> flags)
        {
            foreach (string type in new[] { "reg", "fancy", "repack" })
            {
                string inJs = $"{inDir}_{type}";
                string outJs = $"{outDir}_{type}";
                List<string> outArgs = flags.ToList();
                outArgs.AddRange(new[] { "-game", game, "-outdir", outJs });
                outArgs.AddRange(new[] { "-incremental", "-compile", "-repack", "-indir", inJs });
                CommandLine(outArgs);
                // These are source tests so recompilation should be fine to skip
            }
        }

        private static void InitTest(string game, string emevdDir, string outDir, List<string> flags)
        {
            DecompileTest(game, emevdDir, $"{outDir}/new", flags);
            if (game.StartsWith("ds2")) return;
            // No init data, should match old scripts (ignoring emeld txt)
            List<string> initlessFlags = flags.ToList();
            initlessFlags.Add("-noinit");
            DecompileTest(game, emevdDir, $"{outDir}/initless", initlessFlags);
            // Only using self-init data
            string selfDir = $"{outDir}/commonless_in";
            Directory.CreateDirectory(selfDir);
            foreach (string path in Directory.GetFiles(emevdDir))
            {
                string outPath = Path.Combine(selfDir, Path.GetFileName(path));
                if (!File.Exists(outPath))
                {
                    File.Copy(path, outPath);
                }
            }
            bool hasCommon = !game.StartsWith("ds1");
            if (hasCommon)
            {
                string commonFile = game == "bb" ? "common" : "common_func";
                string commonPath = $"{outDir}/initless_reg/{commonFile}.emevd.dcx.js";
                if (File.Exists(commonPath))
                {
                    File.Copy(commonPath, Path.Combine(selfDir, Path.GetFileName(commonPath)), true);
                }
                // Note this does output a converted common_func, so any repack will fully recover it
                DecompileTest(game, selfDir, $"{outDir}/commonless", flags);
            }
            // Init repack tests
            RepackTest(game, $"{outDir}/initless", $"{outDir}/repacknew", flags);
            if (hasCommon)
            {
                RepackTest(game, $"{outDir}/commonless", $"{outDir}/repackcommonless", flags);
            }
            // Migration tests
            if (Directory.Exists(outDir + "/old_reg"))
            {
                foreach (string type in new[] { "reg", "fancy", "repack" })
                {
                    // Test compile doesn't fail
                    RecompileTest(game, $"{outDir}/old_{type}", $"{outDir}/newold_{type}_out", flags);
                }
                RepackTest(game, $"{outDir}/old", $"{outDir}/repackold", flags);
                // Also test initless doesn't fail
                RepackTest(game, $"{outDir}/old", $"{outDir}/repackoldinitless", initlessFlags);
            }
        }

        public static void Run(string[] args)
        {
            string game = args[0];
            string docGame = game == "ds1r" ? "ds1" : game;
            string inDir = args[1];
            if (defaultGameDirs.TryGetValue(inDir, out string gameDir)) inDir = gameDir;
            string outDir = args[2];
            if (args.Contains("dumpall"))
            {
                List<string> cmdArgs = new();
                if (game == "ds1") cmdArgs.Add("-ptde");
                else if (game.StartsWith("ds2")) cmdArgs.Add("-nofancy");
                cmdArgs.Add("-force");
                InitTest(docGame, inDir, $"{outDir}/{game}", cmdArgs);
                return;
            }
            // Fancy recompilation
            string pat = "*";
            // pat = "m21_00_00_00";
            List<string> emevdPaths = Directory.GetFiles(inDir, $"{pat}.emevd").Concat(Directory.GetFiles(inDir, $"{pat}.emevd.dcx")).ToList();
            InstructionDocs docs = new InstructionDocs($"{docGame}-common.emedf.json");
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
            InitData initData = new InitData { BaseDir = inDir };
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
                    // if (!data.SequenceEqual(original)) throw new Exception($"Mismatched header:\n{val}");
                }
                InitData.Links links = scripter.LoadLinks(initData);
                string reg1 = recordText("reg1", name, () => scripter.Unpack(links));
                if (reg1 == null) continue;
                if (args.Contains("reg"))
                {
                    if (recordText("reg2", name + "-compile", () => scripter.Pack(reg1, links)) != null)
                    {
                        recordText("reg2", name, () => scripter.Unpack(links));
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

                        fes.Pack(testCases, links);
                        string packUnit = scripter.Unpack(links);
                        Console.WriteLine(packUnit);
                        string repackUnit = fes.Repack(testCases, links);
                        if (!args.Contains("silent"))
                        {
                            Console.WriteLine(repackUnit);
                        }
                        if (args.Contains("validate"))
                        {
                            fes.Pack(repackUnit, links);
                            string packUnit2 = scripter.Unpack(links);
                            Console.WriteLine($"Repack matched: {packUnit == packUnit2}");
                            File.WriteAllText($@"{outDir}\test_reg3.js", packUnit);
                            File.WriteAllText($@"{outDir}\test_reg4.js", packUnit2);
                        }
                        return;
                    }
                    if (args.Contains("repackreg"))
                    {
                        recordText("fancy3", name, () => fes.Repack(reg1, links));
                    }
                    if (!args.Contains("repack") && !args.Contains("pack") && !args.Contains("fancy")) continue;
                    string fancy1 = recordText("fancy1", name, () => fes.Unpack(links));
                    if (fancy1 != null)
                    {
                        if (args.Contains("repack"))
                        {
                            recordText("fancy2", name, () => fes.Repack(fancy1, links));
                        }
                        if (args.Contains("pack"))
                        {
                            if (recordText("reg3", name + "-compile", () => fes.Pack(fancy1, links)) != null)
                            {
                                recordText("reg3", name, () => scripter.Unpack(links));
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
