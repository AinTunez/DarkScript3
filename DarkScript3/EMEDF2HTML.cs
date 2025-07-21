using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using SoulsFormats;

namespace DarkScript3
{
    /// <summary>
    /// Simple hacky HTML representation of important information in an EMEDF file.
    /// 
    /// There is a fair amount of mixing of logic and presentation that might possibly be doable
    /// in a template language, but it would probably be about this lengthy.
    /// 
    /// Make sure to call Escape() on all strings coming from EMEDF.
    /// </summary>
    public class EMEDF2HTML
    {
        public static void Generate(string[] args)
        {
            string game = args[1];
            string outDir = args[2];
            string emevdDir = args.Length > 3 ? string.Join(" ", args.Skip(3)) : null;
            Console.WriteLine($">>>>>> Generating {game}");
            InstructionDocs docs = new InstructionDocs($"{game}-common.emedf.json");
            EMEDF2HTML gen = new EMEDF2HTML();
            gen.AppendEMEDF(docs, emevdDir);
            string outPath = $@"{outDir}\{game}-emedf.html";
            Console.WriteLine($"<<<<<< Out to [{outPath}]");
            using (TextWriter writer = File.CreateText(outPath))
            {
                writer.Write(gen.ToString().Replace("\r\n", "\n"));
            }
        }

        // Various per-game and per-instruction heuristics. Non-static so it's not initialized unnecessarily.
        private readonly Dictionary<string, string> gameNames = new Dictionary<string, string>
        {
            ["ds1"] = "Dark Souls",
            ["bb"] = "Bloodborne",
            ["ds2"] = "Dark Souls II",
            ["ds2scholar"] = "Dark Souls II Scholar",
            ["ds3"] = "Dark Souls III",
            ["sekiro"] = "Sekiro",
            ["er"] = "Elden Ring",
            ["ac6"] = "Armored Core VI",
            ["nr"] = "Elden Ring Nightreign",
        };
        private readonly Dictionary<string, Regex> interestingEmevds = new Dictionary<string, Regex>()
        {
            ["ds1"] = new Regex(@"^(common|m1\d_.*)\.emevd(?:\.dcx)?$"),
            ["bb"] = new Regex(@"^(common|m[23]\d_.*|m29)\.emevd\.dcx$"),
            ["ds3"] = new Regex(@"^(common|common_func|m[34].*)\.emevd\.dcx$"),
            ["sekiro"] = new Regex(@"^(common|common_func|m[12].*)\.emevd\.dcx$"),
        };
        // Not shown unless all usages are in secondary files
        private readonly Dictionary<string, Regex> secondaryEmevd = new Dictionary<string, Regex>()
        {
            ["bb"] = new Regex(@"^m21_01"),
            ["ds3"] = new Regex(@"^m4[67]"),
            ["sekiro"] = new Regex(@"^m.._.._[1-9]"),
            // Files for which MSBs don't exist (there are scattered cases of this in the main mission files as well)
            ["ac6"] = new Regex(@"^m[6-9]|^m02|^m01_[29]|^m00_11_00_00|^m00_20_00_00|^m01_00_[0124]|^m01_01_[279]|^m01_02_[012479]|^m01_03_[01348]|^m01_04_[345]|^m01_05_[12456789]|^m01_06_[57]|^m01_07_[01349]|^m01_08_[023]|^m01_09_[289]|^m01_10_[1358]|^m01_11_[125]|^m01_12_[45]|^m10_01|^m11|^m22"),
        };
        // Abbreviated if too many
        private readonly Dictionary<string, Regex> elidedEmevd = new Dictionary<string, Regex>()
        {
            ["er"] = new Regex(@"^(m60|m61|m3[0-2]|m4[0-3])"),
            ["nr"] = new Regex(@"^(m60|m[2-4])"),
        };
        private readonly Dictionary<string, string> specialCommands = new Dictionary<string, string>
        {
            ["1000[03]"] = "Goto",
            ["1000[04]"] = "EndEvent or RestartEvent",
            ["1000[103]"] = "Goto",
            ["0[00]"] = "WaitFor",
            ["1000[00]"] = "WaitFor",
            ["1000[01]"] = "GotoIf",
            ["1000[02]"] = "EndIf or RestartIf",
            ["1000[101]"] = "GotoIf",
            ["1000[07]"] = "GotoIf(cond.Passed)",
            ["1000[08]"] = "EndIf(cond.Passed) or RestartIf(cond.Passed)",
            ["1000[107]"] = "GotoIf(cond.Passed)",
        };
        private readonly HashSet<string> noDetailsEnums = new HashSet<string>
        {
            "ConditionGroup", "Label", "ComparisonType",
            // In Elden Ring
            "ONOFF", "ONOFFCHANGE", "TargetEventFlagType", "InsideOutsideState", "ConditionState", "DeathState",
            "OwnershipState", "EventEndType", "DisabledEnabled"
        };

