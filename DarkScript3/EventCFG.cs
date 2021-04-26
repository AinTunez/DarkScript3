using System;
using System.Collections.Generic;
using System.Linq;
using static DarkScript3.ScriptAst;
using static DarkScript3.InstructionTranslator;

namespace DarkScript3
{
    public class EventCFG
    {
        public int ID { get; set; }

        private bool debugPrint = false;
        private int NextID = 0;
        private FlowNode First;
        private Dictionary<int, FlowNode> Nodes = new Dictionary<int, FlowNode>();
        private CFGOptions options;

        public EventCFG(int id, CFGOptions options = null)
        {
            ID = id;
            this.options = options ?? CFGOptions.DEFAULT;
        }

        public class CFGOptions
        {
            // Decompilation
            // Combine definitions of the same condition group in the same basic block.
            public bool CombineDefinitions = true;
            // Only combine adjacent definitions, so order is preserved with other condition groups.
            public bool CombineNonAdjacentDefinitions = false;
            // When definitions are always used in a certain evaluation and nowhere else, inline them into the usage.
            public bool InlineDefinitions = true;
            // Only inline definitions when condition group order won't change.
            public bool InlineDefinitionsOutOfOrder = false;
            // Name condition groups based on what they're calculating rather than based on the register number.
            // (Avoid doing this if a condition group is redefined after main group usage, indicating possible use for other purposes.)
            public bool RenameConditionGroups = true;

            // Compilation.
            // Minimize diffs for DS1 most of the time by using two separate condition groups for an expression like x = a && b.
            public bool UseTwoGroupsForPlainAnd = false;
            // This is a hacky way to pass this in, but it is effectively a change-able input to how the compiler is called.
            // It should be set for DS1 PTDE only, not DS1R.
            public bool RestrictConditionGroupCount = false;

            // Combines and inlines conditions without changing underlying behavior.
            public static readonly CFGOptions DEFAULT = new CFGOptions();
            // Matches the original nearly line-for-line but with additional control flow structures.
            public static readonly CFGOptions MIN = new CFGOptions
            {
                CombineDefinitions = false,
                InlineDefinitions = false,
                RenameConditionGroups = false
            };
        }

        public class FlowNode : IComparable<FlowNode>
        {
            public override string ToString() => $"#{ID}";
            public int ID { get; set; }
            public Intermediate Im { get; set; }
            public FlowNode Pred { get; set; }
            public FlowNode Succ { get; set; }
            public List<FlowNode> JumpPred = new List<FlowNode>();
            public FlowNode JumpTo { get; set; }
            public List<FlowNode> AllPreds(bool ignoreEnd = false)
            {
                List<FlowNode> preds = new List<FlowNode>();
                if (Pred != null && (!Pred.AlwaysJump || (ignoreEnd && Pred.AlwaysEnd)))
                {
                    preds.Add(Pred);
                }
                preds = preds.Union(JumpPred).ToList();
                return preds;
            }
            // True if succ is never followed. Succ is still retained for source ordering, but not used in dataflow analysis.
            public bool AlwaysJump => Im is CondIntermediate condIm && condIm.Cond.Always;
            // For cleaner control flow analysis, treat returns as normal statements.
            public bool AlwaysEnd => Im is End condIm && condIm.Cond.Always;
            // Set if part of a sequence of gotos, meaning if/else should be avoided
            public bool MultiGoto { get; set; }

            // Graph evaluation stuff. Valid until nodes get deleted or rewritten.
            // TODO: See if this can be made more efficient and cleaner.
            // These could be sets and could also be in an external dictionary.
            // Also, these get quadratically large, unnecessary for most super long events.

            // All nodes before this one that must be evaluated before it.
            public List<FlowNode> Dominators { get; set; }
            // Transitive closure of dominators, calculated right before if/else structuring.
            public List<FlowNode> ImmedDominators { get; set; }
            // All nodes before this one that can possibly be evaluated before it.
            public List<FlowNode> Ancestors { get; set; }
            // Two nodes have the same block id if they always get executed together.
            // In a stricter mode, blocks are shared if two nodes define the same things.
            public int BlockID { get; set; }

            // Condition tree
            // The context usable in this instruction.
            public ConditionContext Context { get; set; }
            // The context established for the next instruction.
            public ConditionContext NextContext { get; set; }

            public int? DefineReg { get; set; }
            public List<int> UseRegs = new List<int>();
            public List<int> CompiledUseRegs = new List<int>();
            public ConditionDAG Define => DefineReg is int reg ? Context.Conds[reg] : null;
            public List<ConditionDAG> Uses => UseRegs.Select(r => Context.Conds[r]).ToList();
            public List<ConditionDAG> CompiledUses => CompiledUseRegs.Select(r => Context.CompiledConds[r]).ToList();

            public int CompareTo(FlowNode other)
            {
                return ID.CompareTo(other.ID);
            }
        }

        public class ConditionContext
        {
            public Dictionary<int, ConditionDAG> Conds = new Dictionary<int, ConditionDAG>();
            public Dictionary<int, ConditionDAG> CompiledConds = new Dictionary<int, ConditionDAG>();

            private static Dictionary<int, ConditionDAG> MergeDict(Dictionary<int, ConditionDAG> existing, Dictionary<int, ConditionDAG> extra)
            {
                Dictionary<int, ConditionDAG> ret = existing.ToDictionary(e => e.Key, e => e.Value);
                foreach (KeyValuePair<int, ConditionDAG> entry in extra)
                {
                    ret[entry.Key] = entry.Value;
                }
                return ret;
            }

            public ConditionContext MainGroupEval()
            {
                return new ConditionContext
                {
                    CompiledConds = MergeDict(CompiledConds, Conds),
                };
            }

            public ConditionContext ClearCompiledConds()
            {
                return new ConditionContext
                {
                    Conds = Conds,
                };
            }

            public static ConditionContext Merge(ConditionContext contextA, ConditionContext contextB)
            {
                ConditionContext o = new ConditionContext();
                void mergeConds(Dictionary<int, ConditionDAG> target, Dictionary<int, ConditionDAG> a, Dictionary<int, ConditionDAG> b)
                {
                    foreach (int cond in a.Keys.Union(b.Keys).ToList())
                    {
                        if (a.ContainsKey(cond) && b.ContainsKey(cond))
                        {
                            target[cond] = a[cond] = b[cond] = a[cond].Merge(b[cond]);
                        }
                        else
                        {
                            target[cond] = a.ContainsKey(cond) ? a[cond] : b[cond];
                        }
                    }
                }
                mergeConds(o.Conds, contextA.Conds, contextB.Conds);
                mergeConds(o.CompiledConds, contextA.CompiledConds, contextB.CompiledConds);
                return o;
            }
        }

        // Condition group structure. This is basically a tree with MAIN at the root.
        public class ConditionDAG
        {
            // The group being used/defined
            public int ResultGroup { get; set; }

            // The MAIN command associated with this condition group, if any.
            // This may only be a CondAssign with group 0. All compilers are also defines for 0.
            public List<FlowNode> Compilers = new List<FlowNode>();

