using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SoulsFormats;
using static SoulsFormats.EMEVD.Instruction;

namespace DarkScript3
{
    /// <summary>
    /// Hacky pile of scripts for testing condition group behavior edge cases.
    /// </summary>
    public class CondTestingTool
    {
        // Static data
        private EMEDF doc;
        private Dictionary<string, (int, int)> docByName;
        private IDictionary<int, string> testItemLots;
        private List<int> testItemLotOrder;

        // Added in tests
        private EMEVD emevd;
        private int i = 1;
        private int evBase = 5950;
        private int falseFlag = 11305405;
        private int trueFlag = 11305406;
        private int flagBase = 11305410;
        private List<int> addEvents = new List<int>();

        public void Run(string[] args)
        {
            // This file ended up being more than just cond testing, but hack pile of one-time
            // scripts still basically applies to these as well.
            if (args.Contains("ac6"))
            {
                // DumpUnknown();
                // DumpMixedEmedf(args);
                CombineMixedEmedf(args);
            }
            else if (args.Contains("nr"))
            {
                DumpUnknown();
            }
            else if (args.Contains("gen"))
            {
                // DumpEldenNew(args);
                DumpWithDefaults(args);
            }
            else if (args.Contains("validate"))
            {
                ValidateEmedf(args);
            }
            else if (args.Contains("args"))
            {
                AnalyzeArgs(args);
            }
            else if (args.Contains("types"))
            {
                DumpTypes(args);
            }
            else
            {
                // RunEldenTests(args);
                RunNightreignTests(args);
            }
        }

        private static readonly Dictionary<string, int> Types = new Dictionary<string, int>()
        {
            ["byte"] = 0,
            ["ushort"] = 1,
            ["uint"] = 2,
            ["sbyte"] = 3,
            ["short"] = 4,
            ["int"] = 5,
            ["float"] = 6,
        };
        private static readonly Dictionary<int, string> RevTypes = Types.ToDictionary(e => e.Value, e => e.Key);
        private static readonly Dictionary<string, int> Widths = new()
        {
            ["byte"] = 1,
            ["ushort"] = 2,
            ["uint"] = 4,
            ["sbyte"] = 1,
            ["short"] = 2,
            ["int"] = 4,
            ["float"] = 4,
        };