        private readonly StringBuilder sb;

        public EMEDF2HTML()
        {
            sb = new StringBuilder();
        }

        public void AppendEMEDF(InstructionDocs docs, string emevdDir = null)
        {
            string game = gameNames.Keys.FirstOrDefault(g => docs.ResourceString.StartsWith(g + "-common"));
            string gameName = game == null ? docs.ResourceString : gameNames[game];

            Dictionary<string, Usages> symbolUsages = new Dictionary<string, Usages>();
            bool showUsages = false;
            if (emevdDir != null)
            {
                symbolUsages = GetSymbolUsages(game, emevdDir, docs);
                showUsages = true;
                // Hack to merge PTDE and DS1R into one
                if (emevdDir.Contains("DARK SOULS REMASTERED"))
                {
                    string ptdeDir = emevdDir.Replace(@"DARK SOULS REMASTERED\event", @"Dark Souls Prepare to Die Edition\DATA\event");
                    if (ptdeDir != emevdDir)
                    {
                        Dictionary<string, Usages> ptdeUsages = GetSymbolUsages(game, ptdeDir, docs);
                        foreach (string symbol in symbolUsages.Keys.Union(ptdeUsages.Keys).ToList())
                        {
                            symbolUsages.TryGetValue(symbol, out Usages ds1rUse);
                            ptdeUsages.TryGetValue(symbol, out Usages ptdeUse);
                            symbolUsages[symbol] = Usages.Reconcile(ds1rUse, "in DS1R", ptdeUse, "in PTDE");
                        }
                    }
                }
            }
            Dictionary<string, string> extras = new Dictionary<string, string>();
            if (game == null)
            {
                // Extra info for easily comparing with other games/versions/etc
                AddExtraAnnotations(docs, extras, "ds3");
                AddExtraAnnotations(docs, extras, "er");
            }

            PageHeader(gameName + " EMEDF for DarkScript3", docs.Translator != null);

            string mainCondName(ConditionData.ConditionDoc condDoc)
            {
                if (condDoc.Name == "Compare") return condDoc.Name;
                string name = condDoc.AllBools.FirstOrDefault()?.Name;
                if (name != null) return name;
                name = condDoc.AllCompares.FirstOrDefault()?.Name;
                if (name != null) return name;
                return condDoc.Name;
            }

            // Instructions
            // Classes section
            // Instructions per xyz
            // For each instruction: one-line method signature, links to enums, equivalent conditions' one-line signatures,
            // equivalent fancy commands, usages
            BigSectionHeader("Instructions");
            foreach (EMEDF.ClassDoc classDoc in docs.DOC.Classes)
            {
                string className = $"{classDoc.Index} - {classDoc.Name}";
                SubHeader(className, className);
                foreach (EMEDF.InstrDoc instrDoc in classDoc.Instructions)
                {
                    string id = InstructionDocs.FormatInstructionID(classDoc.Index, instrDoc.Index);
                    string name = instrDoc.DisplayName;

                    InstructionTranslator.ConditionSelector condSelect = null;
                    docs.Translator?.Selectors.TryGetValue(id, out condSelect);
                    InstructionTranslator.ShortSelector shortSelect = null;
                    docs.Translator?.ShortSelectors.TryGetValue(id, out shortSelect);

                    List<string> tags = new List<string> { "instr" };
                    if (condSelect != null || (docs.Translator?.LabelDocs.ContainsKey(id) ?? false)) tags.Add("condinstr");
                    bool unused = showUsages && !symbolUsages.ContainsKey(name);
                    if (unused) tags.Add("unused");

                    Section(name, "Instruction " + id, tags, classDoc.Index != 1014, () =>
                    {
                        if (showUsages)
                        {
                            symbolUsages.TryGetValue(name, out Usages usages);
                            SectionUsageDetails(usages);
                        }
                        if (extras.TryGetValue(name, out string extra))
                        {
                            SectionExtra(extra);
                        }
                        FunctionSignature(name, instrDoc.Arguments.ToList(), instrDoc.OptionalArgs);
                        if (condSelect != null)
                        {
                            if (!condSelect.Cond.Hidden)
                            {
                                string condName = mainCondName(condSelect.Cond);
                                sb.Append($"<p>Condition function: <code>");
                                Link(condName, condName);
                                sb.AppendLine("</code></p>");
                            }
                            else if (specialCommands.TryGetValue(id, out string alt))
                            {
                                sb.Append($"<p><code>{Escape(alt)}</code> in MattScript</p>");
                            }
                        }
                        if (shortSelect != null)
                        {
                            List<InstructionTranslator.ShortVariant> shorts = shortSelect.Variants.Where(v => !v.Hidden).ToList();
                            if (shorts.Count > 0)
                            {
                                sb.AppendLine($"<p class=\"liststart cond\">Simpler version{(shorts.Count == 1 ? "" : "s")}:</p><ul class=\"condlist cond\">");
                                foreach (InstructionTranslator.ShortVariant v in shorts)
                                {
                                    ShortListItem(v, instrDoc);
                                }
                                sb.AppendLine("</ul>");
                            }
                        }

                    });
                }
            }
            BigSectionFooter();

            // Condition functions. Main head is first bool/compare if it exists
            if (docs.Translator != null)
            {
                BigSectionHeader("Condition Functions");
                // Reread it to get the original order and grouping and names, but use InstructionTranslator for everything else
                ConditionData conds = ConditionData.ReadStream("conditions.json");
                InstructionTranslator info = docs.Translator;
                // There are duplicate names for different games, so just bail out if the same name encountered again
                HashSet<string> condNames = new HashSet<string>();
                foreach (ConditionData.ConditionDoc storedCondDoc in conds.Conditions)
                {
                    if (storedCondDoc.Hidden || condNames.Contains(storedCondDoc.Name))
                    {
                        continue;
                    }
                    condNames.Add(storedCondDoc.Name);
                    if (!info.CondDocs.TryGetValue(storedCondDoc.Name, out InstructionTranslator.FunctionDoc baseDoc))
                    {
                        // Corresponding instructions do not exist in this game
                        continue;
                    }
                    // Make sure we have the right one for this game
                    ConditionData.ConditionDoc condDoc = baseDoc.ConditionDoc;
                    Usages usages = Usages.UnionAll(
                        baseDoc.Variants.Values
                            .Select(id => symbolUsages.TryGetValue(info.InstrDocs[id].DisplayName, out Usages instrUsages) ? instrUsages : null));

                    List<string> tags = new List<string> { "cond" };
                    bool unused = showUsages && usages == null;
                    if (unused) tags.Add("unused");

                    Section(mainCondName(condDoc), "Condition function", tags, true, () =>
                    {
                        if (showUsages)
                        {
                            SectionUsageDetails(usages);
                        }
                        FunctionSignature(condDoc.Name, baseDoc.Args, baseDoc.OptionalArgs);

                        int variantCount = condDoc.AllBools.Count + condDoc.AllCompares.Count;
                        if (variantCount > 0)
                        {
                            sb.AppendLine($"<p class=\"liststart\">Simpler version{(variantCount == 1 ? "" : "s")}:</p><ul class=\"condlist\">");
                            foreach (ConditionData.BoolVersion b in condDoc.AllBools)
                            {
                                BoolConditionListItem(b, info.CondDocs[b.Name], baseDoc);
                            }
                            foreach (ConditionData.CompareVersion c in condDoc.AllCompares)
                            {
                                CompareConditionListItem(c, info.CondDocs[c.Name], baseDoc);
                            }
                            sb.AppendLine("</ul>");
                        }
                    });
                }
                BigSectionFooter();
            }

            // Enums
            // Name, all values. exclude bools tho
            BigSectionHeader("Enums");
            foreach (EMEDF.EnumDoc enumDoc in docs.DOC.Enums)
            {
                if (enumDoc.Name == "BOOL") continue;

                string name = enumDoc.DisplayName;
                List<string> tags = new List<string> { "enum" };
                if (showUsages && !symbolUsages.ContainsKey(name)) tags.Add("unused");

                Section(name, "Enum", tags, true, () =>
                {
                    symbolUsages.TryGetValue(name, out Usages enumUsages);
                    if (showUsages)
                    {
                        SectionUsageDetails(enumUsages);
                    }
                    bool showDetails = !noDetailsEnums.Contains(name) && !enumDoc.DisplayValues.All(
                        e => symbolUsages.TryGetValue(e.Value, out Usages entryUsages) && Usages.Equals(entryUsages, enumUsages));
                    sb.AppendLine("<ul class=\"enumlist\">");
                    foreach (KeyValuePair<string, string> entry in enumDoc.DisplayValues)
                    {
                        string entryNum = entry.Key;
                        string entryName = entry.Value;
                        string unusedClass = showDetails && !symbolUsages.ContainsKey(entryName) ? " class=\"enumunused\"" : "";
                        sb.Append($"<li><code{unusedClass}>{entryName} = {entryNum}</code>");
                        if (showDetails && symbolUsages.TryGetValue(entryName, out Usages entryUsages))
                        {
                            sb.Append($" <span class=\"enumusage usageinfo\">Used in {entryUsages}</span>");
                        }
                        sb.AppendLine("</li>");
                    }
                    sb.AppendLine("</ul>");
                });
            }
            BigSectionFooter();

            PageFooter();
        }

