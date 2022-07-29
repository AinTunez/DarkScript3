using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using SoulsFormats;
using static DarkScript3.ConditionData;
using static DarkScript3.ScriptAst;
using static SoulsFormats.EMEVD.Instruction;

namespace DarkScript3
{
    public class InstructionTranslator
    {
        // Map from generic function name to function doc
        public Dictionary<string, FunctionDoc> CondDocs;
        // Map from x[y] to a condition selector for rewriting into a control flow structure
        public Dictionary<string, ConditionSelector> Selectors;
        // Map from short instruction name to doc
        public Dictionary<string, ShortVariant> ShortDocs;
        // Map from x[y] to short instruction selector.
        public Dictionary<string, ShortSelector> ShortSelectors;
        // Map from x[y] to the doc for the instruction. Stored for compilation purposes.
        public Dictionary<string, EMEDF.InstrDoc> InstrDocs;
        // Map from x[y] to a label num.
        public Dictionary<string, int> LabelDocs;
        // Map from alias name to function name
        public Dictionary<string, string> DisplayAliases;

        public class FunctionDoc
        {
            // The name of this particular condition function
            public string Name { get; set; }
            // Args for this condition function when it appears decompiled or in source.
            public List<EMEDF.ArgDoc> Args { get; set; }
            // The number of args at the end which are optional and may take on default values.
            // This is convenient shorthand at times, but it is necessary the case where different variants
            // have mismatching args, and only the cond variant always has all of them (InOutsideArea).
            public int OptionalArgs { get; set; }

            // For compilation
            // The ConditionDoc of this function, used for negating it, and for other documentation.
            public ConditionDoc ConditionDoc { get; set; }
            // The negate doc, also used for negating it, if negation/comparison is possible
            public EMEDF.EnumDoc NegateEnum { get; set; }
            // Possible UseVariants for this name, to figure out the exact command.
            // These should be consistent for every function with this ConditionDoc.
            // The other way, figuring out a condition function from a command, is the job of ConditionSelector.
            public Dictionary<ControlType, string> Variants = new Dictionary<ControlType, string>();
            // Extra bit for goto variant without cond/skip, to avoid transforming into if statement
            public bool GotoOnly { get; set; }
        }