            // All the places some condition is added to this condition group.
            // This may only be a CondAssign.
            public List<FlowNode> Defines = new List<FlowNode>();

            // All the places the condition group is evaluated or added to another condition group.
            public List<FlowNode> Uses = new List<FlowNode>();
            public List<FlowNode> CompiledUses = new List<FlowNode>();

            public ConditionDAG Merge(ConditionDAG o)
            {
                if (o == this || o == null) return this;
                Compilers = Compilers.Union(o.Compilers).ToList();
                Defines = Defines.Union(o.Defines).ToList();
                Uses = Uses.Union(o.Uses).ToList();
                CompiledUses = CompiledUses.Union(o.CompiledUses).ToList();
                return this;
            }
        }

        // Common utilities

        private List<FlowNode> NodeList()
        {
            List<FlowNode> order = new List<FlowNode>();
            HashSet<FlowNode> visited = new HashSet<FlowNode>();
            // Reverse postorder
            void visit(FlowNode n)
            {
                visited.Add(n);
                List<FlowNode> nexts = new List<FlowNode>();
                if (n.Succ != null)
                {
                    nexts.Add(n.Succ);
                }
                if (n.JumpTo != null)
                {
                    nexts.Add(n.JumpTo);
                }
                foreach (FlowNode next in nexts)
                {
                    if (!visited.Contains(next))
                    {
                        visit(next);
                    }
                }
                order.Add(n);
            }
            visit(First);
            order.Reverse();
            return order;
        }

        private void PrintNodes(int indent = 0, bool labels = false)
        {
            string sp = string.Join("", Enumerable.Repeat("    ", indent));
            foreach (FlowNode n in NodeList())
            {
                string label = null;
                if (labels) label = string.Join("", n.Im.Labels.Select(l => $"{l}: "));
                Console.WriteLine($"{sp}{n.ID} [{n.BlockID}]: {label}{n.Im}");
            }
        }

        private void RemoveNode(FlowNode node, Intermediate replacement)
        {
            if (node == First) First = null;
            if (node.Pred != null)
            {
                node.Pred.Succ = node.Succ;
            }
            if (node.Succ != null)
            {
                node.Succ.Pred = node.Pred;
                if (First == null) First = node.Succ;
            }
            foreach (FlowNode jump in node.JumpPred)
            {
                Intermediate im = jump.Im;
                if (!(im is Goto go))
                {
                    throw new Exception($"Internal exception: {node} jump pred {jump} not a goto apparently");
                }
                FlowNode target = node.Succ;
                jump.JumpTo = target;
                go.ToNode = target == null ? -1 : target.ID;
                if (target != null) target.JumpPred.Add(jump);
            }
            // Transfer over decorations to the current replacement. Maybe should combine blank lines, or take the min of the two?
            if (node.Im == replacement) throw new Exception($"Internal error: can't offload removed node {node}'s decorations only itself");
            if (node.Im.Decorations != null)
            {
                if (replacement.Decorations == null) replacement.Decorations = new List<SourceDecoration>();
                replacement.Decorations.AddRange(node.Im.Decorations);
            }
            Nodes.Remove(node.ID);
        }

        // Used by both compilation and decompilation. Replaces goto line/node commands with labels when they exist.
        public void AddLabelsToCfgPass(Result result)
        {
            Dictionary<string, FlowNode> labels = new Dictionary<string, FlowNode>();
            foreach (FlowNode node in Enumerable.Reverse(NodeList()))
            {
                if (node.Im is Label label)
                {
                    labels[$"L{label.Num}"] = node;
                }
                else if (node.Im is Goto g && g.ToLabel != null)
                {
                    if (!labels.TryGetValue(g.ToLabel, out FlowNode targetNode))
                    {
                        result.Warnings.Add($"{g} goes to nonexistent label (it will just end the event)");
                    }
                    if (targetNode != null)
                    {
                        node.JumpTo = targetNode;
                        g.ToNode = targetNode.ID;
                        targetNode.JumpPred.Add(node);
                    }
                }
                // Add for previous line to use. Labels can't jump to themselves.
                foreach (string l in node.Im.Labels)
                {
                    labels[l] = node;
                }
            }
        }

        // End common utilities

        public class Result
        {
            public List<string> Errors = new List<string>();
            public List<string> Warnings = new List<string>();
        }

        public Result Compile(EventFunction func, InstructionTranslator info)
        {
            Result result = new Result();

            // Code generation
            int tempVarID = 1;
            string newVar() => $"#temp{tempVarID++}";

            // Pending jump sources to add before generating the targets, to link them together.
            List<FlowNode> jumpFroms = new List<FlowNode>();
            List<string> nextLabels = null;
            NextID = 0;
            FlowNode current = null;

            void genBlock(List<Intermediate> cmds)
            {
                foreach (Intermediate im in cmds)
                {
                    genStatement(im);
                }
            }
            void genStatement(Intermediate im)
            {
                nextLabels = im.Labels;
                im.Labels = new List<string>();
                if (im is IfElse ifelse)
                {
                    genIf(ifelse);
                }
                else if (im is CondAssign assign)
                {
                    Cond assignResult = genSimpleCond(assign.Cond, assign, im);
                    if (assignResult != null)
                    {
                        assign.Cond = assignResult;
                        newIntermediate(assign, im);
                    }
                }
                else if (im is CondIntermediate condIm)
                {
                    condIm.Cond = genSimpleCond(condIm.Cond, null, im);
                    newIntermediate(condIm, im);
                }
                else
                {
                    newIntermediate(im, im);
                }
            }
            Cond genSimpleCond(Cond cond, CondAssign existing, Intermediate source)
            {
                if (cond is OpCond op)
                {
                    string var = null;
                    bool useExisting = false;
                    if (existing != null && !op.Negate)
                    {
                        bool opCompatible = op.And
                            ? (options.UseTwoGroupsForPlainAnd ? existing.Op == CondAssignOp.AssignAnd : existing.Op != CondAssignOp.AssignOr)
                            : existing.Op != CondAssignOp.AssignAnd;
                        if (opCompatible)
                        {
                            useExisting = true;
                            var = existing.ToVar;
                        }
                    }
                    if (!useExisting)
                    {
                        var = newVar();
                    }
                    foreach (Cond opArg in op.Ops)
                    {
                        Cond arg = genSimpleCond(opArg, null, source);
                        CondAssign assign = new CondAssign { ToVar = var, Cond = arg, Op = op.And ? CondAssignOp.AssignAnd : CondAssignOp.AssignOr };
                        newIntermediate(assign, source);
                    }
                    return useExisting ? null : new CondRef { Compiled = false, Name = var, Negate = op.Negate };
                }
                else
                {
                    return cond;
                }
            }
            void genIf(IfElse ifelse)
            {
                Cond cond = ifelse.Cond;
                cond.Negate = !cond.Negate;
                cond = genSimpleCond(cond, null, ifelse);
                Goto im = new Goto { Cond = cond };
                // There is one annoying behavior here, which is that we don't know if the jump ends up at
                // a label or not (it would require lookahead), and a different # of instructions may be
                // emitted in case a given condition group has a COND/GOTO variant but not a SKIP one.
                // This happens a bunch with GotoIfPlayerIsNotInOwnWorldExcludesArena.
                newIntermediate(im, ifelse);
                FlowNode node = current;
                genBlock(ifelse.True);
                if (ifelse.False.Count == 0)
                {
                    jumpFroms.Add(node);
                    genBlock(ifelse.False);
                }
                else
                {
                    Goto im2 = new Goto { Cond = Cond.ALWAYS };
                    newIntermediate(im2, null);
                    FlowNode node2 = current;
                    jumpFroms.Add(node);
                    genBlock(ifelse.False);
                    jumpFroms.Add(node2);
                }
            }
            void newIntermediate(Intermediate im, Intermediate source)
            {
                foreach (Intermediate subIm in info.ExpandCond(im, newVar))
                {
                    if (source != null && source.Decorations != null)
                    {
                        if (subIm != source)
                        {
                            if (subIm.Decorations == null) subIm.Decorations = new List<SourceDecoration>();
                            subIm.Decorations.AddRange(source.Decorations);
                        }
                        source = null;
                    }
                    newNode(subIm);
                }
            }
            void newNode(Intermediate im)
            {
                FlowNode node = new FlowNode
                {
                    ID = NextID++,
                    Im = im,
                };
                im.ID = node.ID;
                if (nextLabels != null)
                {
                    im.Labels = nextLabels;
                    nextLabels = null;
                }
                if (current == null)
                {
                    First = node;
                }
                else
                {
                    current.Succ = node;
                    node.Pred = current;
                }
                if (jumpFroms.Count > 0)
                {
                    foreach (FlowNode jumpFrom in jumpFroms)
                    {
                        jumpFrom.JumpTo = node;
                        if (!(jumpFrom.Im is Goto g))
                        {
                            throw new Exception($"Internal error: added {jumpFrom} {jumpFrom.Im} as jump origin but it's not a Goto");
                        }
                        g.ToNode = node.ID;
                        node.JumpPred.Add(jumpFrom);
                    }
                    jumpFroms.Clear();
                }
                current = node;
            }

            genBlock(func.Body);
            // This is always included at the end as a possible jump target for skip length calculation, but always taken out later.
            if (current == null || !(current.Im is NoOp))
            {
                newNode(new NoOp());
            }

            AddLabelsToCfgPass(result);

            // Additional processing for labels: if calculated targets of jumps are proper labels, add the label to the jump
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is Goto g && g.ToLabel == null && node.JumpTo.Im is Label label)
                {
                    g.ToLabel = $"L{label.Num}";
                }
            }