        // Usages

        // Usages for display names of these symbols: instruction name, enum name, enum value
        private Dictionary<string, Usages> GetSymbolUsages(string game, string emevdDir, InstructionDocs docs)
        {
            Dictionary<string, HashSet<string>> symbolsByFile = new Dictionary<string, HashSet<string>>();
            HashSet<string> allSymbols = new HashSet<string>();
            game = game ?? "";

            interestingEmevds.TryGetValue(game, out Regex mainRegex);
            List<string> allFiles = new List<string>();
            Console.WriteLine($"------ Usages from [{emevdDir}]");
            foreach (string emevdPath in Directory.GetFiles(emevdDir))
            {
                if (mainRegex != null && !mainRegex.Match(Path.GetFileName(emevdPath)).Success) continue;
                string name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(emevdPath));
                allFiles.Add(name);
                // Console.WriteLine($"--- {name}");
                HashSet<string> symbols = symbolsByFile[name] = new HashSet<string>();
                EMEVD emevd = EMEVD.Read(emevdPath);
                foreach (EMEVD.Event evt in emevd.Events)
                {
                    for (int insIndex = 0; insIndex < evt.Instructions.Count; insIndex++)
                    {
                        // This is all very best-effort
                        EMEVD.Instruction ins = evt.Instructions[insIndex];
                        EMEDF.InstrDoc doc = docs.DOC[ins.Bank]?[ins.ID];
                        if (doc == null) continue;
                        symbols.Add(doc.DisplayName);
                        Dictionary<EMEVD.Parameter, string> paramNames = docs.ParamNames(evt);
                        try
                        {
                            // A slight abuse of this function, ignoring the returned list
                            docs.UnpackArgsWithParams(ins, insIndex, doc, paramNames, (argDoc, val) =>
                            {
                                if (argDoc.GetDisplayValue(val) is string displayStr)
                                {
                                    symbols.Add(displayStr);
                                }
                                return val;
                            });
                            // Also add a usage if the enum is present at all, even if parameterized
                            foreach (EMEDF.ArgDoc argDoc in doc.Arguments)
                            {
                                if (argDoc.EnumDoc != null && argDoc.EnumName != "BOOL")
                                {
                                    symbols.Add(argDoc.EnumDoc.DisplayName);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                allSymbols.UnionWith(symbols);
            }

            Dictionary<string, Usages> symbolUsages = new Dictionary<string, Usages>();
            List<string> primaryFiles = null;
            List<string> elidedFiles = new List<string>();
            if (secondaryEmevd.TryGetValue(game, out Regex secondaryRegex))
            {
                primaryFiles = allFiles.Where(f => !secondaryRegex.Match(f).Success).ToList();
            }
            if (elidedEmevd.TryGetValue(game, out Regex elidedRegex))
            {
                elidedFiles = allFiles.Where(f => elidedRegex.Match(f).Success).ToList();
            }
            foreach (string symbol in allSymbols)
            {
                List<string> matchFiles = allFiles.Where(f => symbolsByFile[f].Contains(symbol)).ToList();
                List<string> totalFiles = allFiles;
                if (primaryFiles != null)
                {
                    List<string> primaryMatchFiles = matchFiles.Intersect(primaryFiles).ToList();
                    if (primaryMatchFiles.Count > 0)
                    {
                        matchFiles = primaryMatchFiles;
                        totalFiles = primaryFiles;
                    }
                }
                // Combining PTDE and DS1R is done after.
                symbolUsages[symbol] = new Usages { Files = matchFiles, AllFiles = totalFiles, ElidedFiles = elidedFiles };
            }
            return symbolUsages;
        }

        public void AddExtraAnnotations(InstructionDocs newDocs, Dictionary<string, string> ret, string game)
        {
            // Cross-game comparisons for possible reversing purposes
            InstructionDocs oldDocs = new InstructionDocs($"{game}-common.emedf.json");
            foreach (EMEDF.ClassDoc classDoc in newDocs.DOC.Classes)
            {
                foreach (EMEDF.InstrDoc instrDoc in classDoc.Instructions)
                {
                    EMEDF.InstrDoc oldDoc = oldDocs.DOC[(int)classDoc.Index]?[(int)instrDoc.Index];
                    if (oldDoc != null)
                    {
                        string content = $"{oldDoc.DisplayName} in {game.ToUpper()}";
                        string prefix = ret.TryGetValue(instrDoc.DisplayName, out string s) ? $"{s}, " : "";
                        ret[instrDoc.DisplayName] = prefix + content;
                    }
                }
            }
        }

        private class Usages
        {
            public List<string> Files { get; set; }
            public List<string> AllFiles { get; set; }
            public List<string> ElidedFiles { get; set; }
            public string Restriction { get; set; }

            public static Usages Reconcile(Usages a, string asuf, Usages b, string bsuf)
            {
                if (a == null || b == null)
                {
                    Usages source = a ?? b;
                    return new Usages
                    {
                        Files = source.Files,
                        AllFiles = source.AllFiles,
                        Restriction = a != null ? asuf : bsuf,
                    };
                }
                if (a.Files.SequenceEqual(b.Files))
                {
                    return a;
                }
                if (!a.AllFiles.SequenceEqual(b.AllFiles)) throw new Exception("Incompatible set of all files");
                if (a.ElidedFiles.Count > 0) throw new Exception("No support for elided files and usage reconciliation");
                SortedSet<string> files = new SortedSet<string>(a.Files.Intersect(b.Files));
                foreach (string afile in a.Files.Except(b.Files))
                {
                    files.Add(afile + " " + asuf);
                }
                foreach (string bfile in b.Files.Except(a.Files))
                {
                    files.Add(bfile + " " + bsuf);
                }
                return new Usages
                {
                    Files = files.ToList(),
                    AllFiles = a.AllFiles
                };
            }

            public static Usages Union(Usages a, Usages b)
            {
                return new Usages
                {
                    Files = a.Files.Union(b.Files).OrderBy(f => f).ToList(),
                    AllFiles = a.AllFiles.Equals(b.AllFiles) || a.AllFiles.SequenceEqual(b.AllFiles) ? a.AllFiles : new List<string>(),
                    ElidedFiles = a.ElidedFiles,
                };
            }

            public static Usages UnionAll(IEnumerable<Usages> allUsages)
            {
                Usages usages = null;
                foreach (Usages subUsages in allUsages)
                {
                    if (usages == null)
                    {
                        usages = subUsages;
                    }
                    else if (subUsages != null)
                    {
                        usages = Union(usages, subUsages);
                    }
                }
                return usages;
            }

            public static bool Equals(Usages a, Usages b)
            {
                if (a == null || b == null) return (a == null) == (b == null);
                return a.Files.SequenceEqual(b.Files);
            }

            public override string ToString()
            {
                string restr = Restriction == null ? "" : $" ({Restriction} only)";
                if (AllFiles != null && AllFiles.Count > 0)
                {
                    if (AllFiles.Count == Files.Count)
                    {
                        return "all" + restr;
                    }
                    if (AllFiles.Count - Files.Count <= 2)
                    {
                        return "all but " + string.Join(" and ", AllFiles.Except(Files)) + restr;
                    }
                }
                if (ElidedFiles != null && ElidedFiles.Count > 0)
                {
                    List<string> alwaysReport = Files.Except(ElidedFiles).ToList();
                    int elidedCount = Files.Count - alwaysReport.Count;
                    if (elidedCount > 10)
                    {
                        if (alwaysReport.Count < 10)
                        {
                            List<string> sample = Files.Intersect(ElidedFiles).Take(10 - alwaysReport.Count).ToList();
                            alwaysReport.AddRange(sample);
                            elidedCount -= sample.Count;
                        }
                        if (elidedCount > 0)
                        {
                            return string.Join(", ", alwaysReport) + $", and {elidedCount} others" + restr;
                        }
                    }
                }
                return string.Join(", ", Files) + restr;
            }
        }

        // HMTL generators

        private void Section(string name, string info, List<string> tags, bool hasContents, Action text)
        {
            string classNames = string.Join(" ", tags);
            sb.AppendLine($"<section class=\"{classNames}\">");
            HeaderFunc(3, name, () =>
            {
                sb.Append("<code>");
                Text(name);
                sb.Append("</code>");
            },
            () =>
            {
                sb.Append($" <span class=\"sectioninfo\">{info}</span>");
            });
            if (hasContents)
            {
                sb.AppendLine($"<div class=\"sectioncontents\">");
                text();
                sb.Append($"</div>");
            }
            sb.AppendLine($"</section>");
        }

        private void SectionExtra(string extra)
        {
            sb.AppendLine($"<p class=\"sectionusage\">{Escape(extra)}</p>");
        }

        private void SectionUsageDetails(Usages usages)
        {
            if (usages == null)
            {
                sb.AppendLine($"<p class=\"sectionusage usageinfo sectionunused\">Unused</p>");
            }
            else
            {
                sb.AppendLine($"<p class=\"sectionusage usageinfo\">Used in {usages}</p>");
            }
        }

        private void FunctionSignature(string name, List<EMEDF.ArgDoc> args, int optCount)
        {
            sb.Append($"<pre>{Escape(name)}(");
            FunctionArguments(args, optCount, true);
            sb.Append($")</pre>");
        }

        private void FunctionArguments(List<EMEDF.ArgDoc> args, int optCount, bool multiLine)
        {
            for (int i = 0; i < args.Count; i++)
            {
                EMEDF.ArgDoc argDoc = args[i];
                bool optional = i >= args.Count - optCount;
                if (optional && i == args.Count - optCount)
                {
                    sb.Append("<span class=\"optarg\">");
                }
                if (i > 0)
                {
                    sb.Append(", ");
                }
                if (multiLine)
                {
                    sb.Append(Environment.NewLine + "    ");
                }
                string typeMod = argDoc.Vararg ? "..." : "";
                if (argDoc.EnumName == null)
                {
                    sb.Append($"{Escape(InstructionDocs.TypeString(argDoc.Type))}{typeMod} {Escape(argDoc.DisplayName)}");
                    if (optional && multiLine)
                    {
                        sb.Append($" = {argDoc.Default}");
                    }
                }
                else if (argDoc.EnumName == "BOOL")
                {
                    sb.Append($"bool{typeMod} {Escape(argDoc.DisplayName)}");
                    if (optional && multiLine)
                    {
                        sb.Append($" = {(argDoc.Default == 0 ? "false" : "true")}");
                    }
                }
                else if (argDoc.EnumDoc != null)
                {
                    // sb.Append($"enum{Escape("<")}");
                    sb.Append($"{Escape(InstructionDocs.TypeString(argDoc.Type))}{Escape("<")}");
                    Link(argDoc.EnumDoc.DisplayName, argDoc.EnumDoc.DisplayName);
                    sb.Append($"{Escape(">")}{typeMod} {Escape(argDoc.DisplayName)}");
                    if (optional && multiLine)
                    {
                        sb.Append($" = {argDoc.GetDisplayValue(argDoc.Default)}");
                    }
                }
                if (optional && i == args.Count - 1)
                {
                    sb.Append("</span>");
                }
            }
        }

        private void ShortListItem(InstructionTranslator.ShortVariant v, EMEDF.InstrDoc baseDoc)
        {
            sb.Append($"<li><code>{Escape(v.Name)}(");
            FunctionArguments(v.Args, v.OptionalArgs, false);
            sb.AppendLine($")</code>");

            List<string> details = new List<string>();
            // Can move this to standard location, though val2 is not used.
            string getReq(EMEDF.ArgDoc arg, object val, object val2)
            {
                string showVal(object a) => Escape(arg.GetDisplayValue(a).ToString());
                return $"<code>{Escape(arg.DisplayName)} = {showVal(val)}{(val2 == null ? "" : " or " + showVal(val2))}</code>";
            }
            for (int i = 0; i < baseDoc.Arguments.Count; i++)
            {
                if (v.ExtraArgs.TryGetValue(i, out int val))
                {
                    details.Add(getReq(baseDoc.Arguments[i], val, null));
                }
            }
            sb.AppendLine($"<br/><span class=\"conddetails\">Where <code>{string.Join(" and ", details)}</code></span></li>");
        }

        private void BoolConditionListItem(ConditionData.BoolVersion b, InstructionTranslator.FunctionDoc doc, InstructionTranslator.FunctionDoc baseDoc)
        {
            sb.Append($"<li><code>{Escape(doc.Name)}(");
            FunctionArguments(doc.Args, doc.OptionalArgs, false);
            sb.AppendLine($")</code>");

            EMEDF.ArgDoc negateArg = baseDoc.Args.Find(a => a.Name == doc.ConditionDoc.NegateField);
            EMEDF.EnumDoc negateEnum = doc.NegateEnum;
            List<string> details = new List<string>();
            string getReq(EMEDF.ArgDoc arg, object val, object val2)
            {
                string showVal(object v) => Escape(arg.GetDisplayValue(v).ToString());
                return $"<code>{Escape(arg.DisplayName)} = {showVal(val)}{(val2 == null ? "" : " or " + showVal(val2))}</code>";
            }
            if (b.Required != null)
            {
                foreach (ConditionData.FieldValue req in b.Required)
                {
                    EMEDF.ArgDoc reqArg = baseDoc.Args.Find(a => a.Name == req.Field);
                    if (reqArg != null)
                    {
                        details.Add(getReq(reqArg, req.Value, null));
                    }
                }
            }
            if (negateArg != null && negateEnum != null)
            {
                if (b.True != null)
                {
                    int trueNum = int.Parse(negateEnum.Values.FirstOrDefault(e => e.Value == b.True).Key);
                    if (b.False == null)
                    {
                        details.Add(getReq(negateArg, trueNum, InstructionTranslator.OppositeOp(doc.ConditionDoc, negateEnum, trueNum)));
                    }
                    else
                    {
                        int falseNum = int.Parse(negateEnum.Values.FirstOrDefault(e => e.Value == b.False).Key);
                        details.Add(getReq(negateArg, trueNum, falseNum));
                    }
                }
                else
                {
                    details.Add($"{Escape(negateArg.DisplayName)} = true or false");
                }
            }
            sb.AppendLine($"<br/><span class=\"conddetails\">Where <code>{string.Join(" and ", details)}</code></span></li>");
        }

        private void CompareConditionListItem(ConditionData.CompareVersion c, InstructionTranslator.FunctionDoc doc, InstructionTranslator.FunctionDoc baseDoc)
        {
            string ops = Escape("== != > < >= <=");
            if (c.Lhs != null)
            {
                // Prebake rather than showing Op()
                sb.AppendLine($"<li><code>{ops}</code>");
                sb.AppendLine($"<br/><span class=\"conddetails\">Comparing <code>leftHandSide</code> and <code>rightHandSize</code></span>");
                return;
            }
            sb.Append($"<li><code>{doc.Name}(");
            FunctionArguments(doc.Args, doc.OptionalArgs, false);
            sb.AppendLine($") <span class=\"condcomp\">== value</span></code>");

            EMEDF.ArgDoc compareArg = baseDoc.Args.Find(a => a.Name == c.Rhs);
            sb.Append("<br/><span class=\"conddetails\">");
            if (compareArg != null)
            {
                sb.Append($"Comparing <code>{Escape(compareArg.DisplayName)}</code> (<code>{ops}</code>)");
            }
            sb.AppendLine("</span></li>");
        }

        private void BigSectionHeader(string text)
        {
            sb.Append($"<div class=\"bigsection {Id(text).ToLowerInvariant()}\">");
            Header(1, text, text);
        }

        private void BigSectionFooter()
        {
            sb.Append("</div>");
        }

        private void SubHeader(string id, string text)
        {
            HeaderFunc(2, id, () => Text(text));
        }

        private void Header(int rank, string id, string text)
        {
            HeaderFunc(rank, id, () => Text(text));
        }

        private void HeaderFunc(int rank, string id, Action text, Action extraText = null)
        {
            id = Id(id);
            sb.Append($"<h{rank} id=\"{id}\"><a href=\"#{id}\" class=\"selfref\">");
            text();
            sb.Append($"</a>");
            extraText?.Invoke();
            sb.AppendLine($"</h{rank}>");
        }

        private void Link(string id, string text)
        {
            id = Id(id);
            sb.Append($"<a href=\"#{id}\">{Escape(text)}</a>");
        }

        private void Text(string text)
        {
            sb.Append(Escape(text));
        }

        private static string Escape(string text)
        {
            return WebUtility.HtmlEncode(text);
        }

        private static readonly Regex noIdChr = new Regex(@"[^\w\d\s]");
        private static readonly Regex idSep = new Regex(@" +");
        private static string Id(string name)
        {
            // It should be fine to have all names in the same namespace (otherwise, we'd need a prefix).
            return string.Join("_", idSep.Split(noIdChr.Replace(name, "")));
        }

        public override string ToString() => sb.ToString();

        private void PageHeader(string title, bool hasConditionFunctions)
        {
            sb.Append($@"
<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>{Escape(title)}</title>
");
            sb.Append(@"
<script>
let hist = {};
function showHide(clicked, show, hide, hidec) {
    if (show) {
        show.split(' ').forEach(c => {
            Array.from(document.getElementsByClassName(c)).forEach(el => {
                el.classList.remove(hidec);
            });
        });
    }
    if (hide) {
        hide.split(' ').forEach(c => {
            Array.from(document.getElementsByClassName(c)).forEach(el => {
                el.classList.add(hidec);
            });
        });
    }
    Array.from(clicked.closest('.showhide').getElementsByClassName('showbutton')).forEach(button => {
        button.classList.remove('showhighlight');
    });
    clicked.classList.add('showhighlight');
    if (clicked.id) {
        const last = parseInt(clicked.id.slice(-1));
        if (clicked.id.indexOf('def') >= 0) {
            delete hist[hidec];
        } else if (!isNaN(last)) {
            hist[hidec] = last;
        }
        const entries = Object.entries(hist);
        if (entries.length == 0) {
            window.history.replaceState(null, null, window.location.pathname);
        } else {
            window.history.replaceState(null, null, '?' + entries.map(([a, b]) => `${a}=${b}`).join('&'));
        }
    }
}
window.onload = function() {
    const url = new URL(window.location.href);
    ['hideunused', 'hideusageinfo', 'hidecond'].forEach(name => {
        const val = url.searchParams.get(name);
        if (val) {
            const button = document.getElementById(name + val);
            if (button) {
                button.onclick();
            }
        }
    });
};
</script>
<style>
body {
    font-family: 'Helvetica', 'Arial', sans-serif;
    margin: 0 20px;
}

a {
    color: #4D4DFF;
    text-decoration: none;
}

a:visited:not(.selfref) {
    color: #4D4DFF;
}

a:hover {
    text-decoration: underline;
}

a.selfref, a.selfref:visited {
    color: #000000;
}

code, pre {
    font-family: 'Monospace Font Hack', monospace;
}

button.showbutton {
    font-family: inherit!important;
    background: none!important;
    border: none;
    color: #000;
    cursor: pointer;
    padding: 0!important;
}

button.showhighlight {
    font-weight: bold;
}

.bigsection {
    background-color: #FDECF5;
    margin-bottom: 25px;
    padding: 5px 15px 15px 15px;
}

section {
    background-color: #F1FAFD;
}

.instructions h3 {
    background-color: #FFFFF0;
    padding: 10px;
}

.condition_functions h3 {
    background-color: #FFFFFF;
    padding: 10px;
}

.enums h3 {
    background-color: #E9DBF3;
    padding: 10px;
}

.sectioncontents {
    padding: 0 10px 5px 10px;
}

.sectionusage {
    color: #664A59;
    font-size: 80%;
}

.enumusage {
    color: #664A59;
    font-size: 80%;
    margin-left: 10px;
}

.sectionusage.sectionunused, .enumunused, .optarg {
    color: #999;
}

.conddetails {
    font-size: 90%;
    display: inline-block;
    margin: 5px 0 3px 15px;
}

.condcomp {
    color: #888;
    font-style: italic;
}

.simplelist {
    list-style-type: none;
    margin: 0;
    padding: 0;
}

.hideunused, .hideusageinfo, .hidecond {
    display: none;
}

.sectioninfo {
    font-size: 70%;
    margin-left: 10px;
}

</style>
</head>
<body>
");
            sb.AppendLine($"<h1>{Escape(title)}</h1>");
            sb.Append("<p class=\"toc\">");
            Link("Instructions", "Instructions");
            if (hasConditionFunctions)
            {
                sb.Append(" | ");
                Link("Condition Functions", "Condition Functions");
            }
            sb.Append(" | ");
            Link("Enums", "Enums");
            sb.AppendLine("</p>");

            sb.Append(@"
<p class=""showhide"">
<button id=""hideunused1"" class=""showbutton"" onclick=""showHide(this, '', 'unused', 'hideunused')"">Hide unused</button> |
<button id=""hideunuseddef"" class=""showbutton showhighlight"" onclick=""showHide(this, 'unused', '', 'hideunused')"">Show unused</button>");
            if (title.Contains("Bloodborne"))
            {
                sb.Append(" <span style=\"font-size: 80%;\">(analysis excludes Chalice Dungeons)</span>");
            }
            sb.Append(@"
</p>
<p class=""showhide"">
<button id=""hideusageinfo1"" class=""showbutton"" onclick=""showHide(this, '', 'usageinfo', 'hideusageinfo')"">Hide usage info</button> |
<button id=""hideusageinfodef"" class=""showbutton showhighlight"" onclick=""showHide(this, 'usageinfo', '', 'hideusageinfo')"">Show usage info</button>
</p>");
            if (hasConditionFunctions)
            {
                sb.Append(@"
<p class=""showhide"">
<button id=""hidecond1"" class=""showbutton"" onclick=""showHide(this, 'cond', 'condinstr', 'hidecond')"">Hide condition instructions</button> |
<button id=""hidecond2"" class=""showbutton"" onclick=""showHide(this, 'condinstr', 'cond', 'hidecond')"">Hide condition functions</button> |
<button id=""hideconddef"" class=""showbutton showhighlight"" onclick=""showHide(this, 'condinstr cond', '', 'hidecond')"">Show both</button>
</p>");
            }
            sb.AppendLine();
        }

        private void PageFooter()
        {
            sb.AppendLine("</body>");
        }
    }
}