        public static InstructionTranslator GetTranslator(InstructionDocs docs)
        {
            ConditionData conds;
            if (File.Exists("conditions.json"))
                conds = ReadFile("conditions.json");
            else if (File.Exists(@"Resources\conditions.json"))
                conds = ReadFile(@"Resources\conditions.json");
            else
                conds = ReadStream("conditions.json");

            List<string> games = conds.Games.Select(g => g.Name).ToList();
            string searchString = docs.ResourceString;
            // Temp hack for Elden Ring
            searchString = searchString.Replace("new-", "");
            string game = games.Find(g => searchString.StartsWith(g + "-common"));
            if (game == null)
            {
                return null;
            }

            EMEDF emedf = docs.DOC;
            // Mapping from instruction id, like 3[03] or 1003[11], to EMEDF doc
            // This is the shorthand used by the config.
            Dictionary<string, EMEDF.InstrDoc> instrs = emedf.Classes
                .SelectMany(c => c.Instructions.Select(i => (InstructionDocs.FormatInstructionID(c.Index, i.Index), i)))
                .ToDictionary(e => e.Item1, e => e.Item2);
            HashSet<string> condInstrs = new HashSet<string>(emedf.Classes
                .Where(c => c.Index < 2000 && c.Index != 1014)
                .SelectMany(c => c.Instructions.Select(i => InstructionDocs.FormatInstructionID(c.Index, i.Index))));

            // Account for a command. Each condition/control flow statement should have a unique treatment.
            HashSet<string> visited = new HashSet<string>();
            bool processInfo(string type, string instr)
            {
                if (instr == null)
                {
                    return false;
                }
                if (!instrs.TryGetValue(instr, out EMEDF.InstrDoc doc))
                {
                    return false;
                }
                // Console.WriteLine($"{type} {instr}: {doc.Name}");
                if (visited.Contains(instr)) throw new Exception($"{instr} appears twice in condition config for {game}");
                visited.Add(instr);
                return true;
            }
            foreach (string instr in conds.NoControlFlow)
            {
                processInfo("Excluded", instr);
            }
            int expectArg(string cmd, string name, object requiredType = null, int pos = -1)
            {
                EMEDF.InstrDoc doc = instrs[cmd];
                int arg;
                List<string> names = name.Split('|').ToList();
                if (pos >= 0)
                {
                    if (pos >= doc.Arguments.Length) throw new ArgumentException($"{doc.Name} doesn't have arg {pos}");
                    if (!names.Contains(doc.Arguments[pos].Name)) throw new ArgumentException($"{doc.Name} arg {pos} dooesn't have name {name}, has {string.Join(", ", doc.Arguments.Select(s => s.Name))}");
                    arg = pos;
                }
                else
                {
                    arg = doc.Arguments.ToList().FindIndex(a => names.Contains(a.Name));
                    if (arg == -1) throw new ArgumentException($"{doc.Name} doesn't have arg named {name}, has {string.Join(", ", doc.Arguments.Select(s => s.Name))}");
                }
                if (requiredType is string enumName)
                {
                    if (doc.Arguments[arg].EnumName != enumName) throw new ArgumentException($"{doc.Name} arg {name} has enum type {doc.Arguments[arg].EnumName}, not {enumName}");
                }
                else if (requiredType is ArgType argType)
                {
                    if (doc.Arguments[arg].Type != (long)argType) throw new ArgumentException($"{doc.Name} arg {name} has type {doc.Arguments[arg].Type}, not {argType}");
                }
                else if (requiredType != null) throw new Exception(requiredType.ToString());
                return arg;
            }
            // Indexed by function name
            Dictionary<string, FunctionDoc> condDocs = new Dictionary<string, FunctionDoc>();
            Dictionary<string, ShortVariant> shortDocs = new Dictionary<string, ShortVariant>();
            // Indexed by command id
            Dictionary<string, ConditionSelector> selectors = new Dictionary<string, ConditionSelector>();
            Dictionary<string, ShortSelector> shortSelectors = new Dictionary<string, ShortSelector>();
            int asInt(object obj) => int.Parse(obj.ToString());
            void addShortVariant(ShortSelector selector, ShortDoc shortDoc, ShortVersion v)
            {
                string cmd = shortDoc.Cmd;
                EMEDF.InstrDoc doc = instrs[cmd];
                ShortVariant variant = new ShortVariant
                {
                    Cmd = shortDoc.Cmd,
                    Name = v.Name,
                    Hidden = v.Hidden,
                };
                List<int> ignore = new List<int>();
                foreach (FieldValue req in v.Required)
                {
                    int reqArg = expectArg(cmd, req.Field);
                    variant.ExtraArgs[reqArg] = req.Value;
                    ignore.Add(reqArg);
                }
                variant.Args = doc.Arguments.Where((a, i) => !ignore.Contains(i)).ToList();
                // There is not an optional relationship between args and optional args, like there is with condition functions
                // (TODO maybe we can remove that now), so just do it at the end.
                if (shortDoc.OptFields != null)
                {
                    int optStart = variant.Args.Count - shortDoc.OptFields.Count;
                    bool matching = shortDoc.OptFields.Select((a, i) => a == variant.Args[optStart + i].Name).All(b => b);
                    if (matching)
                    {
                        variant.OptionalArgs = shortDoc.OptFields.Count;
                    }
                }
                selector.Variants.Add(variant);
                shortDocs[variant.Name] = variant;
            }
            void addVariants(ConditionSelector selector, ConditionDoc cond, ControlType use, string cmd, int control = -1)
            {
                if (selector.Variants.ContainsKey(cmd)) throw new Exception($"Already added variants of {cond.Name} for {cmd}");
                selector.Variants[cmd] = new List<ConditionVariant>();
                selectors[cmd] = selector;
                EMEDF.InstrDoc doc = instrs[cmd];
                int negateArg = -1;
                string negateName = cond.IsCompare ? "Comparison Type" : cond.NegateField;
                EMEDF.EnumDoc negateDoc = null;
                if (negateName != null)
                {
                    negateArg = expectArg(cmd, negateName, cond.IsCompare ? "Comparison Type" : null);
                    string negateEnum = doc.Arguments[negateArg].EnumName;
                    // Pretend these are compatible
                    if (negateEnum == "ON/OFF") negateEnum = "ON/OFF/CHANGE";
                    negateDoc = emedf.Enums.Where(d => d.Name == negateEnum).FirstOrDefault();
                    if (negateDoc == null) throw new ArgumentException($"Command {cmd} for cond {cond.Name} at arg {negateArg} uses enum which does not exist");
                    if (selector.NegateEnum == null)
                    {
                        selector.NegateEnum = negateDoc;
                    }
                    else if (selector.NegateEnum.Name != negateEnum)
                    {
                        throw new ArgumentException($"Command {cmd} for {cond.Name} has negate enum {negateEnum} but {selector.NegateEnum.Name} was already used in a different command");
                    }
                }
                void addVariant(BoolVersion bv = null, CompareVersion cv = null)
                {
                    ConditionVariant variant = new ConditionVariant { Variant = use };
                    string name = cond.Name;
                    List<int> ignore = new List<int>();
                    if (control >= 0)
                    {
                        variant.ControlArg = control;
                        ignore.Add(control);
                    }
                    if (negateArg >= 0 && (cv != null || bv != null))
                    {
                        variant.NegateArg = negateArg;
                        ignore.Add(negateArg);
                    }
                    if (bv != null)
                    {
                        name = bv.Name;
                        if (negateDoc == null) throw new ArgumentException($"Cond {cond.Name} has boolean variant {name} but no negate_field");
                        string trueVal = negateDoc.Name == "BOOL" ? "TRUE" : bv.True;
                        string trueKey = negateDoc.Values.Where(e => e.Value == trueVal).FirstOrDefault().Key;
                        if (trueKey == null) throw new Exception($"Cond {cond.Name} has no given true value for {name} {negateDoc.Name}");
                        variant.TrueOp = asInt(trueKey);
                        if (bv.Required != null)
                        {
                            foreach (FieldValue req in bv.Required)
                            {
                                int reqArg = expectArg(cmd, req.Field);
                                variant.ExtraArgs[reqArg] = req.Value;
                                ignore.Add(reqArg);
                            }
                        }
                    }
                    else if (cv != null)
                    {
                        name = cv.Name;
                        if (cv.Rhs == null) throw new ArgumentException($"Cond {cond.Name} has compare variant {name} with no RHS specified");
                        variant.CompareArg = expectArg(cmd, cv.Rhs);
                        ignore.Add(variant.CompareArg);
                        if (cv.Lhs != null)
                        {
                            variant.CompareArg2 = expectArg(cmd, cv.Lhs);
                            ignore.Add(variant.CompareArg2);
                        }
                    }
                    if (name == null) throw new ArgumentException($"Name for {cond.Name} and {cmd} is missing");
                    EMEDF.ArgDoc optArgDoc = null;
                    // Optional args serve two purposes: to hide noisy default values, and to address inconsistencies between variants.
                    // In all cases, they can be established from the COND variant.
                    if (use == ControlType.COND && cond.OptFields != null)
                    {
                        bool matching = cond.OptFields.Select((a, i) => a == doc.Arguments[doc.Arguments.Length - cond.OptFields.Count + i].Name).All(b => b);
                        // Missing opt args can happen when "# of target character"-type arguments get added over time.
                        // This is mostly a pretty-printing feature. so it might be tedious to specify each individual change between games.
                        if (matching)
                        {
                            int optArg = doc.Arguments.Length - cond.OptFields.Count;
                            if (ignore.Contains(optArg)) throw new Exception($"Optional arg {cond.OptFields[0]} is not part of the normal argument list for {name} with {cmd}");
                            optArgDoc = doc.Arguments[optArg];
                        }
                    }
                    // Non-control-flow arguments should match for all uses of a condition, across goto/skip/etc. commands
                    // (other than for optional args)
                    List<EMEDF.ArgDoc> condArgs = doc.Arguments.Where((a, i) => !ignore.Contains(i)).ToList();
                    if (condDocs.TryGetValue(name, out FunctionDoc condDoc))
                    {
                        string older = string.Join(", ", condDoc.Args.Select(a => a.Name));
                        string newer = string.Join(", ", condArgs.Select(a => a.Name));
                        bool matching = older == newer;
                        if (!matching && condDoc.OptionalArgs > 0)
                        {
                            // This is only permissible if the existing definition has optional args, in which case the shared segment should still match.
                            older = string.Join(", ", condDoc.Args.Take(condDoc.Args.Count - condDoc.OptionalArgs).Select(a => a.Name));
                            matching = older == newer;
                        }
                        if (!matching)
                        {
                            throw new Exception($"Multiple possible definitions found for {name}: [{older}] existing vs [{newer}] for {cmd}. opt args {condDoc.OptionalArgs}");
                        }
                    }
                    else
                    {
                        condDocs[name] = condDoc = new FunctionDoc
                        {
                            Name = name,
                            Args = condArgs,
                            ConditionDoc = cond,
                            NegateEnum = negateDoc,
                        };
                        if (optArgDoc != null)
                        {
                            condDoc.OptionalArgs = condArgs.Count - condArgs.IndexOf(optArgDoc);
                        }
                    }
                    condDoc.Variants[use] = cmd;
                    variant.Doc = condDoc;
                    selector.Variants[cmd].Add(variant);
                }
                foreach (BoolVersion version in cond.AllBools)
                {
                    addVariant(bv: version);
                }
                foreach (CompareVersion version in cond.AllCompares)
                {
                    addVariant(cv: version);
                }
                addVariant();
            }
            foreach (ShortDoc shortDoc in conds.Shorts ?? new List<ShortDoc>())
            {
                if (shortDoc.Games != null && !shortDoc.Games.Contains(game)) continue;
                if (!processInfo("Short", shortDoc.Cmd)) continue;
                EMEDF.InstrDoc doc = instrs[shortDoc.Cmd];
                ShortSelector selector = new ShortSelector { Cmd = shortDoc.Cmd };
                shortSelectors[shortDoc.Cmd] = selector;
                if (shortDoc.OptFields != null)
                {
                    // Unlike condition docs, this length is always fixed.
                    int optStart = doc.Arguments.Length - shortDoc.OptFields.Count;
                    bool matching = shortDoc.OptFields.Select((a, i) => a == doc.Arguments[optStart + i].Name).All(b => b);
                    if (matching)
                    {
                        doc.OptionalArgs = shortDoc.OptFields.Count;
                    }
                }
                if (shortDoc.Enable != null)
                {
                    EMEDF.ArgDoc enableDoc = doc.Arguments.Where(a => a.EnumName == "Disabled/Enabled").FirstOrDefault();
                    if (enableDoc == null) throw new Exception($"No Disabled/Enabled field found to make short version for {shortDoc.Cmd} {doc.Name}");
                    if (shortDoc.Shorts == null) shortDoc.Shorts = new List<ShortVersion>();
                    shortDoc.Shorts.Add(ShortVersion.ForCustomField($"Disable{shortDoc.Enable}", enableDoc.Name, 0));
                    shortDoc.Shorts.Add(ShortVersion.ForCustomField($"Enable{shortDoc.Enable}", enableDoc.Name, 1));
                }
                if (shortDoc.Shorts == null) continue;
                foreach (ShortVersion version in shortDoc.Shorts)
                {
                    addShortVariant(selector, shortDoc, version);
                }
            }
            foreach (ConditionDoc cond in conds.Conditions)
            {
                ConditionSelector selector = new ConditionSelector { Cond = cond };
                if (cond.Games != null && !cond.Games.Contains(game)) continue;
                if (processInfo($"Cond {cond.Name}", cond.Cond))
                {
                    // The cond variant should go first, as it can have a superset of other variants' args if marked in the config.
                    expectArg(cond.Cond, "Result Condition Group", "Condition Group", 0);
                    addVariants(selector, cond, ControlType.COND, cond.Cond, 0);
                }
                if (processInfo($"Skip {cond.Name}", cond.Skip))
                {
                    expectArg(cond.Skip, "Number Of Skipped Lines", ArgType.Byte, 0);
                    addVariants(selector, cond, ControlType.SKIP, cond.Skip, 0);
                }
                if (processInfo($"End  {cond.Name}", cond.End))
                {
                    expectArg(cond.End, "Execution End Type", "Event End Type", 0);
                    addVariants(selector, cond, ControlType.END, cond.End, 0);
                }
                if (processInfo($"Goto {cond.Name}", cond.Goto))
                {
                    expectArg(cond.Goto, "Label", "Label", 0);
                    addVariants(selector, cond, ControlType.GOTO, cond.Goto, 0);
                }
                if (processInfo($"Wait {cond.Name}", cond.Wait))
                {
                    // Implicit main arg
                    addVariants(selector, cond, ControlType.WAIT, cond.Wait);
                }
#if DEBUG
                if (game == "er")
                {
                    // Some extra consistency checking here, mainly for development sanity purposes.
                    // It's not actually necessary for parsing the config.
                    Dictionary<string, string> namePrefixes = new Dictionary<string, string>
                    {
                        [""] = cond.Cond,
                        ["SKIP "] = cond.Skip,
                        ["END "] = cond.End,
                        ["GOTO "] = cond.Goto,
                    };
                    string baseName = null;
                    foreach (KeyValuePair<string, string> check in namePrefixes)
                    {
                        string cmd = check.Value;
                        if (cmd == null || !instrs.TryGetValue(cmd, out EMEDF.InstrDoc doc)) continue;
                        string name = doc.Name;
                        if (name == "IF Condition Group" || name.Contains("Parameter Comparison")) continue;
                        string pref = check.Key;
                        if (!name.StartsWith(pref)) throw new Exception($"Expected prefix [{pref}] for {cmd} {name}");
                        string norm = name.Substring(pref.Length);
                        if (baseName == null)
                        {
                            baseName = norm;
                        }
                        else if (baseName != norm)
                        {
                            throw new Exception($"Name {cmd} {name} mismatches equivalent condition name {baseName} ({string.Join(" ", namePrefixes)})");
                        }
                    }
                }
#endif
            }
            string undocError = string.Join(", ", condInstrs.Where(i => !visited.Contains(i)).Select(i => $"{i}:{instrs[i].Name}"));
            if (undocError.Length > 0)
            {
                // This doesn't have to be an error, but it does mean that condition group decompilation is impossible when these commands are present.
                throw new ArgumentException($"Present in emedf but not condition config for {game}: {undocError}");
            }
            Dictionary<string, int> labels = new Dictionary<string, int>();
            EMEDF.ClassDoc labelClass = emedf[1014];
            if (labelClass != null)
            {
                labels = labelClass.Instructions.ToDictionary(i => InstructionDocs.FormatInstructionID(1014, i.Index), i => (int)i.Index);
            }
            Dictionary<string, string> aliases = new Dictionary<string, string>();
            if (conds.Aliases != null)
            {
                List<string> noReal = conds.Aliases.Keys.Intersect(condDocs.Keys).ToList();
                if (noReal.Count > 0)
                {
                    throw new Exception($"Condition aliases match actual condition names: {string.Join(", ", noReal)}");
                }
                // Not all targets may exist in all games
                aliases = conds.Aliases.Where(e => condDocs.ContainsKey(e.Value)).ToDictionary(e => e.Key, e => e.Value);
            }
            foreach (FunctionDoc condDoc in condDocs.Values)
            {
                condDoc.GotoOnly = condDoc.Variants.ContainsKey(ControlType.GOTO)
                    && !condDoc.Variants.ContainsKey(ControlType.COND)
                    && !condDoc.Variants.ContainsKey(ControlType.SKIP);
            }
            return new InstructionTranslator
            {
                CondDocs = condDocs,
                Selectors = selectors,
                ShortDocs = shortDocs,
                ShortSelectors = shortSelectors,
                InstrDocs = instrs,
                LabelDocs = labels,
                DisplayAliases = aliases,
            };
        }