            Dictionary<string, List<CondAssignOp>> groupOps = new Dictionary<string, List<CondAssignOp>>();
            foreach (FlowNode node in NodeList())
            {
                if (!(node.Im is CondIntermediate condIm)) continue;
                if (condIm is CondAssign assign)
                {
                    if (assign.ToVar == null) throw new Exception($"Internal error: expected variable name in assignment {condIm}");
                    if (!groupOps.TryGetValue(assign.ToVar, out List<CondAssignOp> ops))
                    {
                        ops = groupOps[assign.ToVar] = new List<CondAssignOp>();
                    }
                    ops.Add(assign.Op);
                }
                if (condIm.Cond is CondRef condRef)
                {
                    if (condRef.Name == null) throw new Exception($"Internal error: expected variable name in usage {condIm}");
                    if (!groupOps.ContainsKey(condRef.Name))
                    {
                        groupOps[condRef.Name] = new List<CondAssignOp>();
                    }
                }
            }

            // TODO: oof DS1R temporary hack, add this option to the UI.
            int maxGroup = options.RestrictConditionGroupCount && ID != 11210100 ? 7 : 15;
            List<int> orGroups = Enumerable.Range(1, maxGroup).ToList();
            List<int> andGroups = Enumerable.Range(1, maxGroup).ToList();
            Dictionary<string, int> groupNames = new Dictionary<string, int>();
            foreach (KeyValuePair<string, List<CondAssignOp>> entry in groupOps)
            {
                string name = entry.Key;
                if (name.StartsWith("and") && int.TryParse(name.Substring(3), out int group))
                {
                    // This is an error mainly because it may have different behavior from how it might appear
                    if (entry.Value.Contains(CondAssignOp.AssignOr)) result.Errors.Add($"Group is named {name} but is defined with |=");
                    groupNames[name] = group;
                    andGroups.Remove(group);
                }
                if (name.StartsWith("or") && int.TryParse(name.Substring(2), out group))
                {
                    if (entry.Value.Contains(CondAssignOp.AssignAnd)) result.Errors.Add($"Group is named {name} but is defined with &=");
                    groupNames[name] = -group;
                    orGroups.Remove(group);
                }
            }
            foreach (KeyValuePair<string, List<CondAssignOp>> entry in groupOps)
            {
                string name = entry.Key;
                if (groupNames.ContainsKey(name)) continue;
                List<CondAssignOp> ops = entry.Value;
                if (ops.Contains(CondAssignOp.Assign) && ops.Count > 1) result.Errors.Add($"Condition group {name} can only use = if it has a single assignment. Use |= or &= instead.");
                // Plain assignment goes to AND registers, unless we've run out.
                if (ops.Contains(CondAssignOp.AssignOr) || (ops.Contains(CondAssignOp.Assign) && andGroups.Count == 0))
                {
                    if (ops.Contains(CondAssignOp.AssignAnd)) result.Errors.Add($"Group {name} uses both &= and |=");
                    if (orGroups.Count == 0)
                    {
                        result.Errors.Add($"Ran out of OR condition groups to allocate to {name}. (Ask for feature request: automatically reusing groups after use)");
                    }
                    else
                    {
                        groupNames[name] = -orGroups[0];
                        orGroups.RemoveAt(0);
                    }
                }
                else
                {
                    if (andGroups.Count == 0)
                    {
                        result.Errors.Add($"Ran out of AND condition groups to allocate to {name}. (Ask for feature request: automatically reusing groups after compilation)");
                    }
                    else
                    {
                        groupNames[name] = andGroups[0];
                        andGroups.RemoveAt(0);
                    }
                }
            }

            foreach (FlowNode node in NodeList())
            {
                if (!(node.Im is CondIntermediate condIm)) continue;
                if (condIm is CondAssign assign)
                {
                    if (groupNames.TryGetValue(assign.ToVar, out int group))
                    {
                        assign.ToCond = group;
                        assign.ToVar = null;
                    }
                }
                if (condIm.Cond is CondRef condRef)
                {
                    if (groupNames.TryGetValue(condRef.Name, out int group))
                    {
                        condRef.Group = group;
                        condRef.Name = null;
                    }
                }
            }