        public void DumpTypes(string[] args)
        {
            InstructionDocs baseDocs = new InstructionDocs("er-common.emedf.json");
            InstructionDocs docs = new InstructionDocs("nr-common.emedf.json");
            Dictionary<string, List<string>> docCats = docs.DOC?.DarkScript?.MetaAliases ?? baseDocs.DOC.DarkScript.MetaAliases;
            List<string> cats = docCats.Keys.ToList();
            Dictionary<string, string> revCats = docCats.SelectMany(e => e.Value.Select(v => (v, e.Key))).ToDictionary(e => e.Item1, e => e.Item2);
            List<EMEDF.DarkScriptType> metaTypes = docs.DOC?.DarkScript?.MetaTypes ?? baseDocs.DOC.DarkScript.MetaTypes;
            List<string> metaNames = metaTypes.SelectMany(t => t.Name != null ? new[] { t.Name } : ((IEnumerable<string>)t.MultiNames ?? Array.Empty<string>())).ToList();
            SortedDictionary<(string, string), List<string>> cmdsByType = new SortedDictionary<(string, string), List<string>>();
            foreach (EMEDF.ClassDoc bank in docs.DOC.Classes.OrderBy(i => i.Index))
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions.OrderBy(i => i.Index))
                {
                    string id = InstructionDocs.FormatInstructionID(bank.Index, instr.Index);
                    string name = instr.DisplayName;
                    foreach (EMEDF.ArgDoc arg in instr.Arguments)
                    {
                        if (arg.Type == 8) continue;
                        string type = RevTypes[(int)arg.Type];
                        if (arg.EnumName == null && type != "float")
                        {
                            var key = (arg.Name, type);
                            if (!cmdsByType.TryGetValue(key, out List<string> cmds))
                            {
                                cmdsByType[key] = cmds = new List<string>();
                            }
                            cmds.Add(name);
                        }
                    }
                }
            }
            List<(string, string)> sortedIds = new();
            bool standardPart(string a) => a.Length > 1 && !a.StartsWith('(') && !a.EndsWith(')');
            foreach (bool useId in new[] { true, false })
            {
                foreach (var entry in cmdsByType)
                {
                    (string argName, string type) = entry.Key;
                    if (argName.Contains("ID") != useId) continue;
                    if (args.Contains("print"))
                    {
                        Console.WriteLine($"{type} {argName}: {string.Join(", ", entry.Value)}");
                    }
                    if (useId)
                    {
                        string sortName = string.Join(" ", argName.Split(' ').Where(standardPart).Reverse());
                        sortedIds.Add((sortName, argName));
                    }
                }
                if (args.Contains("print")) Console.WriteLine();
            }
            sortedIds.Sort();
            List<string> allIds = sortedIds.Select(a => a.Item2).ToList();
            List<string> catIds = cats.ToList(); // allIds.Intersect(cats).ToList();
            if (!catIds.Contains("Other")) catIds.Add("Other");
            Dictionary<string, List<string>> aliases = catIds.ToDictionary(c => c, _ => new List<string>());
            if (args.Contains("aliases"))
            {
                foreach (string id in allIds)
                {
                    if (catIds.Contains(id)) continue;
                    // if (metaNames.Contains(id)) continue;
                    string standard = string.Join(" ", id.Split(' ').Where(standardPart));
                    if (!revCats.TryGetValue(id, out string cat) || !catIds.Contains(cat))
                    {
                        cat = Enumerable.Reverse(catIds).Where(c => standard.EndsWith(c)).FirstOrDefault("Other");
                    }
                    aliases[cat].Add(id);
                }
                foreach ((string cat, List<string> als) in aliases)
                {
                    Console.WriteLine($"\"{cat}\": [");
                    foreach (string al in als.Distinct())
                    {
                        string suf = metaNames.Contains(al) ? "*" : "";
                        Console.WriteLine($"  \"{al}{suf}\",");
                    }
                    Console.WriteLine("],");
                }
            }
        }

        public void DumpEldenUnknown()
        {
            InstructionDocs docs = new InstructionDocs("er-common.emedf.json");
            foreach (EMEDF.ClassDoc bank in docs.DOC.Classes.OrderBy(i => i.Index))
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions.OrderBy(i => i.Index))
                {
                    string id = InstructionDocs.FormatInstructionID(bank.Index, instr.Index);
                    string name = instr.Name;
                    if (name.ToLowerInvariant().Contains("unknown") || id == "4[15]")
                    {
                        // ID, Name, Notes, Removal, Args
                        if (name.Contains('"')) throw new Exception(name);
                        List<string> cells = new List<string> { id, name };
                        cells.AddRange(instr.Arguments.Select(a =>
                            (a.EnumName != null ? $"{a.EnumName} " : "") + $"{InstructionDocs.TypeString(a.Type)} {a.Name}"));
                        Console.WriteLine(string.Join(",", cells.Select(c => $"\"{c}\"")));
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                }
            }
        }

        private EMEDF.ArgDoc ParseArgType(string text)
        {
            string getType(string prefix)
            {
                foreach (KeyValuePair<string, int> type in Types)
                {
                    if (prefix.StartsWith(type.Key + " ")) return type.Key;
                }
                return null;
            }
            EMEDF.EnumDoc getEnum(string prefix)
            {
                foreach (EMEDF.EnumDoc enumDoc in doc.Enums)
                {
                    if (prefix.StartsWith(enumDoc.Name + " ")) return enumDoc;
                }
                return null;
            }
            string desc = text.Split('=')[0].Trim();
            EMEDF.EnumDoc enumDoc = null;
            string type = getType(desc);
            if (type == null)
            {
                enumDoc = getEnum(desc);
                if (enumDoc == null) throw new Exception($"Can't parse {text}");
                desc = desc.Substring(enumDoc.Name.Length).Trim();
                type = getType(desc);
                if (type == null) throw new Exception($"Can't parse type in {text}");
            }
            desc = desc.Substring(type.Length).Trim();
            EMEDF.ArgDoc argDoc = new EMEDF.ArgDoc
            {
                Name = desc,
                Type = Types[type],
                EnumName = enumDoc?.Name,
                Default = desc == "Number of Target Characters" ? 1 : 0,
                Min = 0,
                Max = 0,
                Increment = 0,
                FormatString = type == "float" ? "%0.3f" : "%d",
            };
            if (argDoc.EnumName == null)
            {
                argDoc.Max = 10000000007;  // Sentinel value, used by FillDefaults
            }
            if (argDoc.Name == "Parameters") argDoc.Vararg = true;
            return argDoc;
        }

        public void DumpEldenNew(ICollection<string> opt)
        {
            doc = EMEDF.ReadFile($"DarkScript3/Resources/er-common.emedf.json");
            string getArgKey(EMEDF.ArgDoc argDoc)
            {
                List<string> parts = new List<string>();
                if (argDoc.EnumName != null) parts.Add(argDoc.EnumName);
                if (!RevTypes.TryGetValue((int)argDoc.Type, out string typeName)) throw new Exception($"Unknown type in {argDoc.Name}: {argDoc.Type}");
                parts.Add(typeName);
                parts.Add(argDoc.Name);
                return string.Join(" ", parts);
            }
            EMEDF.ArgDoc makeDefaults(int min, int max, int inc, int def)
            {
                return new EMEDF.ArgDoc
                {
                    Min = min,
                    Max = max,
                    Increment = inc,
                    Default = def,
                };
            }
            string getMinorDetailDesc(EMEDF.ArgDoc argDoc)
            {
                return $"min {argDoc.Min} max {argDoc.Max} inc {argDoc.Increment} default {argDoc.Default}";
            }
            Dictionary<string, EMEDF.ArgDoc> exampleDocs = new Dictionary<string, EMEDF.ArgDoc>
            {
                ["int Ceremony ID"] = makeDefaults(0, 99, 1, 0),
                ["byte Region ID"] = makeDefaults(0, 99, 1, 0),
                ["byte Index ID"] = makeDefaults(0, 99, 1, 0),
                ["uint NPC Threat Level"] = makeDefaults(0, 99, 1, 0),
                ["uint Min NPC Threat Level"] = makeDefaults(0, 99, 1, 0),
                ["uint Max NPC Threat Level"] = makeDefaults(0, 99, 1, 0),
                ["sbyte Pool Type"] = makeDefaults(0, 1, 1, 0),
                ["int Activity ID"] = makeDefaults(0, 99, 1, 0),
                ["byte Hour"] = makeDefaults(0, 1, 23, 0),
                ["byte Minute"] = makeDefaults(0, 1, 59, 0),
                ["byte Second"] = makeDefaults(0, 1, 59, 0),
            };
            foreach (EMEDF.ClassDoc classDoc in doc.Classes)
            {
                foreach (EMEDF.InstrDoc instrDoc in classDoc.Instructions)
                {
                    foreach (EMEDF.ArgDoc argDoc in instrDoc.Arguments)
                    {
                        string key = getArgKey(argDoc);
                        if (exampleDocs.TryGetValue(key, out EMEDF.ArgDoc old))
                        {
                            string oldDesc = getMinorDetailDesc(old);
                            string newDesc = getMinorDetailDesc(argDoc);
                            if (oldDesc != newDesc)
                            {
                                if (opt.Contains("defaults")) Console.WriteLine($"Different values for {key}: [{oldDesc}] old vs [{newDesc}] new");
                            }
                        }
                        else
                        {
                            exampleDocs[key] = argDoc;
                        }
                    }
                }
            }
            EMEDF.ArgDoc getArg(string text, string debugInfo = "")
            {
                EMEDF.ArgDoc argDoc = ParseArgType(text);
                // It seems enums don't get default values etc.
                if (argDoc.EnumName == null)
                {
                    string argKey = getArgKey(argDoc);
                    EMEDF.ArgDoc old = null;
                    if (!argDoc.Name.StartsWith("Unknown"))
                    {
                        exampleDocs.TryGetValue(argKey, out old);
                        if (old == null && RevTypes[(int)argDoc.Type] == "uint" && argDoc.Name.EndsWith("Entity ID"))
                        {
                            exampleDocs.TryGetValue("uint Target Entity ID", out old);
                        }
                    }
                    if (old == null)
                    {
                        if (opt.Contains("defaults")) Console.WriteLine($"Unknown arg {argKey}{debugInfo}");
                        argDoc.Max = 10000000007;
                    }
                    else
                    {
                        if (opt.Contains("known")) Console.WriteLine($"Known arg {argKey}: {getMinorDetailDesc(old)}");
                        argDoc.Min = old.Min;
                        argDoc.Max = old.Max;
                        argDoc.Increment = old.Increment;
                        argDoc.Default = old.Default;
                    }
                }
                return argDoc;
            }
            foreach (string line in File.ReadAllLines("unknown.tsv"))
            {
                string[] cells = line.Split('\t');
                string cmd = cells[0];
                int bank, id;
                try
                {
                    (bank, id) = InstructionDocs.ParseInstructionID(cmd);
                }
                catch (FormatException)
                {
                    continue;
                }
                string name = cells[1].Trim(new[] { '?', ' ' });
                List<EMEDF.ArgDoc> args = cells.Skip(2)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => getArg(c, $" in {name}"))
                    .ToList();
                EMEDF.InstrDoc instrDoc = doc[bank][id];
                if (instrDoc == null)
                {
                    instrDoc = new EMEDF.InstrDoc
                    {
                        Name = name,
                        Index = id,
                        Arguments = args,
                    };
                    doc[bank].Instructions.Add(instrDoc);
                }
                else
                {
                    if (opt.Contains("changes") && instrDoc.Name != name) Console.WriteLine($"{instrDoc.Name} -> {name}");
                    instrDoc.Name = name;
                    if (instrDoc.Arguments.Count != args.Count)
                    {
                        string err = $"Mismatched arg count for {cmd}: {instrDoc.Arguments.Count} -> {args.Count}";
                        if (opt.Contains("changes"))
                        {
                            Console.WriteLine(err);
                        }
                        if (instrDoc.Arguments.Count < args.Count)
                        {
                            instrDoc.Arguments = instrDoc.Arguments.Concat(args.Skip(instrDoc.Arguments.Count)).ToList();
                        }
                        else throw new Exception(err);
                    }
                    for (int i = 0; i < args.Count; i++)
                    {
                        EMEDF.ArgDoc from = args[i];
                        EMEDF.ArgDoc to = instrDoc.Arguments[i];
                        to.Name = from.Name;
                        to.Type = from.Type;
                        to.EnumName = from.EnumName;
                    }
                }
            }
            foreach (EMEDF.ClassDoc classDoc in doc.Classes)
            {
                classDoc.Instructions = classDoc.Instructions.OrderBy(x => x.Index).ToList();
            }
            if (opt.Contains("dryrun")) return;
            string output = JsonConvert.SerializeObject(doc, Formatting.Indented).Replace("\r\n", "\n");
            File.WriteAllText("new-er-common.emedf.json", output);
        }

        // Standalone default filling after AddUnknownValues
        public void DumpWithDefaults(string[] opt)
        {
            string getArgKey(EMEDF.ArgDoc argDoc)
            {
                List<string> parts = new List<string>();
                if (argDoc.EnumName != null) parts.Add(argDoc.EnumName);
                if (!RevTypes.TryGetValue((int)argDoc.Type, out string typeName)) throw new Exception($"Unknown type in {argDoc.Name}: {argDoc.Type}");
                parts.Add(typeName);
                parts.Add(argDoc.Name);
                return string.Join(" ", parts);
            }
            EMEDF.ArgDoc makeDefaults(int min, int max, int inc = 1, int def = 0)
            {
                return new EMEDF.ArgDoc
                {
                    Min = min,
                    Max = max,
                    Increment = inc,
                    Default = def,
                };
            }
            string getMinorDetailDesc(EMEDF.ArgDoc argDoc)
            {
                return $"min {argDoc.Min} max {argDoc.Max} inc {argDoc.Increment} default {argDoc.Default}";
            }
            Dictionary<string, EMEDF.ArgDoc> exampleDocs = new Dictionary<string, EMEDF.ArgDoc>
            {
                ["short Mission ID"] = makeDefaults(0, 999),
                ["float Souls Multiplier"] = makeDefaults(0, 100000000),
                ["float Target Probability"] = makeDefaults(0, 1, 1, 0),
            };
            // From new name to existing one
            Dictionary<string, string> copyDocs = new()
            {
                ["byte Unused Hours"] = "byte Hours",
                ["byte Unused Minutes"] = "byte Minutes",
                ["byte Unused Seconds"] = "byte Seconds",
                ["float Fade to White Ratio"] = "float Fade to Black Ratio",
                ["float Fade to White Time (s)"] = "float Fade to Black Time (s)",
            };
            EMEDF oldDoc = EMEDF.ReadFile($"DarkScript3/Resources/er-common.emedf.json");
            foreach (EMEDF.ClassDoc classDoc in oldDoc.Classes)
            {
                foreach (EMEDF.InstrDoc instrDoc in classDoc.Instructions)
                {
                    foreach (EMEDF.ArgDoc argDoc in instrDoc.Arguments)
                    {
                        string key = getArgKey(argDoc);
                        if (exampleDocs.TryGetValue(key, out EMEDF.ArgDoc old))
                        {
                            string oldDesc = getMinorDetailDesc(old);
                            string newDesc = getMinorDetailDesc(argDoc);
                            if (oldDesc != newDesc)
                            {
                                if (opt.Contains("defaults")) Console.WriteLine($"Different values for {key}: [{oldDesc}] old vs [{newDesc}] new");
                            }
                        }
                        else
                        {
                            exampleDocs[key] = argDoc;
                        }
                    }
                }
            }
            void updateArg(EMEDF.ArgDoc argDoc, string debugInfo = "")
            {
                // It seems enums don't get default values etc.
                if (argDoc.EnumName != null)
                {
                    return;
                }
                string argKey = getArgKey(argDoc);
                EMEDF.ArgDoc old = null;
                if (argDoc.Max != 10000000007)
                {
                    // Require sentinel value I guess
                    return;
                }
                if (!argDoc.Name.StartsWith("Unknown"))
                {
                    exampleDocs.TryGetValue(argKey, out old);
                    if (old == null && copyDocs.TryGetValue(argKey, out string oldArgKey))
                    {
                        exampleDocs.TryGetValue(oldArgKey, out old);
                    }
                }
                if (old == null)
                {
                    if (opt.Contains("defaults")) Console.WriteLine($"Unknown arg {argKey}{debugInfo}");
                    string type = RevTypes[(int)argDoc.Type];
                    argDoc.Increment = 1;
                    if (type == "byte" || type == "sbyte")
                    {
                        argDoc.Max = 99;
                    }
                    else if (type == "short" || type == "ushort")
                    {
                        argDoc.Max = 9999;
                    }
                    else if (type == "int" || type == "uint")
                    {
                        argDoc.Max = 2000000000;
                    }
                    else if (type == "float")
                    {
                        argDoc.Max = 100000000;
                    }
                }
                else
                {
                    if (opt.Contains("known")) Console.WriteLine($"Known arg {argKey}: {getMinorDetailDesc(old)}");
                    argDoc.Min = old.Min;
                    argDoc.Max = old.Max;
                    argDoc.Increment = old.Increment;
                    argDoc.Default = old.Default;
                }
            }
            InstructionDocs docs = new InstructionDocs("nr-common.emedf.json");
            doc = docs.DOC;
            foreach (EMEDF.ClassDoc classDoc in doc.Classes)
            {
                foreach (EMEDF.InstrDoc instrDoc in classDoc.Instructions)
                {
                    foreach (EMEDF.ArgDoc argDoc in instrDoc.Arguments)
                    {
                        updateArg(argDoc, $" in {instrDoc.Name}");
                    }
                }
            }
            if (opt.Contains("dryrun")) return;
            string output = JsonConvert.SerializeObject(doc, Formatting.Indented).Replace("\r\n", "\n");
            File.WriteAllText("nr-new-common.emedf.json", output);
        }

        public void RunNightreignTests(string[] args)
        {
            string gameDir = $@"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING NIGHTREIGN\Game\event";
            string modDir = $@"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING NIGHTREIGN\Game\testevent\event";
            foreach (string emevdPath in Directory.GetFiles(modDir, "*.emevd.dcx"))
            {
                File.Delete(emevdPath);
            }
            InstructionDocs docs = new InstructionDocs("nr-common.emedf.json");
            void addEvent(string file, params EMEVD.Event[] events)
            {
                EMEVD emevd = EMEVD.Read($@"{gameDir}\{file}.emevd.dcx");
                EMEVD.Event constructor = emevd.Events[0];
                foreach (EMEVD.Event ev in events)
                {
                    // Reverse order
                    constructor.Instructions.InsertRange(0, new[]
                    {
                        new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, 0 }),
                    });
                    emevd.Events.Add(ev);
                    string modFile = $@"{modDir}\{file}.emevd.dcx";
                    Console.WriteLine(modFile);
                    emevd.Write($@"{modDir}\{file}.emevd.dcx");
                }
            }
            // string file = "m10_00_00_00";
            // runner.Instructions.Add(ParseAdd($"IF Character Has SpEffect (0,10000,501000,1,0,1)"));
            int id = 34905435;
            doc = docs.DOC;
            docByName = doc.Classes.SelectMany(c => c.Instructions.Select(i => (i, (int)c.Index))).ToDictionary(i => i.Item1.Name, i => (i.Item2, (int)i.Item1.Index));
            EMEVD.Event ev = new EMEVD.Event(id++, EMEVD.Event.RestBehaviorType.Default);
            ev.Instructions.AddRange(new[]
            {
                // --- First trigger
                // ParseAdd("WAIT Fixed Time (Seconds) (15)"),
                ParseAdd($"IF Character Has State Info (0,10000,275,1,0,1)"),
                // ParseAdd("Record User Disp Log (11270, 20000, 0, -1)"), // Extra talisman slot
                // --- Action
                // new EMEVD.Instruction(2004, 88, new List<object>{ 10000, 50230 }),
                // new EMEVD.Instruction(2007, 21, new List<object>{ 3000 }),
                // new EMEVD.Instruction(2004, 90, new List<object>{ }),
                // new EMEVD.Instruction(2004, 91, new List<object>{ 10000661, 912000080 }),
                // new EMEVD.Instruction(2003, 14, new List<object> { (byte)60, (byte)43, (byte)38, (byte)00, 1043382200, 0 }),
                // new EMEVD.Instruction(2003, 113, new List<object> { (byte)60, (byte)43, (byte)38, (byte)00, 1043382200, 0 }),
                // new EMEVD.Instruction(2003, 110, new List<object> { 1f }),
                // new EMEVD.Instruction(2003, 111, new List<object> { 100, 20000 }),
                // new EMEVD.Instruction(2003, 113, new List<object> { 19, 0, 2 }),
                // 1044360200
                // new EMEVD.Instruction(2003, 116, new List<object> { 20000 }),
                // new EMEVD.Instruction(2004, 86, new List<object> { }), // Add flask
                ParseAdd($"IF Event Flag (0, 1, 0, 7700)"),
                // --- Second trigger
                // ParseAdd("WAIT Fixed Time (Seconds) (0.1)"),
                // ParseAdd($"IF Character Has State Info (0,10000,275,1,0,1)"),
                ParseAdd("Record User Disp Log (11210, 20000, 0, -1)"), // Condemned has invaded
                // --- Action
                // new EMEVD.Instruction(2004, 92, new List<object>{ (byte)3, 50230 }),
                // new EMEVD.Instruction(2007, 19, new List<object>{ 4010 }),
                // new EMEVD.Instruction(2004, 88, new List<object>{ 10000, 50730 }),
                // new EMEVD.Instruction(2004, 87, new List<object> { }), // Refill flasks?
                // ParseAdd("WAIT Fixed Time (Seconds) (0.1)"),
                // ParseAdd("END Unconditionally (1)")
            });
            if (args.Contains("weather"))
            {
                ev.Instructions.Clear();
                List<int> types = Enumerable.Range(0, 24).ToList();
                types.Add(-1);
                List<int> banners = new() { 82, 80, 81 };  // 0 1 2 -> 3 1 2
                foreach (int type in types)
                {
                    ev.Instructions.AddRange(new[]
                    {
                        ParseAdd($"IF Character Has State Info (0,10000,275,1,0,1)"),
                        ParseAdd($"Change Weather({type}, -1, 1)"),
                        ParseAdd("WAIT Fixed Time (Seconds) (2)"),
                        ParseAdd($"Refill Estus ()"),
                        // ParseAdd("Add Estus Charge ()"),
                    });
                    if (type >= 0)
                    {
                        int banner = banners[type % banners.Count];
                        ev.Instructions.Add(ParseAdd($"Display Text Effect ({banner})"));
                    }
                }
            }

            addEvent("common", ev);

            if (args.Contains("warp"))
            {
                EMEVD.Event rtWarp = new EMEVD.Event(id++, EMEVD.Event.RestBehaviorType.Default);
                // rtWarp.Instructions.Add(ParseAdd("Warp Player (60, 43, 38, 0, 1043382200, 0)"));
                rtWarp.Instructions.Add(ParseAdd("Warp Player (18, 0, 0, 0, 18002200, 0)"));
                addEvent("m10_00_00_00", rtWarp);
            }

            // constructor.Instructions.Insert(0, new EMEVD.Instruction(2003, 66, new List<object> { 0, 7602, 1 }));
            // constructor.Instructions.Insert(0, new EMEVD.Instruction(2003, 14, new List<object> { (byte)60, (byte)43, (byte)38, (byte)00, 1043382200, 0 }));
            // constructor.Instructions.Insert(0, new EMEVD.Instruction(2003, 14, new List<object> { (byte)19, (byte)00, (byte)00, (byte)00, 19002300, 0 }));
        }

        private static readonly DCX.Type quickDcx = DCX.Type.DCX_DFLT_11000_44_9;
        public void RunEldenTests(IList<string> args)
        {
            evBase = 3777000;
            trueFlag = 19002900;
            falseFlag = 19002901;
            flagBase = 19002905;
            InstructionDocs docs = new InstructionDocs("er-common.emedf.json");
            doc = docs.DOC;
            docByName = doc.Classes.SelectMany(c => c.Instructions.Select(i => (i, (int)c.Index))).ToDictionary(i => i.Item1.Name, i => (i.Item2, (int)i.Item1.Index));
            if (args.Contains("remove"))
            {
                RunEldenRemoveTest(args);
            }
            else if (args.Contains("init"))
            {
                RunEldenInitTests(args);
            }
            else
            {
                RunEldenCommonTests(args);
            }
        }

        public void DumpUnknown()
        {
            InstructionDocs docs = new InstructionDocs("er-common.emedf.json");
            doc = docs.DOC;
            SortedSet<(int, int)> used = new();
            Dictionary<(int, int), int> bad = new();
            Dictionary<(int, int), SortedSet<int>> badBytes = new();
            // string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event";
            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING NIGHTREIGN\Game\event";
            foreach (string path in Directory.GetFiles(dir, "*.emevd.dcx"))
            {
                string fileName = Path.GetFileName(path);
                emevd = EMEVD.Read(path);
                foreach (EMEVD.Event e in emevd.Events)
                {
                    Dictionary<EMEVD.Parameter, string> pn = e.Parameters.ToDictionary(p => p, p => $"X{p.SourceStartByte}_{p.ByteCount}");
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        var insId = (ins.Bank, ins.ID);
                        used.Add(insId);
                        try
                        {
                            EMEDF.InstrDoc instrDoc = doc[ins.Bank][ins.ID];
                            List<object> args = docs.UnpackArgsWithParams(
                                ins, i, instrDoc, pn, (argDoc, val) => argDoc.GetDisplayValue(val)).Args;
                        }
                        catch (Exception ex)
                        {
                            int len = ins.ArgData.Length / 4;
                            bad[insId] = len;
                            if (!badBytes.ContainsKey(insId)) badBytes[insId] = new();
                            for (int k = 0; k < ins.ArgData.Length; k++)
                            {
                                if (ins.ArgData[k] != 0)
                                {
                                    badBytes[insId].Add(k);
                                }
                            }
                        }
                    }
                }
            }
            string prev = "";
            foreach (var insId in used)
            {
                (int bank, int id) = insId;
                string insStr = InstructionDocs.FormatInstructionID(bank, id);
                string name = $"XUnknown {insStr}";
                List<string> args;
                if (bad.TryGetValue(insId, out int len))
                {
                    bool init = bank == 2000 && (id == 7 || id == 8);
                    if (init) len = 3;
                    ISet<int> bytes = badBytes[insId];
                    string byteChar(int i) => bytes.Contains(i) ? "X" : "0";
                    string bytesChar(int i) => $"{byteChar(i)}{byteChar(i + 1)}{byteChar(i + 2)}{byteChar(i + 3)}";
                    args = Enumerable.Range(0, len).Select(i => $"uint Unknown{i * 4}_{bytesChar(i * 4)}").ToList();
                    if (bank < 100)
                    {
                        args.RemoveAt(0);
                        int maxByte = bytes.Where(x => x < 4).OrderByDescending(x => x).FirstOrDefault();
                        for (int i = maxByte; i >= 0; i--)
                        {
                            string s = i == 0 ? "Condition Group sbyte Result Condition Group" : $"sbyte Unknown{i}_{byteChar(i)}";
                            args.Insert(0, s);
                        }
                    }
                    else if (init) args[2] = "uint Parameters";
                }
                else
                {
                    EMEDF.InstrDoc instrDoc = doc[bank][id];
                    name = instrDoc.Name;
                    args = instrDoc.Arguments.Select(a => (a.EnumName == null ? "" : $"{a.EnumName} ") + $"{RevTypes[(int)a.Type]} {a.Name}").ToList();
                }
                string desc = $"{name}{string.Join("", args.Select(s => $", {s}"))}";
                if (desc.Contains("XUnknown"))
                {
                    if (!prev.Contains("XUnknown")) Console.WriteLine();
                    string hexId = $">0x{id:x}";
                    Console.WriteLine($">{hexId.PadRight(insStr.Length)}, {desc}");
                    Console.WriteLine($">{insStr}, {desc}");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine($"{insStr}, {desc}");
                }
                prev = desc;
            }
        }

        public void CombineMixedEmedf(string[] args)
        {
            List<InstructionDocs> emedfs = new();
            foreach (string arg in args)
            {
                if (arg.EndsWith("json"))
                {
                    emedfs.Add(new InstructionDocs(arg));
                }
            }
            string csv = args.Where(a => a.EndsWith(".csv")).First();
            SortedSet<(int, int)> used = GetUsed(@"C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event");
            SortedSet<(int, int)> docced = new();
            Dictionary<string, string> enumMap = new();
            doc = emedfs[0].DOC;
            foreach (string line in File.ReadAllLines(csv))
            {
                string[] cells = line.Split(",").Select(s => s.Trim()).ToArray();
                string cat = cells[0];
                if (cat != "new" && cat != "new!!") continue;
                string cmd = cells[1];
                int bank, id;
                try
                {
                    (bank, id) = InstructionDocs.ParseInstructionID(cmd);
                }
                catch (FormatException)
                {
                    continue;
                }
                EMEDF.InstrDoc instr = doc[bank]?[id];
                if (instr == null) throw new Exception($"Missing {cmd}");
                docced.Add((bank, id));
                string name = cells[2].Trim();
                List<string> argStrs = cells.Skip(3).ToList();
                if (instr.Arguments.Count != argStrs.Count) throw new Exception($"Arg length mismatch {cmd}");
                for (int i = 0; i < argStrs.Count; i++)
                {
                    string argStr = argStrs[i];
                    EMEDF.ArgDoc argDoc = instr.Arguments[i];
                    string type = RevTypes[(int)argDoc.Type];
                    int typeIndex = argStr.IndexOf(type + " ");
                    if (typeIndex == -1) throw new Exception($"Arg {cmd} {i} is not {type}");
                    string enumName = typeIndex == 0 ? null : argStr.Substring(0, typeIndex).Trim();
                    string argName = argStr.Substring(typeIndex + type.Length).Trim();
                    if ((enumName == null) != (argDoc.EnumName == null)) throw new Exception($"Arg {cmd} {i} enum mismatch");
                    if (enumName != null)
                    {
                        if (enumMap.TryGetValue(argDoc.EnumName, out string existName) && existName != enumName)
                        {
                            throw new Exception($"Enum {argDoc.EnumName} -> {existName} // {enumName}");
                        }
                        enumMap[argDoc.EnumName] = enumName;
                        argDoc.EnumName = enumName;
                    }
                    argDoc.Name = argName;
                }
                instr.Name = name;
            }
            doc.Enums = doc.Enums.Where(enumDoc =>
            {
                if (!enumMap.TryGetValue(enumDoc.Name, out string name)) return false;
                bool changed = false;
                for (int i = 2; i < 4; i++)
                {
                    EMEDF.EnumDoc enumDoc2 = emedfs[i].DOC.Enums.Where(n => n.Name == name).FirstOrDefault();
                    if (enumDoc2 == null) continue;
                    if (enumDoc.Values.Keys.SequenceEqual(enumDoc2.Values.Keys))
                    {
                        changed = true;
                        enumDoc.Values = enumDoc2.Values;
                        break;
                    }
                }
                if (!changed && args.Contains("debug")) Console.WriteLine($"Missing {enumDoc.Name} -> {name}");
                enumDoc.Name = name;
                enumDoc.Values = new(enumDoc.Values.OrderBy(e => int.Parse(e.Key)));
                return true;
            }).ToList();
            foreach (var g in enumMap.GroupBy(e => e.Value))
            {
                if (g.Count() > 1 && args.Contains("debug"))
                {
                    Console.WriteLine($"{g.Key}: {string.Join("//", g.Select(e => e.Key))}");
                }
            }
            doc.Classes.RemoveAll(classDoc =>
            {
                classDoc.Instructions.RemoveAll(instrDoc =>
                {
                    (int, int) insId = ((int)classDoc.Index, (int)instrDoc.Index);
                    if (!docced.Contains(insId))
                    {
                        if (used.Contains(insId)) throw new Exception($"{insId} used");
                        return true;
                    }
                    return false;
                });
                EMEDF.ClassDoc classDoc2 = emedfs[2].DOC[(int)classDoc.Index];
                if (classDoc2 != null) classDoc.Name = classDoc2.Name;
                return classDoc.Instructions.Count == 0;
            });
            doc.WriteFile("ac6-common.emedf.json");
        }

        private SortedSet<(int, int)> GetUsed(string emevdDir)
        {
            SortedSet<(int, int)> used = new();
            foreach (string path in Directory.GetFiles(emevdDir, "*.emevd.dcx"))
            {
                string fileName = Path.GetFileName(path);
                emevd = EMEVD.Read(path);
                foreach (EMEVD.Event e in emevd.Events)
                {
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        var insId = (ins.Bank, ins.ID);
                        used.Add(insId);
                    }
                }
            }
            return used;
        }

        public void DumpMixedEmedf(string[] args)
        {
            List<InstructionDocs> emedfs = new();
            foreach (string arg in args)
            {
                if (arg.EndsWith("json"))
                {
                    emedfs.Add(new InstructionDocs(arg));
                }
            }
            SortedSet<(int, int)> used = GetUsed(@"C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event");
            IEnumerable<(int, int)> getInstrs(InstructionDocs docs)
            {
                return docs.DOC.Classes.SelectMany(c => c.Instructions.Select(i => ((int)c.Index, (int)i.Index)));
            }
            string getName(InstructionDocs docs) => docs.ResourceString.Replace("-common.emedf.json", "");
            Dictionary<string, List<string>> argsMap = new();
            Dictionary<string, string> enumMap = new();
            Dictionary<string, string> instrMap = new();
            string argStr(EMEDF.ArgDoc a) => (a.EnumName == null ? "" : $"{a.EnumName} ") + $"{RevTypes[(int)a.Type]} {a.Name}";
            foreach ((int bank, int id) in getInstrs(emedfs[1]))
            {
                EMEDF.InstrDoc first = emedfs[1].DOC[bank]?[id];
                EMEDF.InstrDoc main = emedfs[2].DOC[bank]?[id];
                if (main == null) continue;
                instrMap[first.Name] = main.Name;
                for (int i = 0; i < Math.Min(first.Arguments.Count, main.Arguments.Count); i++)
                {
                    EMEDF.ArgDoc firstArg = first.Arguments[i];
                    EMEDF.ArgDoc mainArg = main.Arguments[i];
                    if (!argsMap.TryGetValue(firstArg.Name, out List<string> mainNames))
                    {
                        argsMap[firstArg.Name] = mainNames = new List<string>();
                    }
                    mainNames.Add(mainArg.Name);
                    if (firstArg.EnumName != null && mainArg.EnumName != null)
                    {
                        enumMap[firstArg.EnumName] = mainArg.EnumName;
                    }
                }
            }
            if (args.Contains("enum"))
            {
                foreach (var g in enumMap.GroupBy(e => e.Value))
                {
                    if (g.Count() > 1)
                    {
                        Console.WriteLine($"{g.Key}: {string.Join("//", g.Select(e => e.Key))}");
                    }
                }
                return;
            }
            Dictionary<string, string> argMap = argsMap.ToDictionary(e => e.Key, e => e.Value.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key);
            foreach ((int bank, int id) in getInstrs(emedfs[0]))
            {
                string insStr = InstructionDocs.FormatInstructionID(bank, id);
                foreach (InstructionDocs docs in emedfs)
                {
                    EMEDF.InstrDoc instrDoc = docs.DOC[bank]?[id];
                    if (instrDoc == null) continue;
                    Console.WriteLine($"{getName(docs).PadRight(6)}, {insStr}, {instrDoc.Name}{string.Join("", instrDoc.Arguments.Select(a => $", {argStr(a)}"))}");
                }
                EMEDF.InstrDoc first = emedfs[0].DOC[bank]?[id];
                EMEDF.InstrDoc main = emedfs[2].DOC[bank]?[id];
                if (main == null) main = emedfs[3].DOC[bank]?[id];
                if (instrMap.TryGetValue(first.Name, out string instrName))
                {
                    first.Name = instrName;
                }
                for (int i = 0; i < first.Arguments.Count; i++)
                {
                    EMEDF.ArgDoc firstArg = first.Arguments[i];
                    if (firstArg.EnumName != null)
                    {
                        firstArg.EnumName = enumMap.TryGetValue(firstArg.EnumName, out string mainEnum) ? mainEnum : "UNKENUM" + firstArg.EnumName;
                    }
                    if (main != null && i < main.Arguments.Count)
                    {
                        EMEDF.ArgDoc mainArg = main.Arguments[i];
                        if (firstArg.Type == mainArg.Type && firstArg.EnumName == mainArg.EnumName)
                        {
                            string extra = "//";
                            if (argsMap.TryGetValue(firstArg.Name, out List<string> names))
                            {
                                extra = names.Contains(mainArg.Name) ? "" : "//" + argMap[firstArg.Name];
                            }
                            firstArg.Name = mainArg.Name + extra;
                        }
                    }
                    else
                    {
                        if (argMap.TryGetValue(firstArg.Name, out string mainName))
                        {
                            firstArg.Name = mainName;
                        }
                    }
                }
                string type = used.Contains((bank, id)) ? "new" : "new!!";
                Console.WriteLine($"{type.PadRight(6)}, {insStr}, {first.Name}{string.Join("", first.Arguments.Select(a => $", {argStr(a)}"))}");
                Console.WriteLine();
            }
        }

        // InstructionDocs helper
        public void AddUnknownValues(EMEDF emedf, string csv)
        {
            doc = emedf;
            Dictionary<int, string> bankNames = new()
            {
                [10] = "Condition - Sound",
                [1010] = "Control Flow - Sound",
                [1012] = "Control Flow - Map",
            };
            foreach (string line in File.ReadAllLines(csv))
            {
                string[] cells = line.Split(",").Select(s => s.Trim()).ToArray();
                string cmd = cells[0];
                if (!cmd.StartsWith('>')) continue;
                cmd = cmd.Substring(1);
                int bank, id;
                try
                {
                    (bank, id) = InstructionDocs.ParseInstructionID(cmd);
                }
                catch (FormatException)
                {
                    continue;
                }
                string name = cells[1].Trim(new[] { '?', ' ' });
                if (name.Contains("XUnknown")) continue;
                List<EMEDF.ArgDoc> args = cells.Skip(2)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => ParseArgType(c))
                    .ToList();
                EMEDF.ClassDoc classDoc = doc[bank];
                if (classDoc == null)
                {
                    string bankName = bankNames[bank];  // $"Unk Bank {bank}"
                    doc.Classes.Add(new EMEDF.ClassDoc { Name = bankName, Index = bank, Instructions = new() });
                }
                EMEDF.InstrDoc instrDoc = doc[bank][id];
                if (instrDoc == null)
                {
                    instrDoc = new EMEDF.InstrDoc
                    {
                        Name = name,
                        Index = id,
                        Arguments = args,
                    };
                    doc[bank].Instructions.Add(instrDoc);
                }
            }
            doc.Classes = doc.Classes.OrderBy(x => x.Index).ToList();
            foreach (EMEDF.ClassDoc classDoc in doc.Classes)
            {
                classDoc.Instructions = classDoc.Instructions.OrderBy(x => x.Index).ToList();
            }
        }

        public void AnalyzeArgs(string[] args)
        {
            Dictionary<string, string> defaultGameDirs = new Dictionary<string, string>
            {
                ["ds1"] = @"C:\Program Files (x86)\Steam\steamapps\common\Dark Souls Prepare to Die Edition\DATA\event",
                ["ds1r"] = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS REMASTERED\event",
                ["bb"] = @"D:\Gamedev\Bloodborne\uroot\dvdroot_ps4\event",
                ["ds3"] = @"C:\Program Files (x86)\Steam\steamapps\common\DARK SOULS III\Game\event",
                // ["ds3"] = @"C:\Users\matt\Downloads\ModData\event_snake",
                ["sekiro"] = @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro\event",
                ["er"] = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event",
                // ["er"] = @"C:\Users\matt\Downloads\ModData\event_erc",
                ["ac6"] = @"C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event",
                ["nr"] = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING NIGHTREIGN\Game\event",
            };
            foreach ((string game, string dir) in defaultGameDirs)
            {
                if (game != "nr") continue;
                List<string> paths = Directory.GetFiles(dir, "*.emevd").Concat(Directory.GetFiles(dir, "*.emevd.dcx")).ToList();
                if (game == "ds3") paths.RemoveAll(p => Path.GetFileName(p).StartsWith("m2"));
                Dictionary<string, EMEVD> emevds = paths.ToDictionary(
                    p => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(p)),
                    p => EMEVD.Read(p));
                string res = game == "ds1r" ? "ds1" : game;
                InstructionDocs docs = new InstructionDocs($"{res}-common.emedf.json");
                AnalyzeArgs(emevds, docs);
            }
        }

        public void AnalyzeArgs(Dictionary<string, EMEVD> emevds, InstructionDocs docs)
        {
            doc = docs.DOC;
            Dictionary<(string, long), List<EMEDF.ArgDoc>> inferred = new();
            (int, int) getParamPair(EMEVD.Parameter p) => ((int)p.SourceStartByte, p.ByteCount);
            string showParam((int, int) p) => $"X{p.Item1}_{p.Item2}";
            string showFull(EMEVD.Parameter p) => $"^{p.InstructionIndex}({p.TargetStartByte} <- X{p.SourceStartByte}_{p.ByteCount})";
            List<string> violations = new();
            int total = 0;
            void addViolation(string v)
            {
                Console.WriteLine(v);
                violations.Add(v);
            }
            bool allowMismatch = docs.ResourceString.StartsWith("ac6");
            Dictionary<long, List<string>> eventFiles = new();
            foreach ((string map, EMEVD emevd) in emevds)
            {
                foreach (EMEVD.Event e in emevd.Events)
                {
                    if (e.ID > 250)
                    {
                        if (!eventFiles.ContainsKey(e.ID)) eventFiles[e.ID] = new();
                        eventFiles[e.ID].Add(map);
                    }
                    Dictionary<(int, int), List<EMEDF.ArgDoc>> paramDocs =
                        e.Parameters.Select(getParamPair).Distinct().ToDictionary(p => p, _ => new List<EMEDF.ArgDoc>());
                    Dictionary<EMEVD.Parameter, string> pn = e.Parameters.ToDictionary(p => p, p => $"X{p.SourceStartByte}_{p.ByteCount}");
                    string evId = $"{e.ID} in {map}";
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        EMEDF.InstrDoc instrDoc = doc[ins.Bank]?[ins.ID];
                        if (instrDoc == null) throw new Exception($"{map} {e.ID}: Unknown {ins.Bank} {ins.ID}");
                        // Parse this normally for display purposes and also simple validation
                        List<object> plainArgs;
                        try
                        {
                            plainArgs = docs.UnpackArgsWithParams(
                                ins, i, instrDoc, pn, (argDoc, val) => argDoc.GetDisplayValue(val), allowMismatch).Args;
                        }
                        catch
                        {
                            Console.WriteLine(InstructionDocs.InstrDebugStringFull(ins, evId, i, pn));
                            throw;
                        }
                        string showInstr()
                        {
                            return $"{evId} #{i} {instrDoc.Name}({string.Join(", ", plainArgs)})";
                        }
                        // This is just initialization, and probably disallow recursive stuff
                        if (docs.IsVariableLength(instrDoc))
                        {
                            if (e.Parameters.Any(p => p.InstructionIndex == i))
                            {
                                // This is fine, just need to unpack differently and repeat the last arg
                                addViolation($"Init has parameters: {showInstr()}");
                                Console.WriteLine($"Args ({e.Instructions.Count}): {string.Join(" ", e.Parameters.Select(showFull))}");
                                continue;
                            }
                        }
                        List<ArgType> argStruct = instrDoc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
                        if (allowMismatch && argStruct.Count > plainArgs.Count)
                        {
                            argStruct = argStruct.Take(plainArgs.Count).ToList();
                        }
                        List<object> args;
                        try
                        {
                            args = InstructionDocs.UnpackArgsSafe(ins.ArgData, argStruct);
                        }
                        catch when (allowMismatch)
                        {
                            // Shrug
                            continue;
                        }
                        List<int> positions = docs.FuncBytePositions[instrDoc];
                        for (int argIndex = 0; argIndex < args.Count; argIndex++)
                        {
                            EMEDF.ArgDoc argDoc = instrDoc.Arguments[argIndex];
                            int bytePos = positions[argIndex];
                            EMEVD.Parameter p = e.Parameters.Find(p => p.InstructionIndex == i && bytePos == p.TargetStartByte);
                            if (p == null) continue;
                            paramDocs[getParamPair(p)].Add(argDoc);
                            total++;
                        }
                    }
                    if (paramDocs.Count == 0)
                    {
                        inferred[(map, e.ID)] = new();
                        continue;
                    }
                    // Check single parameter width per offset (otherwise assume max)
                    // TODO: Handle multiple widths
                    Dictionary<int, int> paramWidths = new();
                    foreach (var g in paramDocs.Keys.GroupBy(p => p.Item1))
                    {
                        if (g.Count() > 1)
                        {
                            addViolation($"Param has multiple widths: {evId} {string.Join(", ", g.Select(showParam))}");
                        }
                        (int pos, int width) = g.Max();
                        if (width == 0 || pos % width != 0)
                        {
                            addViolation($"Invalid param alignment: {evId} {string.Join(", ", g.Select(showParam))}");
                        }
                        paramWidths[g.Key] = width;
                    }
                    // Check single arg type per parameter
                    // Check single arg enum type per parameter
                    Dictionary<int, EMEDF.ArgDoc> posArgs = new();
                    foreach (((int, int) p, List<EMEDF.ArgDoc> argDocs) in paramDocs.OrderByDescending(e => e.Key))
                    {
                        if (argDocs.Count == 0)
                        {
                            // This happens in AC6 m80 because of init with parameters
                            // But basically an internal error
                            // addViolation($"Could not find arg info for {evId} {showParam(p)} (either vararg or invalid index)");
                            continue;
                        }
                        List<string> types = argDocs.Select(a => a.Type == 8 ? "uint" : RevTypes[(int)a.Type]).Distinct().ToList();
                        if (types.Count > 1)
                        {
                            addViolation($"Param has multiple types: {evId} {showParam(p)}: {string.Join(", ", types)}");
                        }
                        List<string> enumTypes = argDocs.Select(a => a.EnumName).Distinct().ToList();
                        if (enumTypes.Count > 1)
                        {
                            // In Sekiro 20005580 etc., enum is used with comparison
                            addViolation($"Param has multiple enum types: {evId} {showParam(p)}: {string.Join(", ", enumTypes)}");
                        }
                        List<int> widths = types.Select(t => Widths[t]).Distinct().ToList();
                        widths.Remove(p.Item2);
                        if (widths.Count > 0)
                        {
                            // TODO: Change width of ArgDoc if needed
                            addViolation($"Param arg has different width: {evId} {showParam(p)}: {string.Join(", ", widths)}");
                        }
                        if (!posArgs.ContainsKey(p.Item1))
                        {
                            posArgs[p.Item1] = argDocs.Last();
                        }
                    }
                    // Check everything is packed together
                    List<EMEDF.ArgDoc> mainArgs = new();
                    int lastPos = -1;
                    foreach ((int pos, EMEDF.ArgDoc argDoc) in posArgs.OrderBy(x => x.Key))
                    {
                        int width = paramWidths[pos];
                        if (lastPos == -1)
                        {
                            lastPos = 0;
                        }
                        else if (width > 0)
                        {
                            // Pad
                            if (lastPos % width > 0)
                            {
                                lastPos += width - (lastPos % width);
                            }
                        }
                        if (pos != lastPos)
                        {
                            addViolation($"Misaligned param: {evId} should have X{lastPos}_{width}, has {string.Join(",", paramDocs.Keys.OrderBy(x => x).Select(showParam))}");
                        }
                        lastPos = pos + width;
                        mainArgs.Add(argDoc);
                    }
                    inferred[(map, e.ID)] = mainArgs;
                }
            }
            // Basic parsing for now
            foreach ((string map, EMEVD emevd) in emevds)
            {
                // Initializations are possible if they're both loaded by the game and linked in the file.
                List<string> linkedMaps = new();
                BinaryReaderEx reader = new BinaryReaderEx(false, emevd.StringData);
                foreach (long offset in emevd.LinkedFileOffsets)
                {
                    reader.Position = offset;
                    string path = reader.ReadUTF16();
                    string linked = Path.GetFileNameWithoutExtension(path);
                    linkedMaps.Add(linked);
                    Console.WriteLine($"{map} -> {path}");
                }
                List<string> normalLinks = new();
                if (emevds.ContainsKey("common_func")) normalLinks.Add("common_func");
                if (docs.ResourceString.StartsWith("bb")) normalLinks.Add(map.StartsWith("m29_") ? "m29" : "common");
                foreach ((long id, List<string> maps) in eventFiles)
                {
                    if (maps.Contains(map) && maps.Intersect(linkedMaps).Any())
                    {
                        addViolation($"Same event {id} in both {map} and linked map {string.Join(",", linkedMaps)}");
                    }
                }
                // Console.WriteLine($"{map} -> {string.Join(",", linkedMaps)}");
                foreach (EMEVD.Event e in emevd.Events)
                {
                    string evId = $"{e.ID} in {map}";
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        EMEDF.InstrDoc instrDoc = doc[ins.Bank]?[ins.ID];
                        if (instrDoc == null) throw new Exception($"{map} {e.ID}: Unknown {ins.Bank} {ins.ID}");
                        if (!docs.IsVariableLength(instrDoc)) continue;
                        // Need a better way to do this
                        int calleeIndex = instrDoc.Arguments.ToList().FindIndex(a => a.Name == "Event ID");
                        bool commonInit = instrDoc.Name.Contains("Common");
                        uint callee = BitConverter.ToUInt32(ins.ArgData, calleeIndex * 4);
                        // We have to allow all of these violations, but leave them commented to find them easier
                        string desc() => $"{instrDoc.Name} {callee} from {evId} [{string.Join(", ", linkedMaps)}]";
                        if (callee == 0xFFFF_FFFF)
                        {
                            // In most games aside from Elden Ring and AC6
                            // addViolation($"Init -1: {evId}");
                            continue;
                        }
                        List<EMEDF.ArgDoc> argDocs = null;
                        string evMap = null;
                        if (commonInit && inferred.TryGetValue(("common_func", callee), out argDocs))
                        {
                            evMap = "common_func";
                        }
                        else if (inferred.TryGetValue((map, callee), out argDocs))
                        {
                            evMap = map;
                        }
                        if (evMap == null)
                        {
                            foreach (string linked in linkedMaps)
                            {
                                if (inferred.TryGetValue((linked, callee), out argDocs))
                                {
                                    evMap = linked;
                                    break;
                                }
                            }
                        }
                        if (evMap == null)
                        {
                            // Nearly all games
                            // addViolation($"No init found anywhere: {desc()}");
                            continue;
                        }
                        if (commonInit && evMap != "common_func")
                        {
                            // Many in Elden Ring and AC6
                            // addViolation($"Extraneous common_func init: {desc()} -> {evMap}");
                        }
                        if (!commonInit && evMap == "common_func")
                        {
                            // One in DS3
                            addViolation($"Unmarked common_func init: {desc()} -> {evMap}");
                        }
                        if (evMap != map && !normalLinks.Contains(evMap))
                        {
                            // None
                            addViolation($"Unusual init: {desc()} -> {evMap}");
                        }
                        List<ArgType> argStruct = argDocs.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
                        if (argDocs.Count == 0)
                        {
                            argStruct.Add(ArgType.Int32);
                        }
                        List<int> positions = InstructionDocs.GetArgBytePositions(argStruct, 0);
                        int expectLength = positions.Last();
                        int dataLength = ins.ArgData.Length - (calleeIndex + 1) * 4;
                        if (expectLength != dataLength)
                        {
                            addViolation($"Mismatched arg length: {desc()}, have {dataLength}, expect {expectLength}");
                            continue;
                        }
                        // TODO fix this, add an offset arg somewhere, or add to argDocs itself
                        List<ArgType> fullArgStruct = Enumerable.Repeat(ArgType.Int32, calleeIndex + 1).Concat(argStruct).ToList();
                        List<object> args = InstructionDocs.UnpackArgsSafe(ins.ArgData, fullArgStruct);
                        args.RemoveRange(0, calleeIndex + 1);
                        if (argDocs.Count == 0 && !(args[0] is int val && val == 0))
                        {
                            addViolation($"Zero-param event with args: {desc()} ({string.Join(", ", args)})");
                        }
                    }
                }
            }
            Console.WriteLine($"{docs.ResourceString}: {violations.Count} violations, {total} args");
        }

        public void ValidateEmedf(ICollection<string> opt)
        {
            InstructionDocs docs = new InstructionDocs("nr-common.emedf.json");
            doc = docs.DOC;
            Dictionary<string, List<List<object>>> allArgs = new Dictionary<string, List<List<object>>>();
            // string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event";
            // string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ARMORED CORE VI FIRES OF RUBICON\Game\event";
            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING NIGHTREIGN\Game\event";
            foreach (string path in Directory.GetFiles(dir, "*.emevd.dcx"))
            {
                string fileName = Path.GetFileName(path);
                emevd = EMEVD.Read(path);
                foreach (EMEVD.Event e in emevd.Events)
                {
                    Dictionary<EMEVD.Parameter, string> pn = e.Parameters.ToDictionary(p => p, p => $"X{p.SourceStartByte}_{p.ByteCount}");
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        EMEDF.InstrDoc instrDoc = doc[ins.Bank]?[ins.ID];
                        if (instrDoc == null)
                        {
                            Console.WriteLine($"Missing {InstructionDocs.InstrDebugStringFull(ins, fileName, i, pn)}");
                            continue;
                        }
                        try
                        {
                            List<object> args = docs.UnpackArgsWithParams(
                                ins, i, instrDoc, pn, (argDoc, val) => argDoc.GetDisplayValue(val)).Args;
                            string name = instrDoc.DisplayName;
                            if (!allArgs.ContainsKey(name)) allArgs[name] = new List<List<object>>();
                            allArgs[name].Add(args);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Bad {InstructionDocs.InstrDebugStringFull(ins, fileName, i, pn)}");
                            continue;
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, List<List<object>>> entry in allArgs)
            {
                string name = entry.Key;
                int maxArg = entry.Value.Select(x => x.Count).Min();
                (int bank, int id) = docs.Functions[name];
                EMEDF.InstrDoc instrDoc = doc[bank][id];
                List<int> positions = docs.FuncBytePositions[instrDoc];
                for (int i = 0; i < maxArg; i++)
                {
                    EMEDF.ArgDoc argDoc = instrDoc.Arguments[i];
                    string argDesc = $"{name}[{argDoc.DisplayName}]";
                    HashSet<object> vals = new HashSet<object>();
                    foreach (List<object> args in entry.Value)
                    {
                        object val = args[i];
                        if (!(val is string s && s.StartsWith("X"))) vals.Add(val);
                    }
                    if (vals.Count == 1 && positions[i] % 4 != 0)
                    {
                        // Try to detect spare byte arguments
                        // ...?
                        // Console.WriteLine($"Spare {argDesc} = {vals.First()}");
                    }
                    if (argDoc.EnumDoc != null)
                    {
                        List<object> intObjs = vals.Where(v => v is not string).ToList();
                        if (intObjs.Count > 0)
                        {
                            Console.WriteLine($"Enum {argDesc} = {string.Join(",", intObjs)}");
                        }
                    }
                    // EMEDF arg checking
                    // Check all defaults fit int types
                    List<object> checks = new List<object> { argDoc.Default, argDoc.Min, argDoc.Max, argDoc.Increment };
                    foreach (object checkVal in checks)
                    {
                        string check = checkVal.ToString();
                        try
                        {
                            if (argDoc.Type == 0) Convert.ToByte(check); //u8
                            else if (argDoc.Type == 1) Convert.ToUInt16(check); //u16
                            else if (argDoc.Type == 2) Convert.ToUInt32(check); //u32
                            else if (argDoc.Type == 3) Convert.ToSByte(check); //s8
                            else if (argDoc.Type == 4) Convert.ToInt16(check); //s16
                            else if (argDoc.Type == 5) Convert.ToInt32(check); //s32
                            else if (argDoc.Type == 6) Convert.ToSingle(check); //f32
                        }
                        catch (Exception)
                        {
                            Console.WriteLine($"! Bad meta value {argDesc} = {check}");
                        }
                    }
                }
            }
        }

        public void RunEldenInitTests(ICollection<string> args)
        {
            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event";
            string outDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"Downloads\Mods\ModEngine-2.0.0-preview3-win64\condtest\event");
            testItemLots = new Dictionary<int, string>
            {
                [997200] = "Rowa",
                [996500] = "Poisonbloom",
                [996540] = "Grave Violet",
                [996800] = "Erdleaf Flower",
                [996830] = "Golden Sunflower",
                [997220] = "Rimed Rowa",
                [997230] = "Bloodrose",
                [998400] = "Cave Moss",
                [997600] = "Mushroom",
                [997950] = "Sanctuary Stone",
                // [997210] = "Golden Rowa",
                // [998410] = "Budding Cave Moss",
                // [998420] = "Crystal Cave Moss",
            };
            testItemLotOrder = testItemLots.Keys.ToList();

            // Just delete previous tests
            foreach (string path in Directory.GetFiles(dir, "*.emevd.dcx"))
            {
                string fileName = Path.GetFileName(path);
                string outFile = Path.Combine(outDir, fileName);
                if (File.Exists(outFile)) File.Delete(outFile);
            }
            // Test if the InitializeCommonEvent self-file things always work
            // Test whether first or second event gets initialized
            // Test same event id in both map and common_func
            Dictionary<string, EMEVD> emevds = new();
            EMEVD load(string name)
            {
                if (emevds.TryGetValue(name, out EMEVD emevd)) return emevd;
                emevd = EMEVD.Read(Path.Combine(dir, name + ".emevd.dcx"));
                emevds[name] = emevd;
                return emevd;
            }
            if (args.Contains("priority"))
            {
                addEvents = null;
                EMEVD commonFunc = load(args.Contains("link_common") ? "common" : "common_func");
                EMEVD commonFunc2 = args.Contains("link_common_func") ? load("common_func") : commonFunc;
                EMEVD map = load(args.Contains("common") ? "common" : "m10_01_00_00");
                if (args.Contains("link_common"))
                {
                    map.LinkedFileOffsets.Add(map.StringData.Length);
                    // var reader = new BinaryReaderEx(false, EVD.StringData);
                    var writer = new BinaryWriterEx(false);
                    writer.WriteBytes(map.StringData);
                    writer.WriteUTF16("N:\\GR\\data\\Param\\event\\common.emevd", true);
                    map.StringData = writer.FinishBytes();
                }
                bool crossFile = args.Contains("common_func");
                bool bothCrossFile = args.Contains("common_func2");
                foreach (bool firstCommon in new[] { true, false })
                {
                    foreach (bool secondCommon in new[] { true, false })
                    {
                        int id = evBase;
                        evBase += 10;
                        if (args.Contains("delay")) map.Events[0].Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (10)"));
                        EMEVD.Event firstEvent = new EMEVD.Event(id);
                        if (bothCrossFile) commonFunc.Events.Add(firstEvent);
                        else map.Events.Add(firstEvent);
                        firstEvent.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (1)"));
                        AddItemTest(firstEvent, $"map event with {(firstCommon ? "common" : "reg")} init");
                        map.Events[0].Instructions.Add(new EMEVD.Instruction(
                            2000, firstCommon ? 6 : 0, new List<object> { 0, id, 0 }));

                        if (args.Contains("delay")) map.Events[0].Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (10)"));
                        EMEVD.Event secondEvent = new EMEVD.Event(id);
                        secondEvent.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (1)"));
                        AddItemTest(secondEvent, $"{(crossFile ? "common_func" : "dupe")} event with {(secondCommon ? "common" : "reg")} init");
                        if (crossFile) commonFunc2.Events.Add(secondEvent);
                        else map.Events.Add(secondEvent);
                        // Needs to be index 1 or else multi reg won't work
                        map.Events[0].Instructions.Add(new EMEVD.Instruction(
                            2000, secondCommon ? 6 : 0, new List<object> { 1, id, 0 }));
                    }
                }
                // With common/reg and first/dupe: Inits first event if either use common init, second event otherwise
                // However, this seems to be order-dependent

                // Init from common:
                // Inits second event if either use common init, first event otherwise

                // With common_func:
                // Inits common_func always

                // With common and common_func:
                // Inits map always (not linked)

                // With link common
                // Inits common always

                // Two common_func events
                // Inits second always

                // Two common events
                // Inits second always

                // common_func and common both linked
                // First link is always used

                // In conclusion: linked from another file, pick the first such file, and last event
                // If only present in this file and duplicate, do not pick one

            }
            // Test cross-byte parameter init
            // Test recursive init after all, which indices are used
            foreach ((string name, EMEVD emevd) in emevds)
            {
                string outPath = Path.Combine(outDir, name + ".emevd.dcx");
                Console.WriteLine(outPath);
                emevd.Write(outPath);
            }
        }

        public void RunEldenCommonTests(ICollection<string> args)
        {
            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event";
            // Specific installation location
            string outDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"Downloads\Mods\ModEngine-2.0.0-preview3-win64\condtest\event");
            testItemLots = new Dictionary<int, string>
            {
                [997200] = "Rowa",
                [996500] = "Poisonbloom",
                [996540] = "Grave Violet",
                [996800] = "Erdleaf Flower",
                [996830] = "Golden Sunflower",
                [997220] = "Rimed Rowa",
                [997230] = "Bloodrose",
                [998400] = "Cave Moss",
                [997600] = "Mushroom",
                [997950] = "Sanctuary Stone",
                // [997210] = "Golden Rowa",
                // [998410] = "Budding Cave Moss",
                // [998420] = "Crystal Cave Moss",
            };
            testItemLotOrder = testItemLots.Keys.ToList();

            // Just delete previous tests
            foreach (string path in Directory.GetFiles(dir, "*.emevd.dcx"))
            {
                string fileName = Path.GetFileName(path);
                string outFile = Path.Combine(outDir, fileName);
                if (File.Exists(outFile)) File.Delete(outFile);
            }
            string name = "common";
            // name = "m11_00_00_00";
            // name = "common_func";
            emevd = EMEVD.Read($@"{dir}\{name}.emevd.dcx");
            string otherName = null;
            // otherName = "m11_00_00_00";
            EMEVD other = otherName == null ? null : EMEVD.Read($@"{dir}\{otherName}.emevd.dcx");
            EMEVD.Event createEvent(EMEVD initPlace = null)
            {
                EMEVD.Event ev = new EMEVD.Event(evBase++);
                emevd.Events.Add(ev);
                initPlace = initPlace ?? emevd;
                initPlace.Events[0].Instructions.Add(new EMEVD.Instruction(
                    2000, name == "common_func" ? 6 : 0, new List<object> { 0, (uint)ev.ID, 0 }));
                return ev;
            }
            EMEVD.Event runner = createEvent();
            if (args.Contains("onoff"))
            {
                // Use register 5. Just detect when condition on/off, with a delay just in case
                // 1[04] - ? 1[04] (5,1)
                // 3[30] IfMapLoaded - 3[30] (5,11,5,0,0)
                // 3[37] WeatherLot - 3[37] (5,2000,0)
                // 3[38] Gender? - 3[38] (5,1)
                // 4[28] LocalPcTargetState - 4[28] (5,2,31,3)
                // 3[31] IfWeatherActive - 3[31] (5,3,0,0)
                // 3[32] IfPlayerInOwnerMap - 3[32] (5,0,0)
                // 4[15] DeadAliveAlt
                // 5[06] IfObjectDestroyed - 10001600 first bird barrel
                EMEVD.Event ev = createEvent(other);
                EMEVD.Instruction test = ParseAdd("3[32] (5,1,1)");
                ev.Instructions.Add(test);
                ev.Instructions.Add(ParseAdd("IF Condition Group (0,1,5)"));
                ev.Instructions.Add(ParseAdd($"Award Item Lot ({testItemLotOrder[0]})"));
                ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (15)"));
                ev.Instructions.Add(test);
                ev.Instructions.Add(ParseAdd("IF Condition Group (0,0,5)"));
                ev.Instructions.Add(ParseAdd($"Award Item Lot ({testItemLotOrder[1]})"));
                ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (15)"));
                ev.Instructions.Add(ParseAdd("END Unconditionally (1)"));
            }
            if (args.Contains("sip"))
            {
                runner.Instructions.Add(ParseAdd($"Set Event Flag (0,{falseFlag},0)"));
                runner.Instructions.Add(ParseAdd($"Set Event Flag (0,{trueFlag},1)"));
                // runner.Instructions.Add(ParseAdd($"IF Character Has SpEffect (0,10000,501000,1,0,1)"));
                runner.Instructions.Add(ParseAdd($"IF Character Has State Info (0,10000,275,1,0,1)"));
                if (args.Contains("clear")) TestClearGroupsElden();
                if (args.Contains("uncompiledmain")) TestUncompiledMain();
                if (args.Contains("stop"))
                {
                    runner.Instructions.Add(ParseAdd("2001[05] (1)"));
                }
                foreach (int id in addEvents)
                {
                    runner.Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, id, 0 }));
                }
            }
            if (args.Contains("weather"))
            {
                // Freeze time just in case of weather interference
                EMEVD.Event ev = createEvent();
                if (args.Contains("stop")) ev.Instructions.Add(ParseAdd("2001[05] (1)"));
                for (int i = 0; i < 24; i++)
                {
                    ev.Instructions.Add(ParseAdd($"IF Character Has SpEffect (0,10000,501000,1,0,1)"));
                    ev.Instructions.Add(ParseAdd($"Award Item Lot ({testItemLotOrder[i % testItemLotOrder.Count]})"));
                    ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (30)"));
                    ev.Instructions.Add(ParseAdd($"2003[68] ({i},-1,1)"));
                }
            }
            if (args.Contains("clearsky"))
            {
                // 16 is snowfield?
                EMEVD.Event ev = createEvent();
                ev.Instructions.Add(ParseAdd("2003[68] (5,-1,1)"));
                ev.Instructions.Add(ParseAdd("END Unconditionally (1)"));
                EMEVD weather = EMEVD.Read($@"{dir}\m60_54_56_00.emevd.dcx"); ;
                weather.Events.RemoveAll(e => e.ID == 1054562815);
                weather.Write($@"{outDir}\m60_54_56_00.emevd.dcx");
            }
            // 2000[03] ClearCompiledConditionGroupState (below tests) - done
            // 2001[05] FreezeTime (basic test, also quit out) - done
            // 2003[68] ChangeWeather (many different args) - done
            // 1001[06] WaitFrames (remove, swap with other one, basic test)
            // 2003[75] Telescope (args)
            // 2003[81] RemoveGesture?
            // 2004[27] Chariot (?)
            // 2004[60] Character-Asset
            // 2004[61] 9999 Graveyard Warp
            // 2004[75] (set FaceParam)
            // 2004[76] ItemLot - combinations of item lot and flag
            // 2004[77] Warp bits
            // 2004[81] ForceCharacterDeath-ish
            // 2005[17] Asset-Dummy
            // 2005[18] Asset-Asset
            // 2006[06] WindSfx
            // 2012[11] SetDarkness
            string outPath = $@"{outDir}\{name}.emevd.dcx";
            Console.WriteLine(outPath);
            emevd.Write(outPath);
            if (other != null)
            {
                outPath = $@"{outDir}\{otherName}.emevd.dcx";
                Console.WriteLine(outPath);
                other.Write(outPath);
            }
            // emevd.Write($@"{dir}\condtest\event\common.emevd", DCX.Type.None);
        }

        public void RunEldenRemoveTest(ICollection<string> args)
        {
            HashSet<(int, int)> allDeleteCommands = new HashSet<(int, int)>
            {
                // Any change observed at all, probably
                (1001, 6), // Cutscene check
                (2001, 4), // Set time
                (2003, 71), // Gesture 1
                (2003, 81), // Gesture 2
                (2003, 75), // Telescope - no-op
                (2003, 80), // Pre-limgrave Scion loss, also, Radahn/Maliketh defeat (anything?)
                (2004, 47), // Graveyard warp. Unknown
                (2004, 61), // Graveyard warp (9999). Unknown
                (2004, 60), // Caravan connection/disconnection
                (2005, 17), // Caravan asset
                (2005, 18), // Caravan asset 2
                (2004, 63), // Enemy range? e.g. Mausoleum, Golem, Caria Manor
                (2004, 69), // Associated with above
                (2004, 70), // Associated with above
                // Makes archer/mausoleum pop in and out
                (2005, 2), // Chair assets
                (2005, 13), // Chair assets with enable/disable
                // Vanilla: roll into chairs. ...yep, chair breaks
                (2003, 78), // Enable WorldMapPointParam? (roundtable o)

                (2003, 68), // Set weather?
                // Not given
                (2004, 84), // Greyll event
                (2012, 11), // Darkness?
                (2012, 12), // Darkness? disable
                // Vanilla: Try Rennala room - check region

                (1003, 203), (1003, 204), // Elevator interactions?
                (2012, 1), // Mirage rise
                // Vanilla: How to interact with item. ... no obvious difference
                (2010, 11), // Fog gates...?
                // Vanilla: Observe sound e.g. at Godrick
                (2003, 82), // Summon sign
                // Seems normal (but check after)... Great-Jar Knights?
                (2004, 27), // Gelmir Grave chariot
                // Vanilla: Try landing on it. Seems to work even without this
                (2004, 43), // Rogier bloodstain, depending on 10009610
                // Vanilla: what Rogier do? Removing this seems nothing also
                (2004, 71), // Road's End Catacombs spirit summon event
                (2008, 4), // Boss defeat, torrent cutscene, two floats
                // Hard to see effect
                (2009, 11), // Coffin rides, Rya warp, arg 0
                (2003, 74), // Psuedomultiplayer
                (2003, 77), // Psuedomultiplayer 2
                (2003, 76), // Second phase arena of some fights
                (2003, 79), // Coop or invader partner
                // Seems normal?
            };
            HashSet<(int, int)> deleteCommands = new HashSet<(int, int)>
            {
                // (2004, 61),
                // (2004, 63),
                // (2004, 69), (2004, 70), // Distance stuff 63, alongside 69, 70. Peninsula mausoleum/Morne Golem
            };
            // deleteCommands = allDeleteCommands;
            // Altered behaviors:
            // Still nighttime after tutorial (groaning heard from graveyard)
            // No restore to full HP after tutorial
            // Early item pickup (didn't wait for cutscene?)
            // Birdseye telescope no work
            // Melina time change works
            // Caravans do not work, as expected
            // Ranni spawning does not change to nighttime
            // Ranni does not go away when aggro'd by Tree Sentinel (fairly difficult to do...)
            // Is it supposed to be raining in Stormhill...? (no)
            // Roundtable hold in "real" position
            // To check:
            // Yuria just completely whooshes out of existence after cooping
            // Fell Twins spooky effect is gone
            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\ELDEN RING\Game\event";
            // Specific installation location
            string outDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"Downloads\Mods\ModEngine-2.0.0-preview3-win64\condtest\event");
            foreach (string path in Directory.GetFiles(dir, "*.emevd.dcx"))
            {
                string fileName = Path.GetFileName(path);
                string outFile = Path.Combine(outDir, fileName);
                if (File.Exists(outFile)) File.Delete(outFile);
                emevd = EMEVD.Read(path);
                bool modified = false;
                foreach (EMEVD.Event e in emevd.Events)
                {
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        if (allDeleteCommands.Contains((ins.Bank, ins.ID)))
                        {
                            modified = true;
                        }
                        if (deleteCommands.Contains((ins.Bank, ins.ID)))
                        {
                            if (!args.Contains("vanilla"))
                            {
                                EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                                e.Instructions[i] = newInstr;
                                e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                            }
                            modified = true;
                        }
                    }
                }
                if (fileName == "common.emevd.dcx")
                {
                    if (args.Contains("cheatrune"))
                    {
                        emevd.Events[0].Instructions.Add(ParseAdd($"Award Item Lot (12070200)"));
                    }
                    // emevd.Events[0].Instructions.Add(ParseAdd($"Set Event Flag (0,10009610,1)"));
                    if (args.Contains("cheathp"))
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase++);
                        // ev.Instructions.AddRange(instrs.Select(t => events.ParseAdd(t)));
                        ev.Instructions.AddRange(new List<EMEVD.Instruction>
                        {
                            // SetSpEffect(35000, 110)
                            new EMEVD.Instruction(2004, 8, new List<object> { 10000, 110 }),
                            // new EMEVD.Instruction(2004, 8, new List<object> { 35000, 110 }),
                            // Scale damage
                            // new EMEVD.Instruction(2004, 8, new List<object> { 10000, 7200 }),
                            // WaitFixedTimeSeconds
                            new EMEVD.Instruction(1001, 0, new List<object> { (float)1 }),
                            // EndUnconditionally(EventEndType.Restart)
                            new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                        });
                        emevd.Events.Add(ev);
                        emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)ev.ID, (uint)0 }));
                    }
                    modified = true;
                }
                if (modified)
                {
                    Console.WriteLine($"Writing {outFile}");
                    emevd.Write(outFile, quickDcx);
                }
            }
        }

        public void RunTests()
        {
            doc = EMEDF.ReadFile($"DarkScript3/Resources/sekiro-common.emedf.json");
            docByName = doc.Classes.SelectMany(c => c.Instructions.Select(i => (i, (int)c.Index))).ToDictionary(i => i.Item1.Name, i => (i.Item2, (int)i.Item1.Index));
            testItemLots = new SortedDictionary<int, string>
            {
                [60210] = "Remnant Gyoubu",
                [60220] = "Remnant Butterfly",
                [60230] = "Remnant Genichiro",
                [60240] = "Remnant Monkeys",
                [60250] = "Remnant Ape",
                [60260] = "Remnant Monk",
                [62208] = "Rice",
                [62214] = "Fine Snow",
            };
            testItemLotOrder = testItemLots.Keys.ToList();

            string dir = @"C:\Program Files (x86)\Steam\steamapps\common\Sekiro";
            emevd = EMEVD.Read($@"{dir}\event\common.emevd.dcx");
            EMEVD.Event runner = new EMEVD.Event(evBase);
            emevd.Events.Add(runner);
            emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, (uint)runner.ID, 0 }));
            runner.Instructions.Add(ParseAdd($"Set Event Flag ({falseFlag},0)"));
            runner.Instructions.Add(ParseAdd($"Set Event Flag ({trueFlag},1)"));
            // Wait for sip
            runner.Instructions.Add(ParseAdd($"IF Character Has SpEffect (0,10000,3000,1,0,1)"));
            RunTest();
            foreach (int id in addEvents)
            {
                runner.Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, id, 0 }));
            }

            emevd.Write($@"{dir}\condtest\event\common.emevd.dcx");
            emevd.Write($@"{dir}\condtest\event\common.emevd", DCX.Type.None);
        }

        private void TestOrder()
        {
            // Test executing commands out of order
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // test cond[2]
            foreach (bool start in new[] { true, false })
            {
                foreach (bool and in new[] { true, false })
                {
                    int reg1 = and ? 1 : -1;
                    int reg2 = and ? 2 : -2;
                    EMEVD.Event ev = new EMEVD.Event(evBase + i);
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? trueFlag : falseFlag)})"));
                    ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? falseFlag : trueFlag)})"));
                    AddItemTest(ev, $"{(start ? "initial true" : "initial false")} and {(and ? "AND" : "OR")}", reg2);
                }
            }
            /*
            If 2 in initial true and AND, then awarding Fine Snow
            If -2 in initial true and OR, then awarding Rice
            If 2 in initial false and AND, then awarding Remnant Monk
            If -2 in initial false and OR, then awarding Remnant Ape
            Expected output if order is important: initial trues only
            Expected outout if order is not important: initial true and OR; initial false and AND
            Actual output: initial trues only
            Result: order is important
            (caveat from later: unimportant after the first frame of a MAIN evaluation)
            */
        }

        private void TestUndefined()
        {
            // skip if condition group compiled/uncompiled (true/false, and/or)
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool check in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 1 : -1;
                        int val = check ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {check} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected output if compiled default false: 1 2
            Expected output if compiled default true: 3 4
            Expected output if uncompiled default false: 5 6
            Expected output if uncompiled default true: 7 8
            Actual output: fine snow, rice, butterfly, gyoubu
            Result: Compiled default false, uncompiled default true
            */
        }

        private void TestUncompiledMain()
        {
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool check in new[] { true, false })
                {
                    foreach (bool mainused in new[] { true, false })
                    {
                        int reg = 0;
                        int val = check ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        if (mainused)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Elapsed Seconds (0,0)"));
                            // ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{trueFlag})"));
                        }
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {check} check for MAIN (eval {mainused})");
                    }
                }
            }
            /*
            f
            1. If compiled True check for MAIN (eval True), then awarding Sanctuary Stone <-
            2. If compiled True check for MAIN (eval False), then awarding Mushroom <-
            3. If compiled False check for MAIN (eval True), then awarding Cave Moss
            4. If compiled False check for MAIN (eval False), then awarding Bloodrose
            5. If uncompiled True check for MAIN (eval True), then awarding Rimed Rowa
            6. If uncompiled True check for MAIN (eval False), then awarding Golden Sunflower
            7. If uncompiled False check for MAIN (eval True), then awarding Erdleaf Flower <-
            8. If uncompiled False check for MAIN (eval False), then awarding Grave Violet <-
            Conclusion: uncompiled default true, compiled default false
            */
        }

        private void TestDefinedPostEval()
        {
            // Use and/or register as true/false
            // Main eval if true/false
            // Skip if compiled/uncompiled true/false after
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 1 : -1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {start} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected if compiled values hold: none of 1 2 3 4 (skipping if state matches flag)
            Expected if uncompiled values ignored: 7 8, since uncompiled default true
            Actual output: 7 8
            Result: Uncompiled registers get reset after main evaluation
            */
        }

        private void TestChangeState()
        {
            // Set event flag to true/false
            // Set condition group compiled/uncompiled to event flag state
            // Change event flag state
            // Evaluate condition group compiled/uncompiled state
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int flag = flagBase + i;
                        int reg = and ? 1 : -1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"Set Event Flag ({flag},{val})"));
                        // ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (1)"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},{val},0,{flag})"));
                        if (compiled)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,{reg})"));
                        }
                        else
                        {
                            ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{reg})"));
                            ev.Instructions.Add(ParseAdd("Label 0 ()"));
                        }
                        ev.Instructions.Add(ParseAdd($"Set Event Flag ({flag},{1 - val})"));
                        // ev.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (1)"));
                        bool redefine = true;
                        if (redefine) ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},{val},0,{flag})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,1,{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {start} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected if compiled is fixed: none of 1 2 3 4
            Expected if uncompiled is fixed: none of 5 6 7 8
            Expected if uncompiled can change: 5 6 7 8
            Actual output: Nothing
            Result: Uncompiled condition groups are set at the time of evaluation only
            Output with redefining cond group: 5 7.
            Result: Uncompiled condition groups can be and/or together with re-evaluation, and will be false/true after switching values.
            */
        }

        private void TestClearGroups()
        {
            // Set condition group state compiled/uncompiled to true/false
            // Optionally clear compiled state
            // Evaluate compiled/uncompiled condition state
            foreach (bool clear in new[] { true, false })
            {
                foreach (bool compiled in new[] { true, false })
                {
                    foreach (bool start in new[] { true, false })
                    {
                        int reg = 1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        if (compiled)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                            // ev.Instructions.Add(ParseAdd($"WAIT For Condition Group State ({val},{reg})"));
                        }
                        else
                        {
                            ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{reg})"));
                            ev.Instructions.Add(ParseAdd("Label 0 ()"));
                        }
                        if (clear)
                        {
                            ev.Instructions.Add(ParseAdd($"Clear Compiled Condition Group State (0)"));
                        }
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,1,{reg})"));
                        AddItemTest(ev, $"{(clear ? "Cleared" : "Not cleared")} {(compiled ? "compiled" : "uncompiled")} evaluation of {start} value");
                    }
                }
            }
            /*
            1. If Cleared compiled evaluation of True value, then awarding Fine Snow
            2. If Cleared compiled evaluation of False value, then awarding Rice
            3. If Cleared uncompiled evaluation of True value, then awarding Remnant Monk
            4. If Cleared uncompiled evaluation of False value, then awarding Remnant Ape
            5. If Not cleared compiled evaluation of True value, then awarding Remnant Monkeys
            6. If Not cleared compiled evaluation of False value, then awarding Remnant Genichiro
            7. If Not cleared uncompiled evaluation of True value, then awarding Remnant Butterfly
            8. If Not cleared uncompiled evaluation of False value, then awarding Remnant Gyoubu
            Expected for not cleared: 6 8
            Expected if clearing only affects compiled conditions, then switching to default value of false: 1 2 4
            Expected if clearing also affects uncompiled conditions: 1 2 3 4
            Output: fine snow, rice, ape, genichiro, gyoubu
            Result: Clearing only resets compiled conditions, by changing them to false again
            Output with waiting for condition group state: fine snow, rice, ape, genichiro, gyoubu
            */
        }

        private void TestClearGroupsElden()
        {
            // Set condition group state compiled/uncompiled to true/false
            // Optionally clear compiled state
            // Evaluate compiled/uncompiled condition state
            foreach (bool clear in new[] { true, false })
            {
                foreach (bool compiled in new[] { true, false })
                {
                    foreach (bool start in new[] { true, false })
                    {
                        int reg = 1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        if (compiled)
                        {
                            // ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                            ev.Instructions.Add(ParseAdd($"WAIT For Condition Group State ({val},{reg})"));
                        }
                        else
                        {
                            ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{reg})"));
                            ev.Instructions.Add(ParseAdd("Label 0 ()"));
                        }
                        if (clear)
                        {
                            ev.Instructions.Add(ParseAdd($"2000[03] (0)"));
                        }
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,1,{reg})"));
                        AddItemTest(ev, $"{(clear ? "cleared" : "not cleared")} {(compiled ? "compiled" : "uncompiled")} evaluation of {start} value");
                    }
                }
            }
            /*
            1. If cleared compiled evaluation of True value, then awarding Sanctuary Stone <-
            2. If cleared compiled evaluation of False value, then awarding Mushroom <-
            3. If cleared uncompiled evaluation of True value, then awarding Cave Moss
            4. If cleared uncompiled evaluation of False value, then awarding Bloodrose <-
            5. If not cleared compiled evaluation of True value, then awarding Rimed Rowa
            6. If not cleared compiled evaluation of False value, then awarding Golden Sunflower <-
            7. If not cleared uncompiled evaluation of True value, then awarding Erdleaf Flower
            8. If not cleared uncompiled evaluation of False value, then awarding Grave Violet <-
            All false values were not skipped either way. This matches Sekiro results.
            */
        }

        private void TestUnrelatedCompilation()
        {
            // Unrelated uncompiled group states after evaluation of related/unrelated groups
            // cond[1] &= true/false
            // cond[2] &= true/false
            // compile cond[2]
            // Check compiled/uncompiled state of 1/2

            // Slight variant: 
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // compile cond[2]
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        int reg1 = 1;
                        int reg2 = 2;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg2},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg2})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,0,{(check ? reg2 : reg1)})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} check for {start} {(check ? "compiled" : "uncompiled")} register");
                    }
                }
            }
            /*
            1. If compiled check for True compiled register, then awarding Fine Snow
            2. If compiled check for True uncompiled register, then awarding Rice
            3. If compiled check for False compiled register, then awarding Remnant Monk
            4. If compiled check for False uncompiled register, then awarding Remnant Ape
            5. If uncompiled check for True compiled register, then awarding Remnant Monkeys
            6. If uncompiled check for True uncompiled register, then awarding Remnant Genichiro
            7. If uncompiled check for False compiled register, then awarding Remnant Butterfly
            8. If uncompiled check for False uncompiled register, then awarding Remnant Gyoubu
            Output: fine snow, rice, screen monkeys, genichiro, lady butterfly, gyoubu
            Result: after evaluation, all uncompiled groups get reset (become true), and compiled checks are true if register was true, even if not evaluated directly
            */
        }

        private void TestMixedCompilation()
        {
            // Combination of unrelated compilation
            // cond[1] &= true/false
            // cond[2] &= true/false
            // compile cond[2]
            // Check compiled/uncompiled state of 1/2

            // And ordered compilation
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // test cond[2]

            // Slight variant: 
            // cond[1] &= true
            // cond[2] &= cond[1]
            // cond[1] &= false
            // Compile cond[2]
            // Evaluate compiled and uncompiled state of cond[1]
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg1 = and ? 1 : -1;
                        int reg2 = and ? 2 : -2;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start ? falseFlag : trueFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg2})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,0,{reg1})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} check for {start} {(and ? "AND" : "OR")} {!start} register");
                    }
                }
            }
            /*
            1. If compiled check for True AND False register, then awarding Fine Snow
            2. If compiled check for True OR False register, then awarding Rice
            3. If compiled check for False AND True register, then awarding Remnant Monk
            4. If compiled check for False OR True register, then awarding Remnant Ape
            5. If uncompiled check for True AND False register, then awarding Remnant Monkeys
            6. If uncompiled check for True OR False register, then awarding Remnant Genichiro
            7. If uncompiled check for False AND True register, then awarding Remnant Butterfly
            8. If uncompiled check for False OR True register, then awarding Remnant Gyoubu
            Expected if uncompiled check will return true always: 5 6 7 8
            Expected if compiled state includes later value: 2 4
            Expected if compiled state excludes later value: 1 2
            Output: 1 4 5 6 7 8 ? But trying again, 1->2?
            Inconclusive. Try again with only compiled checks.
            */
        }

        private void TestMixedCompilation2()
        {
            // cond[1] &= true/false
            // cond[2] &= cond[1]
            // cond[1] &= false/false
            // Compile cond[2]
            // Evaluate compiled state of cond[1]
            foreach (bool and in new[] { true, false })
            {
                foreach (bool start1 in new[] { true, false })
                {
                    foreach (bool start2 in new[] { true, false })
                    {
                        int reg1 = and ? 1 : -1;
                        int reg2 = and ? 2 : -2;
                        int val = start1 ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start1 ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(start2 ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg2})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,0,{reg1})"));
                        AddItemTest(ev, $"compiled check for {start1} {(and ? "AND" : "OR")} {start2} register");
                    }
                }
            }
            /*
            1. If compiled check for True AND True register, then awarding Fine Snow
            2. If compiled check for True AND False register, then awarding Rice
            3. If compiled check for False AND True register, then awarding Remnant Monk
            4. If compiled check for False AND False register, then awarding Remnant Ape
            5. If compiled check for True OR True register, then awarding Remnant Monkeys
            6. If compiled check for True OR False register, then awarding Remnant Genichiro
            7. If compiled check for False OR True register, then awarding Remnant Butterfly
            8. If compiled check for False OR False register, then awarding Remnant Gyoubu
            Output: fine snow, screen monkeys, genichiro, butterfly
            Result: Compiled condition group state is the state at evaluation time
            */
        }

        private void TestDelayedCompileOrder()
        {
            // cond[1] &= flag1
            // cond[2] &= cond[1]
            // cond[1] &= flag2
            // Compile cond[2]
            // (After 2 seconds, set flag1/flag2 to true)
            // Does it evaluate?
            List<int> flagsToSet = new List<int>();
            foreach (bool and in new[] { true, false })
            {
                foreach (bool setalt in new[] { true, false })
                {
                    int reg1 = and ? 1 : -1;
                    int reg2 = and ? 2 : -2;
                    int flag1 = flagBase + i * 2;
                    int flag2 = flagBase + i * 2 + 1;
                    flagsToSet.Add(flag1);
                    EMEVD.Event ev = new EMEVD.Event(evBase + i);
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(setalt ? flag1 : flag2)})"));
                    ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg1},1,0,{(setalt ? flag2 : flag1)})"));
                    ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg2},1,{reg1})"));
                    ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,{reg2})"));
                    if (setalt)
                    {
                        // This test is messed up, just move on
                    }
                    AddItemTest(ev, $"{(setalt ? "set flag" : "unset flag")} check for {(and ? "AND" : "OR")} register");
                }
            }
            EMEVD.Event setter = new EMEVD.Event(evBase + i);
            // setter.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (20)"));
            foreach (int flag in flagsToSet)
            {
                setter.Instructions.Add(ParseAdd($"Set Event Flag ({flag},1)"));
            }
            AddEvent(setter);
            /*
            1. If set flag check for AND register, then awarding Fine Snow
            2. If unset flag check for AND register, then awarding Rice
            3. If set flag check for OR register, then awarding Remnant Monk
            4. If unset flag check for OR register, then awarding Remnant Ape
            Output with 1 frame delay: monk, ape
            Output with no delay or 0 frame delay: fine snow, monk, ape
            Result: Ordering does not matter for main group evaluation
            */
        }

        private void TestDelayedUndefinedEvaluation()
        {
            // Recall: Compiled default false, uncompiled default true
            // cond[5] &= cond[1]
            // cond[5] &= flag
            // Compile cond[5]
            // (After delay seconds, set flag to true)
            List<int> flagsToSet = new List<int>();
            foreach (bool delay in new[] { true, false })
            {
                foreach (bool truecond in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 5 : -5;
                        int flag = trueFlag;
                        if (delay)
                        {
                            flag = flagBase + i;
                            flagsToSet.Add(flag);
                        }
                        int val = truecond ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{flag})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group ({reg},{val},-1)"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,{reg})"));
                        AddItemTest(ev, $"{(delay ? "delay" : "immediate")} {truecond} check for {(and ? "AND" : "OR")} register");
                    }
                }
            }
            EMEVD.Event setter = new EMEVD.Event(evBase + i);
            setter.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (40)"));
            foreach (int flag in flagsToSet)
            {
                setter.Instructions.Add(ParseAdd($"Set Event Flag ({flag},1)"));
            }
            AddEvent(setter);
            /*
            1. If delay True check for AND register, then awarding Fine Snow
            2. If delay True check for OR register, then awarding Rice
            3. If delay False check for AND register, then awarding Remnant Monk
            4. If delay False check for OR register, then awarding Remnant Ape
            5. If immediate True check for AND register, then awarding Remnant Monkeys
            6. If immediate True check for OR register, then awarding Remnant Genichiro
            7. If immediate False check for AND register, then awarding Remnant Butterfly
            8. If immediate False check for OR register, then awarding Remnant Gyoubu
            Output: 2 5 6 8. Then after delay: 1 4. Never: 3 7
            Note that compiled is default false, uncompiled is default true.
            In this context, AND(01) is considered true always.
            Output if OR(01) is used instead: 2 5 6 8. Then 1 4. So same.
            */
        }

        private void TestUncompiledUse()
        {
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{(check ? 1 : 0)},1)"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {check} check for {start}");
                    }
                }
            }
            /*
            1. If compiled True check for True, then awarding Fine Snow
            2. If compiled False check for True, then awarding Rice
            3. If compiled True check for False, then awarding Remnant Monk
            4. If compiled False check for False, then awarding Remnant Ape
            5. If uncompiled True check for True, then awarding Remnant Monkeys
            6. If uncompiled False check for True, then awarding Remnant Genichiro
            7. If uncompiled True check for False, then awarding Remnant Butterfly
            8. If uncompiled False check for False, then awarding Remnant Gyoubu
            Expected if uncompiled holds: 6 7 (not skipped), and not 5 8
            Expected if compiled ignores uncompiled: 1 3, with true check vs false register
            Output: 1 3 6 7
            */
        }

        private void TestUncompiledCompileUse()
        {
            foreach (bool direct in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? trueFlag : falseFlag)})"));
                        if (!direct)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Condition Group (2,1,1)"));
                        }
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,1,{(direct ? 1 : 2)})"));
                        ev.Instructions.Add(ParseAdd($"Label 0 ()"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,{(check ? 1 : 0)},1)"));
                        AddItemTest(ev, $"{(direct ? "direct" : "indirect")} {check} check for {start}");
                    }
                }
            }
            /*
            1. If direct True check for True, then awarding Fine Snow
            2. If direct False check for True, then awarding Rice
            3. If direct True check for False, then awarding Remnant Monk
            4. If direct False check for False, then awarding Remnant Ape
            5. If indirect True check for True, then awarding Remnant Monkeys
            6. If indirect False check for True, then awarding Remnant Genichiro
            7. If indirect True check for False, then awarding Remnant Butterfly
            8. If indirect False check for False, then awarding Remnant Gyoubu
            Expected if no possible influence: 1 3 5 7 (check is true)
            Result: Uncompiled condition groups have no effect on compiled checks
            */
        }

        private void TestMultiCompile()
        {
            // Use and/or register as true/false
            // Main eval if true/false
            // Skip if compiled/uncompiled true/false after
            foreach (bool direct in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{(start ? 1 : 0)},1)"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (2,1,0,{(start ? trueFlag : falseFlag)})"));
                        bool interfere = true;
                        if (interfere)
                        {
                            ev.Instructions.Add(ParseAdd($"IF Event Flag (1,1,0,{(start ? falseFlag : trueFlag)})"));
                        }
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{(start ? 1 : 0)},2)"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,{(check ? 1 : 0)},{(direct ? 2 : 1)})"));
                        AddItemTest(ev, $"{(direct ? "direct" : "indirect")} {check} check for {start}");
                    }
                }
            }
            /*
            1. If direct True check for True, then awarding Fine Snow
            2. If direct False check for True, then awarding Rice
            3. If direct True check for False, then awarding Remnant Monk
            4. If direct False check for False, then awarding Remnant Ape
            5. If indirect True check for True, then awarding Remnant Monkeys
            6. If indirect False check for True, then awarding Remnant Genichiro
            7. If indirect True check for False, then awarding Remnant Butterfly
            8. If indirect False check for False, then awarding Remnant Gyoubu
            Expected direct output: when check != start, or 2 3
            Expected indirect output if reset: 5 7, where check is true
            Output: 2 3 6 7
            Result: Compilation only affects used condition groups
            With interference: output is 6 8 instead for indirect, when check is true (meaning: 1 is undefined, so it is false?).
            */
        }

        private void TestDelayedMultiCompile()
        {
            // Use and/or register as true/false
            // Main eval if true/false
            // Skip if compiled/uncompiled true/false after
            List<int> flagsToSet = new List<int>();
            foreach (bool start2 in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool check in new[] { true, false })
                    {
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        int flag = flagBase + i;
                        flagsToSet.Add(flag);
                        int reg = -1;
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{(start ? 1 : 0)},{reg})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag (2,1,0,{flag})"));
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start2 ? trueFlag : falseFlag)})"));
                        ev.Instructions.Add(ParseAdd($"IF Condition Group (0,1,2)"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Compiled) (1,{(check ? 1 : 0)},{reg})"));
                        AddItemTest(ev, $"{check} check for {start}->{start2}");
                    }
                }
            }
            EMEVD.Event setter = new EMEVD.Event(evBase + i);
            setter.Instructions.Add(ParseAdd($"WAIT Fixed Time (Frames) (40)"));
            foreach (int flag in flagsToSet)
            {
                setter.Instructions.Add(ParseAdd($"Set Event Flag ({flag},1)"));
            }
            AddEvent(setter);

            /*
            1. If True check for True->True, then awarding Fine Snow
            2. If False check for True->True, then awarding Rice
            3. If True check for False->True, then awarding Remnant Monk
            4. If False check for False->True, then awarding Remnant Ape
            5. If True check for True->False, then awarding Remnant Monkeys
            6. If False check for True->False, then awarding Remnant Genichiro
            7. If True check for False->False, then awarding Remnant Butterfly
            8. If False check for False->False, then awarding Remnant Gyoubu
            Result with AND/OR: 2 4 6 7
            Result with no start2 set: 2 3 6 7
            Setting the register makes a difference if initial is false, and second is true
            Conclusion: Can't rely on main group evaluation to always set compiled state, so never inline groups with more than one usage, to be safe.
            */
        }

        private void TestDefinedPostWait()
        {
            // Variant of TestDefinedPostWait
            // Instead of main group eval, use wait command
            foreach (bool compiled in new[] { true, false })
            {
                foreach (bool start in new[] { true, false })
                {
                    foreach (bool and in new[] { true, false })
                    {
                        int reg = and ? 1 : -1;
                        int val = start ? 1 : 0;
                        EMEVD.Event ev = new EMEVD.Event(evBase + i);
                        ev.Instructions.Add(ParseAdd($"IF Event Flag ({reg},1,0,{(start ? trueFlag : falseFlag)})"));
                        // ev.Instructions.Add(ParseAdd($"IF Condition Group (0,{val},{reg})"));
                        ev.Instructions.Add(ParseAdd($"WAIT For Condition Group State ({val},{reg})"));
                        ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State ({(compiled ? "Compiled" : "Uncompiled")}) (1,{val},{reg})"));
                        AddItemTest(ev, $"{(compiled ? "compiled" : "uncompiled")} {start} check for {(and ? "AND" : "OR")}");
                    }
                }
            }
            /*
            1. If compiled True check for AND, then awarding Fine Snow
            2. If compiled True check for OR, then awarding Rice
            3. If compiled False check for AND, then awarding Remnant Monk
            4. If compiled False check for OR, then awarding Remnant Ape
            5. If uncompiled True check for AND, then awarding Remnant Monkeys
            6. If uncompiled True check for OR, then awarding Remnant Genichiro
            7. If uncompiled False check for AND, then awarding Remnant Butterfly
            8. If uncompiled False check for OR, then awarding Remnant Gyoubu
            Expected if compiled values hold: none of 1 2 3 4 (skipping if state matches flag)
            Expected if uncompiled values ignored: 7 8, since uncompiled default true
            Actual output from main eval: only 7 8
            Actual output of this:
            */
        }

        // Possible further investigation:
        // Check out Sekiro 11105791 for ClearCompiledConditionGroupState on uncompiled
        // Likewise, Sekiro 20004010 for using compiled condition groups from OR of uncompiled
        // Compiled condition groups - are these retained across multiple MAIN groups? DS1 11010001
        // Does default state of compiled condition group change after single evaluation?

        // Compiled default false, uncompiled default true
        private void RunTest()
        {
            TestClearGroups();
            // TestDefinedPostEval();
            // TestChangeState();
            // TestUnrelatedCompilation();
            // TestMixedCompilation();
            // TestMixedCompilation2();
            // TestDelayedCompileOrder();
            // TestDelayedUndefinedEvaluation();
            // TestUncompiledUse();
            // TestUncompiledCompileUse();
            // TestMultiCompile();
            // TestDelayedMultiCompile();
            // TestDefinedPostWait();
        }

        private void AddEvent(EMEVD.Event ev)
        {
            emevd.Events.Add(ev);
            addEvents.Add((int)ev.ID);
        }

        private void AddItemTest(EMEVD.Event ev, string desc, int group = -99)
        {
            int itemLot = testItemLotOrder.Last();
            testItemLotOrder.Remove(itemLot);
            if (group > -99)
            {
                ev.Instructions.Add(ParseAdd($"SKIP IF Condition Group State (Uncompiled) (1,0,{group})"));
            }
            ev.Instructions.Add(ParseAdd($"Award Item Lot ({itemLot})"));
            Console.WriteLine($"// {i}. If{(group > -99 ? $" {group} in" : "")} {desc}, then awarding {testItemLots[itemLot]}");
            i++;
            if (addEvents != null)
            {
                AddEvent(ev);
            }
        }

        // Very simple command parsing scheme.
        private static (string, List<string>) ParseCommandString(string add)
        {
            int sparen = add.LastIndexOf('(');
            int eparen = add.LastIndexOf(')');
            if (sparen == -1 || eparen == -1) throw new Exception($"Bad command string {add}");
            string cmd = add.Substring(0, sparen).Trim();
            return (cmd, add.Substring(sparen + 1, eparen - sparen - 1).Split(',').Where(arg => !string.IsNullOrWhiteSpace(arg)).ToList());
        }

        private EMEVD.Instruction ParseAdd(string add)
        {
            (string cmd, List<string> addArgs) = ParseCommandString(add);
            if (!docByName.TryGetValue(cmd, out (int, int) docId))
            {
                if (cmd.EndsWith("]") && cmd.Contains("["))
                {
                    string[] parts = cmd.TrimEnd(']').Split('[');
                    docId = (int.Parse(parts[0]), int.Parse(parts[1]));
                }
                else throw new Exception($"Unrecognized command '{cmd}'");
            }
            EMEDF.InstrDoc addDoc = doc[docId.Item1][docId.Item2];
            if (addDoc == null) Console.WriteLine($"{docId.Item1} {docId.Item2}: {string.Join(", ", doc[docId.Item1].Instructions.Select(i => i.Index))}");
            List<ArgType> argTypes = addDoc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
            if (addArgs.Count != argTypes.Count) throw new Exception($"Expected {argTypes.Count} arguments for {cmd}, given {addArgs.Count} in {add}");
            return new EMEVD.Instruction(docId.Item1, docId.Item2, addArgs.Select((a, j) => ParseArg(a, argTypes[j])));
        }

        private static object ParseArg(string arg, ArgType type)
        {
            switch (type)
            {
                case ArgType.Byte:
                    return byte.Parse(arg);
                case ArgType.UInt16:
                    return ushort.Parse(arg);
                case ArgType.UInt32:
                    return uint.Parse(arg);
                case ArgType.SByte:
                    return sbyte.Parse(arg);
                case ArgType.Int16:
                    return short.Parse(arg);
                case ArgType.Int32:
                    return int.Parse(arg);
                case ArgType.Single:
                    return float.Parse(arg);
                default:
                    throw new Exception($"Unrecognized arg type {type}");
            }
        }
    }
}