        private ConditionVariant GetVariantByName(string docName, string cmd)
        {
            ConditionSelector selector = Selectors[cmd];
            ConditionVariant variant = selector.Variants[cmd].Find(f => f.Doc.Name == docName);
            if (variant == null) throw new Exception($"Internal error: no variant for {docName} and {cmd}");
            return variant;
        }

        // As part of instruction compilation, rewrite all instruction so that they correspond to valid emevd commands.
        // But don't convert them into instructions yet, since we need to edit control things like condition group register allocation and line skipping.
        public List<Intermediate> ExpandCond(Intermediate im, Func<string> newVar)
        {
            if (im is Instr || im is Label || im is NoOp || im.IsMeta)
            {
                return new List<Intermediate> { im };
            }
            else if (im is CondIntermediate condIm)
            {
                // Either use a direct command, or require a condition group because of negation or missing variant (or, in future, optional args).
                // Some commands do not support condition groups:
                // - goto/skip/end only: CompareCompiledConditionGroup, CompareNumberOfCoopClients, CompareNumberOfCoopClients
                // - goto only: HollowArenaMatchType
                Cond cond = condIm.Cond;
                if (cond == null)
                {
                    return new List<Intermediate> { im };
                }
                else if (cond is ErrorCond)
                {
                    // Just quit out here. Error is already recorded elsewhere
                    return new List<Intermediate>();
                }
                else if (cond is OpCond) throw new Exception($"Internal error: should have expanded out all subconditions in {im}");

                string docName = cond.DocName;
                if (!CondDocs.TryGetValue(docName, out FunctionDoc functionDoc)) throw new Exception($"Internal error: Unknown condition function {docName}");
                ControlType type = condIm.ControlType;
                // The only non-synthetic use of WAIT is in condition groups, and these can just be converted to main group eval with no behavior change.
                if (type == ControlType.WAIT) type = ControlType.COND;
                bool indirection = false;
                if (functionDoc.Variants.ContainsKey(type))
                {
                    // If variant exists, see if it can be used
                    ConditionVariant mainVariant = GetVariantByName(docName, functionDoc.Variants[type]);
                    if (cond.Negate && mainVariant.NegateArg == -1)
                    {
                        if (!functionDoc.Variants.ContainsKey(ControlType.COND))
                        {
                            throw new FancyNotSupportedException($"Can't use {docName} in this form since it does't have a condition version. Add or remove negation so that it can be translated to emevd.", im);
                        }
                        // Console.WriteLine($"Expanding {im} because it can't be negated as {type}");
                        indirection = true;
                    }
                }
                else
                {
                    // Needed for DS1, which only has skip. The instruction is rewritten later with # of lines if the label is synthetic.
                    if (type == ControlType.GOTO && functionDoc.Variants.ContainsKey(ControlType.SKIP))
                    {
                        // The type is ControlType.SKIP in this case, which will be accounted for once SkipLines is filled in.
                    }
                    else if (functionDoc.Variants.ContainsKey(ControlType.COND))
                    {
                        indirection = true;
                    }
                    else
                    {
                        // Also part of error: can't use compiled groups in condition group.
                        throw new FancyNotSupportedException($"Can't use statement type {type} with {functionDoc.Name}; can only use it with {string.Join(", ", functionDoc.Variants.Keys)}", im);
                    }
                }
                if (indirection)
                {
                    string var = newVar() + "z";
                    ConditionVariant cmdVariant = GetVariantByName(docName, functionDoc.Variants[ControlType.COND]);
                    CondRef tmpCond = new CondRef { Compiled = false, Name = var };
                    if (cond.Negate && cmdVariant.NegateArg == -1)
                    {
                        cond.Negate = false;
                        tmpCond.Negate = true;
                    }
                    Intermediate instr = new CondAssign { Cond = cond, Op = CondAssignOp.Assign, ToVar = var, Labels = condIm.Labels };

                    // Note that this is destructive. If this becomes a problem with multiple references that should
                    // remain divergent, conditions may need some deep cloning routines.
                    condIm.Cond = tmpCond;
                    condIm.Labels = new List<string>();
                    return new List<Intermediate> { instr, condIm };
                }
                else
                {
                    return new List<Intermediate> { condIm };
                }
            }
            else throw new Exception($"Internal error: unable to compile unknown instruction {im}");
        }