            // Fill in SkipLines here
            Dictionary<FlowNode, int> lines = new Dictionary<FlowNode, int>();
            int i = 0;
            foreach (FlowNode node in NodeList())
            {
                lines[node] = i;
                if (!(node.Im is NoOp))
                {
                    // For NoOp nodes at the end of a block, they are equivalent to then next statement.
                    // For NoOp nodes at the very end, i doesn't matter anymore.
                    i++;
                }
            }
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is Goto g && (g.ToLabel == null || !LabelIds.ContainsKey(g.ToLabel)))
                {
                    if (node.JumpTo == null)
                    {
                        result.Errors.Add($"Target label not found for {g}");
                        continue;
                    }
                    int start = lines[node];
                    int end = lines[node.JumpTo];
                    g.ToLabel = null;
                    g.SkipLines = Math.Max(0, end - start - 1);
                    if (g.SkipLines > byte.MaxValue)
                    {
                        result.Errors.Add($"Can't skip more than 256 lines in {g}. Add a label L0-L20 at the destination instead, if the game supports this");
                    }
                }
            }

            List<Intermediate> instrs = new List<Intermediate>();
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is NoOp) continue;
                try
                {
                    Instr instr = info.CompileCond(node.Im);
                    instr.Decorations = node.Im.Decorations;
                    instrs.Add(instr);
                    node.Im = instr;
                }
                catch (Exception ex) when (ex is FancyNotSupportedException || ex is InstructionTranslationException)
                {
                    // These exceptions are basically the same thing, but InstructionTranslationException means it's the user's fault.
                    result.Errors.Add(ex.Message);
                }
            }

            if (debugPrint)
            {
                Console.WriteLine($"---------------------------------------------- {ID}");
                PrintNodes();
                Console.WriteLine();
            }

            func.Body = instrs;
            func.Fancy = false;

            return result;
        }

        public Result Decompile(EventFunction func, InstructionTranslator info)
        {
            Result result = new Result();

            if (debugPrint) Console.WriteLine($"$Event({func.ID}, {func.RestBehavior}, function({string.Join(", ", func.Params)}) {{");

            // Rewrite control flow operators and initialize CFG
            List<Intermediate> cmds = func.Body;
            for (int i = 0; i < cmds.Count; i++)
            {
                Intermediate im = cmds[i];
                if (im is Instr instr)
                {
                    cmds[i] = im = info.DecompileCond(instr);
                    im.Decorations = instr.Decorations;
                }
                im.ID = i;
                Nodes[i] = new FlowNode
                {
                    ID = i,
                    Im = im,
                };
                NextID++;
            }
            // Also add a synthetic end node
            FlowNode END = new FlowNode
            {
                ID = cmds.Count,
                Im = new NoOp(),
            };
            Nodes[cmds.Count] = END;
            for (int i = 0; i <= cmds.Count; i++)
            {
                if (i > 0)
                {
                    Nodes[i].Pred = Nodes[i - 1];
                }
                if (i < cmds.Count)
                {
                    Nodes[i].Succ = Nodes[i + 1];
                }
                if (First == null) First = Nodes[i];
            }

            // Replace line skips with jumps to ids
            // Also mark lines as skippable and mark jump targets
            for (int i = 0; i < cmds.Count; i++)
            {
                FlowNode node = Nodes[i];
                Intermediate im = cmds[i];
                if (im is Goto g)
                {
                    int target;
                    if (g.SkipLines >= 0)
                    {
                        target = i + 1 + g.SkipLines;
                        if (target > cmds.Count)
                        {
                            target = cmds.Count;
                            result.Warnings.Add($"Command {im} skips past the end of the event");
                        }
                    }
                    else if (g.ToLabel != null) continue;
                    else throw new ArgumentException($"Unrecognized skip {im}");

                    FlowNode targetNode = Nodes[target];
                    node.JumpTo = targetNode;
                    g.ToNode = targetNode.ID;
                    targetNode.JumpPred.Add(Nodes[im.ID]);
                }
            }

            AddLabelsToCfgPass(result);

            // From this point on, do everything in the CFG
            cmds = null;

            int blockId = 0;
            foreach (FlowNode node in NodeList())
            {
                List<FlowNode> preds = node.AllPreds();
                // Do dominators and ancestors for data flow
                node.Dominators = new List<FlowNode> { node };
                node.Ancestors = new List<FlowNode> { node };
                if (preds.Count == 0)
                {
                    // Nothing to do here. If pred exists, this node is unreachable, which could be
                    // a warning, except that a *lot* of vanilla emevd is like this.
                }
                else
                {
                    List<FlowNode> domList = preds[0].Dominators.ToList();
                    for (int i = 1; i < preds.Count; i++)
                    {
                        domList = domList.Intersect(preds[i].Dominators).ToList();
                    }
                    node.Dominators.AddRange(domList);
                    for (int i = 0; i < preds.Count; i++)
                    {
                        node.Ancestors = node.Ancestors.Union(preds[i].Ancestors).ToList();
                    }
                }
                // Now, basic blocks. These are used as a heurustic for combining condition definitions.
                node.BlockID = -1;
                int getAssign(FlowNode n) => n.Im is CondAssign condIm ? condIm.ToCond : 100;
                if (preds.Count == 1)
                {
                    // There is the possibility to inherit basic blocks here, if the previous line unconditionally goes to this one
                    FlowNode pred = preds[0];
                    if ((pred.Succ == null || pred.Succ == node) && (pred.JumpTo == node || pred.JumpTo == null))
                    {
                        if (true || options.CombineNonAdjacentDefinitions || getAssign(node) == getAssign(pred))
                        {
                            node.BlockID = pred.BlockID;
                        }
                    }
                }
                if (node.BlockID == -1)
                {
                    node.BlockID = blockId++;
                }
            }
            foreach (FlowNode node in NodeList())
            {
                // Also, for the purpose of control flow algorithms, dominators don't include themselves.
                node.Dominators.Remove(node);
            }

            if (debugPrint)
            {
                PrintNodes();
                Console.WriteLine($"---------------------------------------------- {ID}");
            }

            // Construct trees rooted at main/uncompiled/compiled condition evaluation, also with parent pointers.
            // First, rewrite condition expressions
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is CondIntermediate condIm && condIm.Cond is CmdCond cond)
                {
                    // Can support parameterized condition groups later, but it's not used in the base game, so don't do for now
                    if (cond.Name == "CompareCompiledConditionGroup" || cond.Name == "CompareConditionGroup")
                    {
                        throw new FancyNotSupportedException($"Parameterized condition group in {node} {node.Im}");
                    }
                    if (cond.Name == "ConditionGroup" || cond.Name == "CompareConditionGroup")
                    {
                        int reg = int.Parse(cond.Args.Last().ToString());
                        condIm.Cond = new CondRef { Compiled = false, Group = reg, Negate = cond.Negate };
                    }
                    if (cond.Name == "CompiledConditionGroup" || cond.Name == "CompareCompiledConditionGroup")
                    {
                        int reg = int.Parse(cond.Args.Last().ToString());
                        condIm.Cond = new CondRef { Compiled = true, Group = reg, Negate = cond.Negate };
                    }
                    // Note that reg can be MAIN here. DS3 does this. That is obviously completely bonkers, but it is handled later.
                }
            }

            // Track condition groups throughout the event CFG
            // Also keep track of various issues that might prevent a condition group from being correctly inlined later
            HashSet<int> noInlineGroups = new HashSet<int>();
            foreach (FlowNode node in NodeList())
            {
                List<FlowNode> preds = node.AllPreds();
                ConditionContext context;
                if (preds.Count == 0)
                {
                    context = new ConditionContext();
                }
                else if (preds.Count == 1)
                {
                    context = preds[0].NextContext;
                }
                else
                {
                    // To maybe fix, order of these seems to be wrong, but it's not used in an order-dependent way.
                    context = ConditionContext.Merge(preds[0].NextContext, preds[1].NextContext);
                    for (int i = 2; i < preds.Count; i++)
                    {
                        context = ConditionContext.Merge(context, preds[i].NextContext);
                    }
                }
                node.Context = node.NextContext = context;
                Intermediate im = node.Im;
                if (im is Instr instr)
                {
                    // Clear compiled state
                    if (instr.Cmd == "2000[03]")
                    {
                        node.NextContext = node.Context.ClearCompiledConds();
                    }
                }
                else if (im is CondIntermediate condIm)
                {
                    if (condIm.Cond is CondRef cond)
                    {
                        int reg = cond.Group;
                        if (cond.Compiled)
                        {
                            if (!context.CompiledConds.TryGetValue(reg, out ConditionDAG use))
                            {
                                result.Warnings.Add($"Condition group {reg} used at {im} without being compiled (found [{string.Join(",", context.Conds.Keys)}])");
                                context.CompiledConds[reg] = use = new ConditionDAG
                                {
                                    ResultGroup = reg
                                };
                                noInlineGroups.Add(reg);
                            }
                            if (debugPrint) Console.WriteLine($"Adding {im.ID} as compiled cond of {use.ResultGroup}, with uses {string.Join(",", use.Uses)}");
                            use.CompiledUses.Add(node);
                            node.CompiledUseRegs.Add(reg);
                        }
                        else
                        {
                            if (!context.Conds.TryGetValue(reg, out ConditionDAG use))
                            {
                                result.Warnings.Add($"Condition group {reg} used at {im} without being defined (found [{string.Join(",", context.CompiledConds.Keys)}])");
                                context.Conds[reg] = use = new ConditionDAG
                                {
                                    ResultGroup = reg
                                };
                                noInlineGroups.Add(reg);
                            }
                            if (debugPrint) Console.WriteLine($"Adding {im.ID} as uncompiled cond of {use.ResultGroup}, with uses {string.Join(",", use.Uses)}");
                            use.Uses.Add(node);
                            node.UseRegs.Add(reg);
                        }
                    }
                    // Is a cond defined?
                    if (condIm is CondAssign assign)
                    {
                        int reg = assign.ToCond;
                        if (!context.Conds.TryGetValue(reg, out ConditionDAG define))
                        {
                            context.Conds[reg] = define = new ConditionDAG
                            {
                                ResultGroup = reg
                            };
                            context.Conds[reg] = define;
                        }
                        define.Defines.Add(node);
                        node.DefineReg = reg;
                        // If it's main cond, all uncompiled become compiled
                        if (reg == 0)
                        {
                            foreach (ConditionDAG comp in context.Conds.Values)
                            {
                                comp.Compilers.Add(node);
                            }
                            node.NextContext = context.MainGroupEval();
                        }
                    }
                }
            }

            Dictionary<int, string> fancyCondNames = new Dictionary<int, string>();
            if (options.RenameConditionGroups)
            {
                // Find all names with definitions used up to a WaitFor.
                // This is iffy with multi-evaluation cases but is a relatively okay naming heuristic overall.
                List<FlowNode> allNodes = NodeList();
                int lastNode = 0;
                while (lastNode < allNodes.Count)
                {
                    int candNode = allNodes.FindIndex(lastNode + 1, node => node.Define != null && node.Define.ResultGroup == 0);

                    Dictionary<int, List<string>> categories = new Dictionary<int, List<string>>();
                    void fillCategories(ConditionDAG use)
                    {
                        if (categories.ContainsKey(use.ResultGroup)) return;
                        categories[use.ResultGroup] = new List<string>();
                        IEnumerable<string> ecats = new List<string>();
                        foreach (FlowNode def in use.Defines)
                        {
                            CondAssign assign = (CondAssign)def.Im;
                            ecats = ecats.Union(info.GetConditionCategories(assign.Cond));
                            foreach (ConditionDAG subuse in def.Uses)
                            {
                                fillCategories(subuse);
                                ecats = ecats.Union(categories[subuse.ResultGroup]);
                            }
                        }
                        categories[use.ResultGroup] = ecats.ToList();
                    }
                    if (candNode != -1)
                    {
                        fillCategories(allNodes[candNode].Define);
                    }
                    // Conditions not part of a main group evaluation.
                    int limit = candNode == -1 ? allNodes.Count : candNode + 1;
                    for (int i = lastNode; i < limit; i++)
                    {
                        ConditionDAG def = allNodes[i].Define;
                        if (def != null)
                        {
                            fillCategories(def);
                        }
                    }
                    lastNode = limit;

                    // Merge this segment into final names list.
                    // If there are mismatched definitions for a given condition group,
                    // the final name becomes a generic placeholder.
                    foreach (KeyValuePair<int, List<string>> naming in categories)
                    {
                        int group = naming.Key;
                        if (group == 0) continue;
                        if (naming.Value.Count == 0)
                        {
                            fancyCondNames[group] = "cond";
                        }
                        else
                        {
                            string name = string.Join("", naming.Value.Select(
                                (c, i) => i == 0 ? c : c.Substring(0, 1).ToUpperInvariant() + c.Substring(1)));
                            if (!fancyCondNames.ContainsKey(group) || fancyCondNames[group] == name)
                            {
                                fancyCondNames[group] = name;
                            }
                            else
                            {
                                fancyCondNames[group] = "cond";
                            }
                        }
                    }
                }
            }

            // Collapse condition definitions where possible, within the same basic block.
            // This can be optionally be further limited to consecutive definitions.
            foreach (FlowNode node in NodeList())
            {
                if (!options.CombineDefinitions)
                {
                    break;
                }
                Intermediate im = node.Im;
                if (node.DefineReg != null && node.DefineReg != 0)
                {
                    ConditionDAG dag = node.Define;
                    // It's just this node, nothing to collapse
                    if (dag.Defines.Count == 1) continue;

                    // Collapse once per ConditionDAG object. So process all from the last one.
                    FlowNode last = dag.Defines.Max();
                    if (last.ID != im.ID) continue;

                    // The Nodes.ContainsKey filtering is because a single condition group may be conditionally used once, then used
                    // another time, and the defines list in the latter case won't be updated by the node shuffling.
                    List<List<FlowNode>> defineGroups = dag.Defines.Where(c => Nodes.ContainsKey(c.ID)).GroupBy(c => Nodes[c.ID].BlockID).Select(g => g.ToList()).ToList();

                    foreach (List<FlowNode> assigns in defineGroups)
                    {
                        if (assigns.Count < 2) continue;
                        OpCond op = new OpCond { And = dag.ResultGroup > 0 };
                        FlowNode dest = assigns.Max();
                        CondAssign destIm = (CondAssign)dest.Im;
                        foreach (FlowNode assign in assigns)
                        {
                            CondAssign ca = (CondAssign)assign.Im;
                            op.Ops.Add(ca.Cond);
                            if (assign != dest)
                            {
                                // Collapse the definition into target
                                dag.Defines.Remove(Nodes[assign.ID]);

                                // Also, update node pointers for any other condition groups which this one uses
                                foreach (ConditionDAG use in Nodes[assign.ID].Uses.Union(Nodes[assign.ID].CompiledUses))
                                {
                                    if (debugPrint) Console.WriteLine($"Rewriting use of {assign.ID} with [{string.Join(",", use.Uses)}] and [{string.Join(",", use.CompiledUses)}]");
                                    if (use.Uses.Contains(assign))
                                    {
                                        use.Uses.Remove(Nodes[assign.ID]);
                                        use.Uses.Add(dest);
                                    }
                                    if (use.CompiledUses.Contains(assign))
                                    {
                                        use.CompiledUses.Remove(Nodes[assign.ID]);
                                        use.CompiledUses.Add(dest);
                                    }
                                    dest.UseRegs.Add(use.ResultGroup);
                                    dest.UseRegs = dest.UseRegs.Distinct().ToList();
                                }

                                RemoveNode(Nodes[assign.ID], destIm);
                            }
                        }
                        destIm.Cond = op;
                    }
                }
            }

            // Only inline condition groups if there is an unconditional direct relationship between defines and use.
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is CondIntermediate condIm)
                {
                    foreach (ConditionDAG use in node.Uses)
                    {
                        // Condition groups cannot be inlined if they have more than one use, including any compiled uses
                        if (use.Uses.Count != 1 || use.CompiledUses.Count > 0)
                        {
                            noInlineGroups.Add(use.ResultGroup);
                        }
                        // Don't inline any group when it has conditional defines
                        // Otherwise, it won't be possible to tell which inlined conditions are associated with which separately defined conditions
                        // It might be possible to do this later with a special syntax
                        foreach (FlowNode define in use.Defines)
                        {
                            if (node.Ancestors.Contains(define) && !node.Dominators.Contains(define))
                            {
                                noInlineGroups.Add(use.ResultGroup);
                            }
                        }
                    }
                }
            }

            // Rewrite condition group usage (CondRef) to either inline or not
            // ConditionDAG is not updated here so it can't be used afterwards.
            foreach (FlowNode node in NodeList())
            {
                if (!options.InlineDefinitions)
                {
                    break;
                }
                if (node.Im is CondIntermediate condIm)
                {
                    bool stopRewrite = false;
                    FlowNode expectedPrev = node.Pred;

                    // Postorder recursion the conds.
                    Cond newCond = condIm.Cond.RewriteCond(c =>
                    {
                        if (stopRewrite)
                        {
                            return null;
                        }
                        if (!(c is CondRef cond && !cond.Compiled))
                        {
                            return null;
                        }
                        ConditionDAG use = node.Uses.Find(d => cond.Group == d.ResultGroup);
                        if (use == null)
                        {
                            // This probably shouldn't happen, meaning the use list of out of date, but leaving in a standalone CondRef is fine.
                            return null;
                        }

                        // Ignore this group if its definition(s) can't be inlined.
                        if (noInlineGroups.Contains(use.ResultGroup))
                        {
                            if (!options.InlineDefinitionsOutOfOrder)
                            {
                                stopRewrite = true;
                            }
                            return null;
                        }

                        List<FlowNode> usedDefines = new List<FlowNode>();
                        foreach (FlowNode define in use.Defines)
                        {
                            if (!Nodes.ContainsKey(define.ID))
                            {
                                // This can happen if there's been rewriting prior to this point.
                                // In this context it is at least safe to ignore, since the define is now somewhere else reachable.
                                continue;
                            }
                            // If this defined is in the future, don't touch it.
                            if (!node.Ancestors.Contains(define)) continue;
                            if (!node.Dominators.Contains(define))
                            {
                                throw new Exception($"Internal error: definition {define} of usage {node} is optional, but not in inline blocklist {string.Join(",", noInlineGroups)}");
                            }
                            usedDefines.Add(define);
                        }
                        if (usedDefines.Count == 0)
                        {
                            // Likely shouldn't happen (use-without-define?) but nothing to do in this case.
                            return null;
                        }

                        if (!options.InlineDefinitionsOutOfOrder)
                        {
                            // Mode to avoid changing the ordering of condition group statements.
                            // Normally, this is harmless, because condition groups don't have side effects.
                            // The main case where this becomes an issue is action buttons, which aren't.
                            foreach (FlowNode define in Enumerable.Reverse(usedDefines))
                            {
                                if (define == expectedPrev)
                                {
                                    expectedPrev = define.Pred;
                                }
                                else
                                {
                                    stopRewrite = true;
                                    return null;
                                }
                            }
                        }

                        // Inline all inlinable uses into a single OpCond. (If there's just one, this gets collapsed later.)
                        OpCond op = new OpCond { Negate = cond.Negate, And = use.ResultGroup > 0 };

                        foreach (FlowNode define in usedDefines)
                        {
                            CondAssign assign = (CondAssign)define.Im;
                            op.Ops.Add(assign.Cond);
                            RemoveNode(Nodes[assign.ID], condIm);
                        }
                        if (op.Ops.Count == 1)
                        {
                            // Don't create a useless OpCond wrapper.
                            // (This does an in-place mutation, maybe avoid that)
                            Cond inner = op.Ops[0];
                            if (cond.Negate) inner.Negate = !inner.Negate;
                            return inner;
                        }
                        else
                        {
                            return op;
                        }
                    }, false);
                    condIm.Cond = newCond;
                }
            }

            // Trivial addition of WaitFors (replace main condition group)
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is CondAssign assign && assign.ToCond == 0)
                {
                    Intermediate im2 = new Wait
                    {
                        Cond = assign.Cond,
                        Decorations = assign.Decorations,
                    };
                    im2.Labels = assign.Labels;
                    im2.ID = assign.ID;
                    node.Im = im2;
                }
            }

            // Two passes for condition group renaming (CondAssign definitions and CondRef uses)
            // In the first pass, collect information on all condition groups not yet inlined: their presence and how many definitions.
            // In the second pass, do actual replacements.
            List<int> renameAllGroups = new List<int>();
            List<int> renameDefineGroups = new List<int>();
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is CondIntermediate condIm)
                {
                    if (condIm is CondAssign assign)
                    {
                        renameAllGroups.Add(assign.ToCond);
                        renameDefineGroups.Add(assign.ToCond);
                    }
                    condIm.Cond.WalkCond(c =>
                    {
                        if (c is CondRef cond)
                        {
                            if (cond.Group == 0)
                            {
                                // This is basically malformed emevd. However, DS3 does it as a hacky way
                                // of commenting out an uncompiled condition group use case. It's called
                                // mainGroupAbuse below. It doesn't preserve it, but same behavior in the end.
                            }
                            else
                            {
                                renameAllGroups.Add(cond.Group);
                            }
                        }
                    });
                }
            }

            // Simple names, to check roundtrip. If better names are enabled, those are used instead.
            Dictionary<int, string> renameNames = renameAllGroups.Distinct().ToDictionary(a => a, a => a > 0 ? $"and{a}" : $"or{-a}");
            renameNames[0] = "mainGroupAbuse";

            Dictionary<string, List<int>> fancyNameVariants = new Dictionary<string, List<int>>();
            string getName(int group)
            {
                if (fancyCondNames.TryGetValue(group, out string name))
                {
                    if (!fancyNameVariants.TryGetValue(name, out List<int> vars))
                    {
                        vars = fancyNameVariants[name] = new List<int>();
                    }
                    // Number identically named groups in source order, like cond, cond2, cond3
                    int index = vars.IndexOf(group);
                    if (index == -1)
                    {
                        index = vars.Count;
                        vars.Add(group);
                    }
                    return name + (index == 0 ? "" : $"{index + 1}");
                }
                return renameNames[group];
            }

            List<int> singletons = renameDefineGroups.GroupBy(a => a).Where(a => a.Count() == 1).Select(a => a.Key).ToList();
            foreach (FlowNode node in NodeList())
            {
                if (node.Im is CondIntermediate condIm)
                {
                    if (condIm is CondAssign assign)
                    {
                        // This is a little ad hoc, and mainly because singletons look better in output code.
                        // Singletons are AND by default so this may lead to running out of those.
                        if (singletons.Contains(assign.ToCond)) assign.Op = CondAssignOp.Assign;
                        assign.ToVar = getName(assign.ToCond);
                    }
                    condIm.Cond = condIm.Cond.RewriteCond(c =>
                    {
                        if (c is CondRef cond)
                        {
                            cond.Name = getName(cond.Group);
                            return cond;
                        }
                        return null;
                    });
                }
            }

            // Mark subsequent gotos, which can't be rewritten into if-else without backtracking in n-1 of them, so disallow all n
            FlowNode prevNode = null;
            foreach (FlowNode node in NodeList())
            {
                if (prevNode != null && prevNode.JumpTo != null && node.JumpTo != null && prevNode.JumpTo.ID <= node.JumpTo.ID)
                {
                    prevNode.MultiGoto = true;
                    node.MultiGoto = true;
                }
                prevNode = node;
            }

            // Fully update dominators before if/else structuring.
            foreach (FlowNode node in NodeList())
            {
                node.Dominators.RemoveAll(n => !Nodes.ContainsKey(n.ID));

                // Because lexical structuring is next, treat end-event as just another instruction.
                node.Ancestors = new List<FlowNode> { node };
                foreach (FlowNode pred in node.AllPreds(ignoreEnd: true))
                {
                    node.Ancestors = node.Ancestors.Union(pred.Ancestors).ToList();
                }
            }

            // Now that nodes have been updated, take transitive reduction to calculate immediate dominators
            // Also, calculate dominance frontier for weird multi-join edge cases, when they arise.
            Dictionary<FlowNode, List<FlowNode>> dominanceFrontier = new Dictionary<FlowNode, List<FlowNode>>();
            foreach (FlowNode node in NodeList())
            {
                node.ImmedDominators = new List<FlowNode>();
                foreach (FlowNode dom in node.Dominators.ToList())
                {
                    if (!node.Dominators.Any(dom2 => dom2.Dominators.Contains(dom)))
                    {
                        node.ImmedDominators.Add(dom);
                    }
                }
                List<FlowNode> frontiered = node.AllPreds(ignoreEnd: true).SelectMany(p => p.Dominators).Distinct().Where(p => !node.Dominators.Contains(p)).ToList();
                foreach (FlowNode predDom in frontiered)
                {
                    if (!dominanceFrontier.ContainsKey(predDom))
                    {
                        dominanceFrontier[predDom] = new List<FlowNode>();
                    }
                    dominanceFrontier[predDom].Add(node);
                }
                string displayDom(FlowNode d) => $"{d.ID}{(node.ImmedDominators.Contains(d) ? "*" : "")}";
                if (debugPrint) Console.WriteLine($"node {node.ID} dominators: {string.Join(" ", node.Dominators.Select(displayDom))} {string.Join(",", frontiered)}");
            }

            // Branching points which may have a higher-level node using their follow, to delay to using that higher-level node.
            List<FlowNode> unresolved = new List<FlowNode>();
            // From conditional nodes to their follows
            Dictionary<FlowNode, FlowNode> ifFollow = new Dictionary<FlowNode, FlowNode>();

            // At this point, reverse postorder should still be instruction ID order.
            List<FlowNode> ifPostorder = NodeList();
            ifPostorder.Reverse();
            foreach (FlowNode node in ifPostorder)
            {
                if (node.Succ == null || node.AlwaysJump || node.JumpTo == null || !(node.Im is CondIntermediate)) continue;
                if (node.MultiGoto) continue;
                List<FlowNode> immeds = ifPostorder.Where(d => d.ImmedDominators.Contains(node)).ToList();

                // A few events which could be possibly improved:
                // Missing a nested if in Sekiro 11105720
                // Many GotoIfs in a row in DS1 840, maybe specialized syntax

                // Follows need to have something jumping to them
                List<FlowNode> multiImmeds = immeds.Where(im => im.JumpPred.Count > 0).ToList();
                FlowNode follow = multiImmeds.OrderByDescending(im => im.ID).DefaultIfEmpty().First();

                // Sanity checks: does follow actually follow from all branches? And does it cross any existing ones?
                // (The "crossing" case is usually taken care of by MultiGoto, but not always.)
                bool crossesExisting(FlowNode n)
                {
                    return ifFollow.Any(followPair => n.ID <= followPair.Key.ID && n.JumpTo.ID > followPair.Key.ID && n.JumpTo.ID < followPair.Value.ID);
                }
                if (follow != null)
                {
                    bool riskyFollow = false;
                    if (!immeds.All(im => follow.Ancestors.Contains(im)))
                    {
                        // This might exclude some if+elseif structurings, like in Sekiro 11005712
                        if (debugPrint) Console.WriteLine($"Risky follow {follow}, immeds missing ancestors {string.Join(",", follow.Ancestors)}");
                        riskyFollow = true;
                    }
                    if (crossesExisting(node))
                    {
                        if (debugPrint) Console.WriteLine($"Risky follow {follow}, crossing {node.JumpTo} into {string.Join(",", ifFollow)}");
                        riskyFollow = true;
                    }
                    if (riskyFollow) follow = null;
                }
                if (debugPrint) Console.WriteLine($"For node {node.ID} with {string.Join(",", immeds)}, they merge back into {string.Join(",", multiImmeds)}, chosen {follow}");
                if (follow == null)
                {
                    // Resolve this at a higher scope. Unless it dominates nothing.
                    // This dominator check could also be earlier, but maybe this is more efficient.
                    if (!ifPostorder.Any(d => d.Dominators.Contains(node))) continue;
                    unresolved.Add(node);
                }
                else
                {
                    ifFollow[node] = follow;
                    List<FlowNode> extraFollows = new List<FlowNode>();
                    foreach (FlowNode otherNode in unresolved)
                    {
                        if (dominanceFrontier.TryGetValue(otherNode, out List<FlowNode> frontier) && frontier.Contains(follow))
                        {
                            // This is a rather iffy heuristic. May need to have a more coherent "is actually really legit follow node" routine.
                            bool crosses = crossesExisting(otherNode);
                            if (debugPrint) Console.WriteLine($"Unresolved frontier {otherNode} {otherNode.Im}->{follow} {follow.Im}, jump {otherNode.JumpTo}, crosses {crosses}");
                            if (otherNode.JumpTo.ID <= follow.ID && !crosses)
                            {
                                extraFollows.Add(otherNode);
                            }
                        }
                    }
                    if (debugPrint && extraFollows.Count > 0) Console.WriteLine($"  + nested nodes [{string.Join(", ", extraFollows)}]");
                    unresolved.RemoveAll(otherNode =>
                    {
                        if (extraFollows.Contains(otherNode))
                        {
                            ifFollow[otherNode] = follow;
                            return true;
                        }
                        return false;
                    });
                }
            }

            HashSet<Goto> explicitGotos = new HashSet<Goto>();
            SortedDictionary<int, int> implicitGotos = new SortedDictionary<int, int>();
            HashSet<int> placedNodes = new HashSet<int>();
            List<Intermediate> structure(FlowNode node, List<FlowNode> follows, List<FlowNode> elses, int depth = 0)
            {
                List<Intermediate> ims = new List<Intermediate>();
                while (node != null)
                {
                    if (follows.Contains(node))
                    {
                        return ims;
                    }
                    if (debugPrint && ifFollow.TryGetValue(node, out FlowNode f) && elses.Contains(f)) Console.WriteLine($"Nested follow {node}->{f} within [{string.Join(",", follows)}]");
                    if (ifFollow.TryGetValue(node, out FlowNode follow) && !elses.Contains(follow))
                    {
                        if (debugPrint) Console.WriteLine($"Follow chain: {node} -> {follow}");
                        // This shouldn't happen.
                        if (node == follow) throw new Exception($"Internal error: bad self-follow {node}");
                        // Should be checked already
                        if (node.Succ == null || node.AlwaysJump || node.JumpTo == null || !(node.Im is CondIntermediate))
                        {
                            throw new Exception($"Internal error: bad if/else starting node {node}");
                        }
                        FlowNode next = node.Succ;
                        FlowNode jump = node.JumpTo;
                        Cond cond = ((CondIntermediate)node.Im).Cond;
                        // Should probably be making defensive copies
                        cond.Negate = !cond.Negate;
                        IfElse im = new IfElse { Cond = cond, Decorations = node.Im.Decorations };
                        im.ID = node.Im.ID;
                        node.Im = im;
                        if (jump == follow || follows.Contains(jump))
                        {
                            im.True = structure(
                                next,
                                follows.Concat(new[] { follow }).ToList(),
                                elses, depth+1);
                            ims.Add(im);
                        }
                        else if (!(jump.Pred.Im is Goto g && g.Cond.Always && g.ToNode == follow.ID))
                        {
                            // We may get this point and have something which can't fit into an if/else,
                            // because the if branch execution can end up in the else branch somehow.
                            // In this case, there cannot be an else branch, and the goto cannot be excised.
                            im.True = structure(
                                next,
                                follows.Concat(new[] { jump, follow }).ToList(),
                                elses.Concat(new[] { jump, follow }).ToList(), depth+1);
                            ims.Add(im);
                            ims.AddRange(structure(
                                jump,
                                follows.Concat(new[] { follow }).ToList(),
                                elses, depth+1));
                        }
                        else
                        {
                            im.True = structure(
                                next,
                                follows.Concat(new[] { jump, follow }).ToList(),
                                elses.Concat(new[] { jump, follow }).ToList(), depth+1);
                            if (im.True.Count > 0 && im.True[im.True.Count - 1] is Goto h && h.Cond.Always && h.ToNode == follow.ID)
                            {
                                implicitGotos[h.ID] = h.ToNode;
                                explicitGotos.Remove(h);
                                im.True.RemoveAt(im.True.Count - 1);
                            }
                            else
                            {
                                throw new Exception($"Internal error: nonstandard if/else {node} {im}");
                            }
                            im.False = structure(
                                jump,
                                follows.Concat(new[] { follow }).ToList(),
                                elses, depth+1);
                            ims.Add(im);
                        }
                        node = follow;
                    }
                    else
                    {
                        // Sanity check for overlapping follows and other weird cases
                        if (placedNodes.Contains(node.Im.ID))
                        {
                            throw new Exception($"Internal error: trying to place {node} {node.Im} twice in structuring {ID}");
                        }
                        placedNodes.Add(node.Im.ID);
                        // Node should be null iff at the end
                        if (node.Im is Goto g && g.ToLabel == null)
                        {
                            explicitGotos.Add(g);
                        }
                        ims.Add(node.Im);
                        node = node.Succ;
                    }
                }
                return ims;
            }

            func.Body = structure(First, new List<FlowNode>(), new List<FlowNode>());

            // In case there is a goto *to* a goto, it needs to go to the ultimate target instead.
            // Only encountered in bloodborne 12901400.
            // And just in case there's a goto to a goto to a goto etc., take a transitive reduction.
            foreach (KeyValuePair<int, int> pair in Enumerable.Reverse(implicitGotos))
            {
                if (implicitGotos.TryGetValue(pair.Value, out int ultimateTarget))
                {
                    implicitGotos[pair.Key] = ultimateTarget;
                }
            }

            // Add synthetic labels to any skips not rewritten
            SortedSet<int> labelTargets = new SortedSet<int>(explicitGotos.Where(g => !implicitGotos.ContainsKey(g.ToNode)).Select(g => g.ToNode));
            Dictionary<int, string> gotoNames = labelTargets.Select((n, i) => (n, i)).ToDictionary(e => e.Item1, e => $"S{e.Item2}");

            // We can still use CFG traversal here, but can't add/remove/swap intermediates anymore, and some nodes may be gone.
            foreach (FlowNode node in NodeList())
            {
                if (implicitGotos.ContainsKey(node.ID)) continue;
                if (gotoNames.TryGetValue(node.ID, out string label))
                {
                    node.Im.Labels.Add(label);
                }
                if (node.Im is Goto g && g.ToLabel == null)
                {
                    // Set the goto's label to the synthetic one.
                    // In theory, this could be a regular label, but we'd have to be confident that it's referring to
                    // the next instance of said label, since label commands can be repeated an arbitrary number of times.
                    int target = g.ToNode;
                    if (implicitGotos.TryGetValue(target, out int ultimateTarget))
                    {
                        target = ultimateTarget;
                    }
                    if (gotoNames.TryGetValue(target, out label))
                    {
                        g.ToLabel = label;
                    }
                    else
                    {
                        throw new Exception($"Internal error: no label found for goto {node} {g}");
                    }
                }
            }

            if (debugPrint)
            {
                func.Print(Console.Out);
                Console.WriteLine();
            }

            func.Fancy = true;

            return result;
        }
    }
}