        // As part of instruction compilation, rewrite all instruction so that they correspond to valid emevd commands.
        // But don't convert them into instructions yet, since we need to edit control things like condition group register allocation and line skipping.
        public Instr CompileCond(Intermediate im)
        {
            // Mainly NoOp and JSStatement shouldn't be passed in here
            if (im is Instr imInstr)
            {
                // Can do more Instr validation here, though it's mainly just required here for short variants.
                // If short variants are defined in JS, they strictly speaking don't need any transformation at all... that may be preferable.
                if (ShortDocs.TryGetValue(imInstr.Name, out ShortVariant variant))
                {
                    string cmd = variant.Cmd;
                    EMEDF.InstrDoc instrDoc = InstrDocs[cmd];
                    Instr instr = new Instr
                    {
                        Cmd = cmd,
                        Name = instrDoc.DisplayName,
                        Args = Enumerable.Repeat((object)null, instrDoc.Arguments.Length).ToList(),
                        Layers = imInstr.Layers,
                    };
                    variant.SetInstrArgs(instr, imInstr);
                    return instr;
                }
                else if (InstrDocs.TryGetValue(imInstr.Cmd, out EMEDF.InstrDoc optDoc)
                    && optDoc.OptionalArgs > 0 && imInstr.Args.Count < optDoc.Arguments.Length)
                {
                    // Make a defensive copy (should use clone?)
                    int missingArgs = optDoc.Arguments.Length - imInstr.Args.Count;
                    return new Instr
                    {
                        Cmd = imInstr.Cmd,
                        Name = imInstr.Name,
                        Args = imInstr.Args.Concat(PadOptionalDefaultArgs(optDoc.Arguments, missingArgs, optDoc.OptionalArgs)).ToList(),
                        Layers = imInstr.Layers,
                    };
                }
                return imInstr;
            }
            else if (im is Label label)
            {
                string cmd = InstructionDocs.FormatInstructionID(1014, label.Num);
                return new Instr { Name = $"Label{label.Num}", Cmd = cmd, Args = new List<object>() };
            }
            else if (im is CondIntermediate condIm)
            {
                Cond cond = condIm.Cond;
                if (cond is ErrorCond)
                {
                    // Just quit out here. Error is already recorded elsewhere
                    return null;
                }
                else if (cond is OpCond)
                {
                    throw new Exception($"Internal error: should have expanded out all subconditions in {im}");
                }

                // The only non-synthetic use of WAIT is in condition groups, and these can just be converted to main group eval with no behavior change.
                ControlType type = condIm.ControlType;
                if (type == ControlType.WAIT) type = ControlType.COND;

                string docName = cond.DocName;
                FunctionDoc functionDoc = CondDocs[docName];

                if (!functionDoc.Variants.ContainsKey(type))
                {
                    string errName = cond.Always ? $"unconditional {type}" : $"{type} {docName}";
                    StringBuilder sb = new StringBuilder();
                    sb.Append($"Compiling {errName} is not supported.");
                    sb.Append($" Only [{string.Join(", ", functionDoc.Variants.Keys)}] are supported.");
                    if (type == ControlType.GOTO && functionDoc.Variants.ContainsKey(ControlType.SKIP))
                    {
                        sb.Append($" Try using a synthetic label instead.");
                    }
                    throw new FancyNotSupportedException(sb.ToString(), im);
                }
                string cmd = functionDoc.Variants[type];
                ConditionVariant variant = GetVariantByName(docName, cmd);
                EMEDF.InstrDoc instrDoc = InstrDocs[cmd];

                Instr instr = new Instr
                {
                    Cmd = cmd,
                    Name = instrDoc.DisplayName,
                    Args = Enumerable.Repeat((object)null, instrDoc.Arguments.Length).ToList(),
                };
                // Re-expose error (such as Goto in bad state) alongside instruction
                object controlVal = condIm.ControlArg;
                int negateVal = cond is CompareCond cmp ? (int)cmp.Type : variant.TrueOp;
                if (cond != null && cond.Negate)
                {
                    if (functionDoc.NegateEnum == null) throw new FancyNotSupportedException($"No way to negate {functionDoc.Name} compiling {docName} to {instr.Name}", im);
                    negateVal = OppositeOp(functionDoc.ConditionDoc, functionDoc.NegateEnum, negateVal);
                }
                variant.SetInstrArgs(instr, cond, controlVal, negateVal);
                return instr;
            }
            else throw new Exception($"Internal error: unable to compile unknown instruction {im}");
        }

        public Intermediate DecompileCond(Instr instr)
        {
            if (Selectors.TryGetValue(instr.Cmd, out ConditionSelector selector))
            {
                if (instr.Layers != null) throw new FancyNotSupportedException($"Control flow instruction with layers is not supported", instr);
                ConditionVariant variant = selector.GetVariant(instr);
                return variant.ExtractInstrArgs(instr);
            }
            else if (LabelDocs.TryGetValue(instr.Cmd, out int label))
            {
                if (instr.Layers != null) throw new FancyNotSupportedException($"Control flow instruction with layers is not supported", instr);
                return new Label { Num = label };
            }
            else if (ShortSelectors.TryGetValue(instr.Cmd, out ShortSelector shortSelector))
            {
                ShortVariant variant = shortSelector.GetVariant(instr);
                if (variant != null)
                {
                    return variant.ExtractInstrArgs(instr);
                }
            }
            if (InstrDocs.TryGetValue(instr.Cmd, out EMEDF.InstrDoc instrDoc) && instrDoc.OptionalArgs > 0)
            {
                HideOptionalDefaultArgs(instr.Args, instrDoc.Arguments, instrDoc.OptionalArgs);
            }
            return instr;
        }

        public ControlType? GetInstrType(Instr instr)
        {
            if (Selectors.TryGetValue(instr.Cmd, out ConditionSelector selector))
            {
                return selector.GetBasicType(instr);
            }
            return null;
        }

        private static bool TryExtractIntArg(object arg, out int val)
        {
            object innerVal = arg;
            if (innerVal is DisplayArg disp)
            {
                innerVal = disp.Value;
            }
            return int.TryParse(innerVal.ToString(), out val);
        }

        private static int ExtractIntArg(Instr instr, int index)
        {
            if (TryExtractIntArg(instr.Args[index], out int val))
            {
                return val;
            }
            throw new FancyNotSupportedException($"Required plain integer or enum for decompilation but found {instr.Args[index]}", instr);
        }

        private static void HideOptionalDefaultArgs(List<object> cmdArgs, IList<EMEDF.ArgDoc> docArgs, int optionalArgs)
        {
            // Hide default optional arguments here, all-or-nothing. This makes for nicer output.
            if (optionalArgs > 0)
            {
                int hidable;
                for (hidable = 0; hidable < optionalArgs; hidable++)
                {
                    int pos = docArgs.Count - 1 - hidable;
                    // If arg exists and is default value, it can be hidden
                    if (pos >= cmdArgs.Count) break;
                    if (!TryExtractIntArg(cmdArgs[pos], out int arg) || arg.ToString() != docArgs[pos].Default.ToString()) break;
                }
                if (hidable == optionalArgs)
                {
                    cmdArgs.RemoveRange(docArgs.Count - optionalArgs, optionalArgs);
                }
            }
        }

        private static IEnumerable<object> PadOptionalDefaultArgs(IList<EMEDF.ArgDoc> docArgs, int missingArgs, int optionalArgs)
        {
            if (missingArgs > 0 && missingArgs <= optionalArgs && missingArgs <= docArgs.Count)
            {
                return Enumerable.Range(docArgs.Count - missingArgs, missingArgs).Select(i => (object)docArgs[i].Default);
            }
            return new object[] { };
        }

        // A specific pair of function and command.
        public class ConditionVariant
        {
            // The doc for this variant. Used for name and default args.
            public FunctionDoc Doc { get; set; }
            // Which variant of the function these args correspond to
            public ControlType Variant { get; set; }
            // If present, the arg for lines to skip, or label to goto, or condition group to use, or ending type.
            public int ControlArg = -1;
            // The arg used for negation or comparison, turned into embedded syntax easier-to-read expressions.
            public int NegateArg = -1;
            // The arg used for comparison, displayed on the right hand side.
            public int CompareArg = -1;
            // For direct integer comparisons only, the arg displayed on the left hand side.
            public int CompareArg2 = -1;
            // For this function, the negate arg value which means true, if negate arg is present.
            public int TrueOp = -1;
            // Any additional args which have an assumed value. Should be checked in ConditionSelector.
            public Dictionary<int, int> ExtraArgs = new Dictionary<int, int>();

            public void SetInstrArgs(Instr instr, Cond cond, object controlVal, int negateVal)
            {
                // Some args are filled in as we go, others are added into extraArgs to be zipped with missing spots at the end.
                List<object> extraArgs = new List<object>();
                if (cond is CondRef condRef)
                {
                    // This variant assumes the group is fixed and negation is implied
                    extraArgs.Add(condRef.Group);
                }
                else if (cond is CmdCond cmdCond)
                {
                    // This variant is just a function call, potentially with negation.
                    extraArgs.AddRange(cmdCond.Args);
                }

                if (ControlArg >= 0)
                {
                    instr.Args[ControlArg] = controlVal;
                }
                if (CompareArg >= 0)
                {
                    if (!(cond is CompareCond cmp))
                    {
                        throw new InstructionTranslationException($"{cond} is only allowed on the left-hand side of a comparison operator");
                    }
                    instr.Args[NegateArg] = negateVal;  // Should be cmp.Type but changed as appropriate
                    instr.Args[CompareArg] = cmp.Rhs;
                    if (CompareArg2 >= 0)
                    {
                        if (cmp.Lhs == null) throw new Exception($"Internal error: selected operator-based {Doc.Name} for {cond}");
                        instr.Args[CompareArg2] = cmp.Lhs;
                    }
                    else
                    {
                        if (cmp.CmdLhs == null) throw new Exception($"Internal error: selected command-based {Doc.Name} for {cond}");
                        extraArgs.AddRange(cmp.CmdLhs.Args);
                    }
                }
                else if (NegateArg >= 0)
                {
                    instr.Args[NegateArg] = negateVal;
                }
                foreach (KeyValuePair<int, int> req in ExtraArgs)
                {
                    instr.Args[req.Key] = req.Value;
                }
                // All control/fixed args have been filled by now, now just to fill in user-specified ones (extraArgs).
                // These should match one-for-one with missing instr.Args, except in the case of optional args, which can pad extraArgs.
                List<int> emptyArgIndices = instr.Args.Select((a, i) => (a, i)).Where(e => e.Item1 == null).Select(e => e.Item2).ToList();
                if (emptyArgIndices.Count != extraArgs.Count)
                {
                    // If there are optional args, add defaults to extra args
                    int missingArgs = emptyArgIndices.Count - extraArgs.Count;
                    if (missingArgs > 0 && missingArgs <= Doc.OptionalArgs)
                    {
                        extraArgs.AddRange(PadOptionalDefaultArgs(Doc.Args, missingArgs, Doc.OptionalArgs));
                        // Doc.Args.GetRange(Doc.Args.Count - missingArgs, missingArgs).Select(argDoc => (object)argDoc.Default));
                    }
                    // This should be accounted for in JS compiler check, before getting to here.
                    else throw new Exception($"Have ({string.Join(", ", extraArgs)}) from {cond} to fit into {instr} but mismatch in count");
                }
                for (int i = 0; i < emptyArgIndices.Count; i++)
                {
                    instr.Args[emptyArgIndices[i]] = extraArgs[i];
                }
            }

            public CondIntermediate ExtractInstrArgs(Instr instr)
            {
                CmdCond cmd = new CmdCond { Name = Doc.Name };
                if (instr.Layers != null) throw new ArgumentException($"Cannot decompile {instr} because it's a control flow instruction with a layer");
                List<int> ignore = new List<int>();
                if (ControlArg >= 0)
                {
                    ignore.Add(ControlArg);
                    if (instr.Args[ControlArg] is ParamArg)
                    {
                        throw new FancyNotSupportedException($"Control arg {ControlArg} in comes from event params, cannot decompile", instr);
                    }
                }
                Cond retCond = cmd;
                if (CompareArg >= 0)
                {
                    ignore.Add(NegateArg);
                    ignore.Add(CompareArg);
                    CompareCond cmp = new CompareCond();
                    cmp.Type = (ComparisonType)ExtractIntArg(instr, NegateArg);
                    cmp.Rhs = instr.Args[CompareArg];
                    if (CompareArg2 >= 0)
                    {
                        ignore.Add(CompareArg2);
                        cmp.Lhs = instr.Args[CompareArg2];
                        // All args should be ignored at this point
                    }
                    else
                    {
                        cmp.CmdLhs = cmd;
                    }
                    retCond = cmp;
                }
                else if (NegateArg >= 0)
                {
                    ignore.Add(NegateArg);
                    cmd.Negate = ExtractIntArg(instr, NegateArg) != TrueOp;
                }
                foreach (KeyValuePair<int, int> req in ExtraArgs)
                {
                    ignore.Add(req.Key);
                }
                for (int i = 0; i < instr.Args.Count; i++)
                {
                    if (!ignore.Contains(i))
                    {
                        cmd.Args.Add(instr.Args[i]);
                    }
                }
                HideOptionalDefaultArgs(cmd.Args, Doc.Args, Doc.OptionalArgs);
                CondIntermediate ret;
                if (Variant == ControlType.COND)
                {
                    int reg = ExtractIntArg(instr, ControlArg);
                    ret = new CondAssign
                    {
                        Cond = retCond,
                        ToCond = reg,
                        Op = reg == 0 ? CondAssignOp.Assign : (reg > 0 ? CondAssignOp.AssignAnd : CondAssignOp.AssignOr),
                    };
                }
                else if (Variant == ControlType.SKIP)
                {
                    // For now, ReserveSkips are only constructed in-memory during repacking
                    if (instr.Args[ControlArg] is ReserveSkip reserve)
                    {
                        ret = new Goto
                        {
                            Cond = retCond,
                            ToLabel = reserve.Target,
                        };
                    }
                    else
                    {
                        ret = new Goto
                        {
                            Cond = retCond,
                            SkipLines = ExtractIntArg(instr, ControlArg)
                        };
                    }
                }
                else if (Variant == ControlType.END)
                {
                    ret = new End
                    {
                        Cond = retCond,
                        Type = ExtractIntArg(instr, ControlArg)
                    };
                }
                else if (Variant == ControlType.GOTO)
                {
                    ret = new Goto
                    {
                        Cond = retCond,
                        ToLabel = $"L{ExtractIntArg(instr, ControlArg)}"
                    };
                }
                else if (Variant == ControlType.WAIT)
                {
                    ret = new Wait
                    {
                        Cond = retCond,
                        Special = true
                    };
                }
                else throw new ArgumentException($"Unrecognized variant style {Variant}");
                return ret;
            }
        }

        public class ConditionSelector
        {
            // Per command key, the different variants which can apply. These are evaluated in order, so the last one should have NegateArg as -1.
            public Dictionary<string, List<ConditionVariant>> Variants = new Dictionary<string, List<ConditionVariant>>();
            public ConditionDoc Cond { get; set; }
            public EMEDF.EnumDoc NegateEnum { get; set; }

            public ControlType GetBasicType(Instr instr)
            {
                if (!Variants.TryGetValue(instr.Cmd, out List<ConditionVariant> variants))
                {
                    throw new ArgumentException($"Can't use selector for {Cond.Name} to handle {instr}");
                }
                return variants[0].Variant;
            }

            public ConditionVariant GetVariant(Instr instr)
            {
                if (!Variants.TryGetValue(instr.Cmd, out List<ConditionVariant> variants))
                {
                    throw new ArgumentException($"Can't use selector for {Cond.Name} to handle {instr}");
                }
                foreach (ConditionVariant variant in variants)
                {
                    if (variant.NegateArg == -1)
                    {
                        return variant;
                    }
                    // If a param, this variant can't be used
                    if (!TryExtractIntArg(instr.Args[variant.NegateArg], out int negateVal)) continue;
                    // If does not match fixed enum, this variant can't be used
                    bool extraOkay = true;
                    foreach (KeyValuePair<int, int> req in variant.ExtraArgs)
                    {
                        if (instr.Args[req.Key] is ParamArg || !TryExtractIntArg(instr.Args[req.Key], out int val) || val != req.Value)
                        {
                            extraOkay = false;
                        }
                    }
                    if (!extraOkay) continue;
                    // Only one variant in these cases
                    if (NegateEnum.Name == "Comparison Type") return variant;
                    if (NegateEnum.Values.Count == 2) return variant;
                    // Otherwise, find the right one for this case
                    BoolVersion version = Cond.AllBools.Find(b => b.Name == variant.Doc.Name);
                    if (version == null) continue;
                    string arg = NegateEnum.Values[negateVal.ToString()];
                    if (version.True == arg || version.False == arg) return variant;
                }
                throw new ArgumentException($"No acceptable condition variant found for {Cond.Name} and {instr}");
            }
        }

        // A pair of instruction and short instruction.
        // This has all the combined data, rather than using something like FunctionDoc.
        public class ShortVariant
        {
            // Short name
            public string Name { get; set; }
            // Command id (may have multiple names)
            public string Cmd { get; set; }
            // Args for this condition function when it appears decompiled or in source.
            public List<EMEDF.ArgDoc> Args { get; set; }
            // Args which have an assumed value.
            public Dictionary<int, int> ExtraArgs = new Dictionary<int, int>();
            // The number of args at the end which are optional and may take on default values.
            public int OptionalArgs { get; set; }
            // Whether this function is automatically used in decompilation, should be documented, etc.
            public bool Hidden { get; set; }

            public Instr ExtractInstrArgs(Instr instr)
            {
                Instr ret = new Instr
                {
                    Name = Name,
                    Cmd = Cmd,
                };
                for (int i = 0; i < instr.Args.Count; i++)
                {
                    if (!ExtraArgs.ContainsKey(i))
                    {
                        ret.Args.Add(instr.Args[i]);
                    }
                }
                HideOptionalDefaultArgs(ret.Args, Args, OptionalArgs);
                return ret;
            }

            public void SetInstrArgs(Instr instr, Instr shortInstr)
            {
                // The approach here is similar to ConditionVariant, since there's the same distinction between fixed and user-given args.
                List<object> extraArgs = shortInstr.Args.ToList();
                foreach (KeyValuePair<int, int> req in ExtraArgs)
                {
                    instr.Args[req.Key] = req.Value;
                }
                List<int> emptyArgIndices = instr.Args.Select((a, i) => (a, i)).Where(e => e.Item1 == null).Select(e => e.Item2).ToList();
                if (emptyArgIndices.Count != extraArgs.Count)
                {
                    int missingArgs = emptyArgIndices.Count - extraArgs.Count;
                    if (missingArgs > 0 && missingArgs <= OptionalArgs)
                    {
                        extraArgs.AddRange(PadOptionalDefaultArgs(Args, missingArgs, OptionalArgs));
                    }
                    // This should be accounted for in JS compiler check, before getting to here.
                    else throw new Exception($"Have ({string.Join(", ", extraArgs)}) from {Name} to fit into {instr} but mismatch in count");
                }
                for (int i = 0; i < emptyArgIndices.Count; i++)
                {
                    instr.Args[emptyArgIndices[i]] = extraArgs[i];
                }
            }
        }

        public class ShortSelector
        {
            // Command id (may have multiple short versions)
            public string Cmd { get; set; }
            // The different short versions which may apply
            public List<ShortVariant> Variants = new List<ShortVariant>();

            public ShortVariant GetVariant(Instr instr)
            {
                if (Cmd != instr.Cmd)
                {
                    throw new ArgumentException($"Can't use selector for {Cmd} to handle {instr}");
                }
                foreach (ShortVariant variant in Variants)
                {
                    if (variant.Hidden) continue;
                    // If does not match fixed enum, this variant can't be used
                    bool extraOkay = true;
                    foreach (KeyValuePair<int, int> req in variant.ExtraArgs)
                    {
                        if (instr.Args[req.Key] is ParamArg || !TryExtractIntArg(instr.Args[req.Key], out int val) || val != req.Value)
                        {
                            extraOkay = false;
                        }
                    }
                    if (!extraOkay) continue;
                    return variant;
                }
                // It is definitely okay to not find a variant for instructions
                return null;
            }
        }

        public static int OppositeOp(ConditionDoc Cond, EMEDF.EnumDoc NegateEnum, int op)
        {
            if (NegateEnum.Name == "Comparison Type")
            {
                return (int)OppositeComparison[(ComparisonType)op];
            }
            if (NegateEnum.Values.Count == 2)
            {
                if (!NegateEnum.Values.ContainsKey(op.ToString())) throw new ArgumentException();
                return int.Parse(NegateEnum.Values.Where(e => e.Key != op.ToString()).First().Key);
            }
            foreach (BoolVersion version in Cond.AllBools)
            {
                string val = NegateEnum.Values[op.ToString()];
                if (version.True == val)
                {
                    return int.Parse(NegateEnum.Values.Where(e => e.Value == version.False).First().Key);
                }
                if (version.False == val)
                {
                    return int.Parse(NegateEnum.Values.Where(e => e.Value == version.False).First().Key);
                }
            }
            throw new ArgumentException($"Opposite value of {op} not present for enum {NegateEnum.Name}");
        }

        // Translation-related utilities

        public List<string> GetConditionCategories(Cond cond)
        {
            List<string> ret = new List<string>();
            void addCategory(string docName)
            {
                if (CondDocs.TryGetValue(docName, out FunctionDoc doc))
                {
                    string cat = doc.ConditionDoc.Category;
                    if (cat != null && !ret.Contains(cat))
                    {
                        ret.Add(cat);
                    }
                }
            }
            cond.WalkCond(c =>
            {
                if (c is CmdCond || c is CompareCond)
                {
                    addCategory(c.DocName);
                }
            });
            return ret;
        }
        
        public void AddDisplayEnums(Instr instr)
        {
            if (InstrDocs.TryGetValue(instr.Cmd, out EMEDF.InstrDoc doc))
            {
                for (int i = 0; i < doc.Arguments.Length; i++)
                {
                    if (i >= instr.Args.Count) break;
                    EMEDF.EnumDoc edoc = doc.Arguments[i].EnumDoc;
                    if (edoc != null && edoc.DisplayValues.TryGetValue(instr.Args[i].ToString(), out string name))
                    {
                        instr.Args[i] = new DisplayArg { DisplayValue = name, Value = instr.Args[i] };
                    }
                }
            }
        }

        public class InstructionTranslationException : Exception
        {
            public InstructionTranslationException(string message) : base(message) { }
        }
    }
}
