using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    public class ScriptAst
    {
        public static readonly string SingleIndent = "    ";
        private static readonly int columnLimit = 100;

        public class EventFunction
        {
            // Normally an int, but may be a SourceNode :fatcat:
            public object ID { get; set; }
            public Event.RestBehaviorType RestBehavior { get; set; }
            public bool Fancy { get; set; }
            public List<string> Params = new List<string>();
            public List<Intermediate> Body = new List<Intermediate>();

            // Cosmetics
            public List<SourceDecoration> EndComments = null;
            public LineMapping LineMapping { get; set; }
            public string Header { get; set; }
            // Repack coherency
            public Dictionary<int, string> CondHints = null;

            public override string ToString() => $"{(Fancy ? "$" : "")}Event({ID}, {RestBehavior}, function({string.Join(",", Params)}) {{ {string.Join(" ", Body)} }});";

            public void Print(TextWriter writer)
            {
                LineTrackingWriter lineWriter = writer as LineTrackingWriter;
                lineWriter?.RecordMapping(LineMapping);
                string processDecorations(List<SourceDecoration> decs, string sp)
                {
                    if (decs == null) return "";
                    string suffix = "";
                    foreach (SourceDecoration dec in decs)
                    {
                        if (dec.Type == SourceDecoration.DecorationType.PRE_BLANK)
                        {
                            writer.WriteLine();
                        }
                        else if (dec.Type == SourceDecoration.DecorationType.PRE_COMMENT)
                        {
                            writer.WriteLine(sp + dec.Comment);
                        }
                        else if (dec.Type == SourceDecoration.DecorationType.POST_COMMENT)
                        {
                            suffix += " " + dec.Comment;
                        }
                    }
                    return suffix;
                }
                // For some reason, in particular scoped cases, V8 cries bloody murder about label redeclaration
                // (label_redeclaration in parser.cc). But ECMAScript spec doesn't seem to require it? V8 just
                // does want it wants with no clear pattern.
                // Disambiguation can be done before printing if desired, but it is a syntax thing, not a semantic one.
                // It mainly seems to arise when two label commands are literally next to each other, and when declared
                // in a parent scope right before a child scope which also includes it.
                // Well, for now, hardcode the only vanilla event which causes this. Fix me :fatcat:
                bool disambiguateLabels = 11052860.Equals(ID);
                List<string> usedLabels = new List<string>();
                string getLabelWithSuffix(string label)
                {
                    if (!disambiguateLabels) return label;
                    // To avoid complication, all trailing underscores are stripped on parsing labels,
                    // though not label references.
                    while (usedLabels.Contains(label))
                    {
                        label += "_";
                    }
                    usedLabels.Add(label);
                    return label;
                }
                void subprint(List<Intermediate> ims, int indent, string prefix = "")
                {
                    string sp = string.Join("", Enumerable.Repeat(SingleIndent, indent));
                    bool prevLabel = false;
                    foreach (Intermediate im in ims)
                    {
                        string suffix = processDecorations(im.Decorations, sp);
                        foreach (string l in im.Labels)
                        {
                            writer.WriteLine(getLabelWithSuffix(l) + ":");
                        }
                        lineWriter?.RecordMapping(im.LineMapping);
                        if (im is NoOp && im.Labels.Count == 0 && !prevLabel)
                        {
                            // Don't print NoOp unless it is needed for synthetic or actual labels.
                        }
                        else if (im is Label label)
                        {
                            writer.WriteLine(getLabelWithSuffix($"L{label.Num}") + ":" + suffix);
                            if (im == ims[ims.Count - 1])
                            {
                                // Slight hack to synthetically add NoOp at the end of a block, not just end of the function
                                // This is kind of a syntactic extension to Label itself.
                                writer.WriteLine(sp + "NoOp();");
                            }
                        }
                        else
                        {
                            string fullLine = sp + prefix + im + suffix;
                            if (fullLine.Length > columnLimit && Fancy)
                            {
                                writer.WriteLine(sp + prefix + im.GetStringTree().Render(sp) + suffix);
                                lineWriter?.PostMapping(im.LineMapping);
                            }
                            else
                            {
                                writer.WriteLine(fullLine);
                            }
                            if (im is IfElse ifIm)
                            {
                                subprint(ifIm.True, indent + 1);
                                if (ifIm.False.Count == 0)
                                {
                                    writer.WriteLine(sp + $"}}");
                                }
                                else if (ifIm.False.Count == 1 && ifIm.False[0] is IfElse)
                                {
                                    // Use recursion for this, to avoid duplicating StringTree etc call sites
                                    // Iteration could be possible if this becomes a performance problem, or if prefix is unsustainable
                                    subprint(ifIm.False, indent, $"}} else ");
                                }
                                else
                                {
                                    writer.WriteLine(sp + $"}} else {{");
                                    subprint(ifIm.False, indent + 1);
                                    writer.WriteLine(sp + $"}}");
                                }
                            }
                            else if (im is LoopStatement loopIm)
                            {
                                subprint(loopIm.Body, indent + 1);
                                writer.WriteLine(sp + $"}}");
                            }
                        }
                        prevLabel = im is Label;
                        prefix = "";
                    }
                }
                writer.WriteLine($"{(Fancy ? "$" : "")}Event({ID}, {RestBehavior}, function({string.Join(", ", Params)}) {{");
                subprint(Body, 1);
                string funcSuffix = processDecorations(EndComments, SingleIndent);
                writer.WriteLine($"}});{funcSuffix}");
            }
        }

        // A list of reserved words, plus some basic info to show in the editor.
        // COND, LABEL, and LAYERS are special strings here. These could probably be factored with ArgDoc into a third simplified object.
        // The list is otherwise used in compilation so people don't define or use condition variables with these names.
        public static readonly Dictionary<string, BuiltIn> ReservedWords = new Dictionary<string, BuiltIn>
        {
            ["EndEvent"] = new BuiltIn
            {
                Doc = "Stops execution in the event and turns its flag on.",
                ControlStatement = true,
            },
            ["RestartEvent"] = new BuiltIn
            {
                Doc = "Stops execution in the event, turns its flags on, and continues from the top on the next frame.",
                ControlStatement = true,
            },
            ["EndIf"] = new BuiltIn
            {
                Args = new List<object> { "COND" },
                Doc = "If the condition is true, stops execution in the event and turns its flag on.",
                Highlight = true,
                ControlStatement = true,
            },
            ["RestartIf"] = new BuiltIn
            {
                Args = new List<object> { "COND" },
                Doc = "If the condition is true, stops execution in the event, turns its flags on, and continues from the top on the next frame.",
                Highlight = true,
                ControlStatement = true,
            },
            ["Goto"] = new BuiltIn
            {
                Args = new List<object> { "LABEL" },
                Doc = "Unconditionally goes to the next instance of the given label.",
                Highlight = true,
                ControlStatement = true,
            },
            ["GotoIf"] = new BuiltIn
            {
                Args = new List<object> { "LABEL", "COND" },
                Doc = "Goes to the next instance of the given label if the condition is true.",
                Highlight = true,
                ControlStatement = true,
            },
            ["WaitFor"] = new BuiltIn
            {
                Args = new List<object> { "COND" },
                Doc = "Waits for the condition to become true. After this, all condition variables are reset.",
                Highlight = true,
                ControlStatement = true,
            },
            ["NoOp"] = new BuiltIn
            {
                Args = new List<object> { },
                Doc = "Does nothing. Not an instruction. Exists only as a target for labels.",
                ControlStatement = true,
            },
            ["Event"] = new BuiltIn
            {
            },
            ["$Event"] = new BuiltIn
            {
            },
            ["$LAYERS"] = new BuiltIn
            {
                Args = new List<object> { "LAYERS" },
                Doc = "Added to the end of an instruction argument list to make it only run in certain ceremony layers. See also IfMapCeremonyState."
            },
        };

        public class BuiltIn
        {
            // Types of arguments, as interpreted by SharedControls
            public List<object> Args { get; set; }
            // Builtin name
            public string Doc { get; set; }
            // Experimental feature to use different syntax highlighting for control flow commands.
            // This is unused at present because it looks ugly.
            public bool Highlight { get; set; }
            // Whether this keyword is a statement which is always followed by (
            public bool ControlStatement { get; set; }
        }

        public class LineMapping : IComparable<LineMapping>
        {
            // These are all 1-indexed
            public int SourceLine { get; set; }
            public int SourceEndLine { get; set; }
            public int PrintedLine { get; set; }
            public int PrintedEndLine { get; set; }

            public int CompareTo(LineMapping other) => PrintedLine.CompareTo(other.PrintedLine);
            public override string ToString() => $"LineMapping{{output={PrintedLine}, source={SourceLine}:{SourceEndLine}}}";
            public LineMapping Clone() => (LineMapping)MemberwiseClone();
        }

        public abstract class Intermediate
        {
            public int ID = -1;

            // Synthetic labels only
            public List<string> Labels = new List<string>();
            // Comments and nonfunctional things in compilation
            public List<SourceDecoration> Decorations { get; set; }
            public LineMapping LineMapping { get; set; }
            public string ToLabelHint { get; set; }

            public void MoveDecorationsTo(Intermediate other)
            {
                if (Decorations != null && other != this)
                {
                    if (other.Decorations == null) other.Decorations = new List<SourceDecoration>();
                    other.Decorations.AddRange(Decorations);
                    Decorations = null;
                }
                other.LineMapping = LineMapping == null ? LineMapping : LineMapping.Clone();
                other.ToLabelHint = ToLabelHint;
            }

            public void MoveDecorationsTo(EventFunction func)
            {
                if (Decorations != null)
                {
                    if (func.EndComments == null) func.EndComments = new List<SourceDecoration>();
                    func.EndComments.AddRange(Decorations);
                    Decorations = null;
                }
            }

            public virtual StringTree GetStringTree() => StringTree.Of(this);
            public virtual bool IsMeta => false;
        }

        public class SourceDecoration
        {
            public enum DecorationType { PRE_COMMENT, POST_COMMENT, PRE_BLANK }
            public DecorationType Type { get; set; }
            public int Position { get; set; }
            public string Comment { get; set; }
        }

        public class NoOp : Intermediate
        {
            public override string ToString() => $"NoOp();";
        }

        private static string ArgString(List<object> args, object layer)
        {
            return string.Join(", ", layer == null ? args : args.Concat(new object[] { layer }));
        }

        public class Layers
        {
            public uint Mask { get; set; }
            public override string ToString() => InstructionDocs.LayerString(Mask);
        }

        public class Instr : Intermediate
        {
            // The actual instruction from the source. Only used in decompilation (compilation uses V8).
            // TODO: Is this actually used anywhere?
            public Instruction Inner { get; set; }
            // The command id, like 3[00], from the source.
            public string Cmd { get; set; }
            // The command name from EMEDF.
            public string Name { get; set; }
            // The values for each of the args.
            // In decompilation, these are the literal values from emevd, or an EnumInt for pretty printing enums.
            // In compilation, these may be JavaScript expression but they are copied into V8 as-is.
            public List<object> Args = new List<object>();
            // Layer
            public object Layers { get; set; }

            public override string ToString() => $"{Name}({ArgString(Args, Layers)});";
        }

        // Can appear as an arg in a List<object> of args, like X0_4
        public class ParamArg
        {
            public string Name { get; set; }
            public override string ToString() => Name;
        }

        public class DisplayArg
        {
            public string DisplayValue { get; set; }
            // Primitive type
            public object Value { get; set; }

            public override string ToString() => DisplayValue;
        }

        public class Label : Intermediate
        {
            public int Num { get; set; }

            public override string ToString() => $"L{Num}:";
        }

        public class JSStatement : Intermediate
        {
            // Allow JS statements to be passed through compilation in some cases
            public string Code { get; set; }

            public override string ToString() => Code;
            public override bool IsMeta => true;
        }

        public class LoopStatement : Intermediate
        {
            // Header statement like: for (let i = 0; i < 10; i++)
            public string Code { get; set; }
            public List<Intermediate> Body = new List<Intermediate>();

            public override string ToString() => $"{Code} {{";
        }

        public class LoopHeader : Intermediate
        {
            public string Code { get; set; }
            public int ToNode = -1;

            public override string ToString() => $"{Code} {{";
            public override bool IsMeta => true;
        }

        // NoOp but for jump-to-header loop ends
        public class LoopFooter : Intermediate
        {
            public override string ToString() => "}";
            public override bool IsMeta => true;
        }

        public class FillSkip : Intermediate
        {
            // Label should start with # per convention, and match a single earlier ReverseSkip
            public string Label { get; set; }

            public override string ToString() => $"_FillSkip(\"{Label}\");";
        }

        public enum ControlType
        {
            COND, SKIP, END, GOTO, WAIT
        }

        public abstract class CondIntermediate : Intermediate
        {
            // The condition this control statement is operating on
            public Cond Cond { get; set; }
            protected string PlainCond {
                get
                {
                    string c = Cond.ToString();
                    if (c.Length >= 3 && c[0] == '(' && c[c.Length - 1] == ')') return c.Substring(1, c.Length - 2);
                    return c;
                }
            }
            // In compilation, a hint for the command this should turn into
            // public string Cmd { get; set; }

            public abstract ControlType ControlType { get; }
            public abstract object ControlArg { get; }
        }

        public class End : CondIntermediate
        {
            public int Type { get; set; }

            public override ControlType ControlType => ControlType.END;
            public override object ControlArg => Type;
            private string EndName => Cond.Always ? (Type == 0 ? "EndEvent" : "RestartEvent") : (Type == 0 ? "EndIf" : "RestartIf");
            public override string ToString() => $"{EndName}({(Cond.Always ? "" : PlainCond)});";
            public override StringTree GetStringTree() => Cond.Always
                ? StringTree.Of(this)
                : StringTree.IsolatedStart($"{EndName}(", Cond.GetStringTree(true), ");");
        }

        public class CondAssign : CondIntermediate
        {
            public int ToCond { get; set; }
            public string ToVar { get; set; }
            public CondAssignOp Op { get; set; }

            public override ControlType ControlType => ControlType.COND;
            public override object ControlArg => ToCond;
            public override string ToString() => $"{ToVar ?? $"cond[{ToCond}]"} {StrAssign[Op]} {PlainCond};";
            public override StringTree GetStringTree() => StringTree.CombinedStart($"{ToVar} {StrAssign[Op]} ", Cond.GetStringTree(true), ";");
        }

        private static readonly Dictionary<CondAssignOp, string> StrAssign = new Dictionary<CondAssignOp, string>
        {
            [CondAssignOp.Assign] = "=",
            [CondAssignOp.AssignOr] = "|=",
            [CondAssignOp.AssignAnd] = "&=",
        };
        public enum CondAssignOp
        {
            Assign, AssignOr, AssignAnd
        }

        public static readonly Dictionary<string, int> LabelIds = Enumerable.Range(0, 21).ToDictionary(l => $"L{l}", l => l);
        public class ReserveSkip
        {
            // Argument to be replaced at runtime with a skip offset.
            // Must be the first argument of the instruction.
            public string Target { get; set; }
            public override string ToString() => $"_ReserveSkip(\"{Target}\")";
        }

        public class Goto : CondIntermediate
        {
            public int SkipLines = -1;
            // public Node To { get; set; }
            public int ToNode = -1;
            public string ToLabel { get; set; }

            public override ControlType ControlType => ToLabel == null || ToLabel.StartsWith("#") ? ControlType.SKIP : ControlType.GOTO;
            public override object ControlArg
            {
                get
                {
                    if (ToLabel == null)
                    {
                        return SkipLines;
                    }
                    if (LabelIds.TryGetValue(ToLabel, out int id))
                    {
                        return id;
                    }
                    if (ToLabel.StartsWith("#"))
                    {
                        // Syntax for constructed jumps
                        return new ReserveSkip { Target = ToLabel };
                    }
                    throw new Exception($"Internal error: Unresolved label {ToLabel}");
                }
            }

            // Of these, only Goto and GotoIf should be present in the final script.
            public override string ToString() => Cond.Always
                ? $"{(ToLabel == null ? (ToNode >= 0 ? $"GotoInternal({ToNode}" : $"SkipLines({SkipLines}") : $"Goto({ToLabel}")});"
                : $"{(ToLabel == null ? (ToNode >= 0 ? $"GotoIfInternal({ToNode}" : $"SkipLinesIf({SkipLines}") : $"GotoIf({ToLabel}")}, {PlainCond});";
            public override StringTree GetStringTree() => Cond.Always
                ? base.GetStringTree()
                : StringTree.IsolatedStart($"GotoIf({ToLabel}, ", Cond.GetStringTree(true), ");");
        }

        public class IfElse : CondIntermediate
        {
            public List<Intermediate> True = new List<Intermediate>();
            public List<Intermediate> False = new List<Intermediate>();
            public override string ToString() => $"if ({PlainCond}) {{";
            public override StringTree GetStringTree() => StringTree.CombinedStart("if (", Cond.GetStringTree(true), ") {");

            // This is functionally a goto, but can't be compiled
            public override object ControlArg => throw new Exception();
            public override ControlType ControlType => throw new Exception();
        }

        public class Wait : CondIntermediate
        {
            // Currently unused. WaitForConditionGroupState is transformed into main cond definition since it's
            // only used in simple circumstances in Sekiro (TODO can investigate if it's interchangeable in all
            // cases, but no one should be using it anyway), and the other ones don't affect dataflow or control
            // flow so they can be treated as normal Instrs.
            public bool Special { get; set; }

            // In terms of compiler output, this is used to mean main group evaluation
            public override ControlType ControlType => ControlType.COND;
            public override object ControlArg => 0;
            // Cond should always be a specific condition and not Always.
            public override string ToString() => $"WaitFor({PlainCond});";
            public override StringTree GetStringTree() => StringTree.IsolatedStart("WaitFor(", Cond.GetStringTree(true), ");");
        }

        public abstract class Cond
        {
            public bool Negate { get; set; }
            protected string Prefix => Negate ? "!" : "";
            public bool Always => this is CmdCond cond && cond.Name == "Always";
            public abstract string DocName { get; }
            public virtual StringTree GetStringTree(bool stripParens = false) => StringTree.Of(this);

            public static Cond ALWAYS = new CmdCond { Name = "Always" };

            public void WalkCond(Action<Cond> visitor, bool preorder = true)
            {
                RewriteCond(cond =>
                {
                    visitor(cond);
                    return null;
                }, preorder);
            }

            public Cond RewriteCond(Func<Cond, Cond> visitor, bool preorder = true)
            {
                if (this is OpCond op)
                {
                    List<Cond> conds = new List<Cond>(op.Ops);
                    for (int i = 0; i < conds.Count; i++)
                    {
                        int offset = preorder ? i : conds.Count - 1 - i;
                        conds[offset] = conds[offset].RewriteCond(visitor, preorder);
                    }
                    op.Ops = conds;
                }
                return visitor(this) ?? this;
            }
        }

        public class CompareCond : Cond
        {
            public ComparisonType Type { get; set; }
            public CmdCond CmdLhs { get; set; }
            public object Lhs { get; set; }
            // Can be int, float, byte, etc.
            public object Rhs { get; set; }

            public override string DocName => CmdLhs == null ? "Op" : CmdLhs.DocName;
            private string CompareOp => StrComparison[Negate ? OppositeComparison[Type] : Type];
            public override string ToString() => $"{(Lhs == null ? CmdLhs : Lhs)} {CompareOp} {Rhs}";
        }

        public class CmdCond : Cond
        {
            public string Name { get; set; }
            public List<object> Args = new List<object>();

            public override string DocName => Name;
            public override string ToString() => $"{Prefix}{Name}({ArgString(Args, null)})";
        }

        public class CondRef : Cond
        {
            public bool Compiled { get; set; }
            // This is just temporary state, and should only be if a condition definition is variable-eligible
            // public int ToNode { get; set; }
            // public int ToIndex = -1;
            public int Group { get; set; }
            // Can add an RHS here if needed
            public string Name { get; set; }

            public override string DocName => Compiled ? "CompiledConditionGroup" : "ConditionGroup";
            public override string ToString() => $"{Prefix}{Name ?? $"cond[{Group}]"}{(Compiled ? ".Passed" : "")}";
        }

        public class OpCond : Cond
        {
            public List<Cond> Ops = new List<Cond>();
            public bool And { get; set; }

            public override string DocName => throw new Exception($"Internal error: no built-in command corresponding to {this}");
            private string CombineOp => And ? " && " : " || ";
            public override string ToString() => $"{Prefix}({string.Join(CombineOp, Ops)})";
            public override StringTree GetStringTree(bool stripParens = false) => new StringTree
            {
                Children = Ops.Select(c => c.GetStringTree()).ToList(),
                Sep = CombineOp,
                Start = stripParens && Prefix == "" ? "" : $"{Prefix}(",
                End = stripParens && Prefix == "" ? "" : ")",
            };
        }

        public class ErrorCond : Cond
        {
            public string Message { get; set; }

            public override string DocName => throw new Exception($"Internal error: no built-in command corresponding to {this}");
            public override string ToString() => $"{Prefix}ERROR({(Message == null ? "" : $"\"{Message}\"")})";
        }

        public enum ComparisonType
        {
            Equal, NotEqual, Greater, Less, GreaterOrEqual, LessOrEqual
        }
        public static Dictionary<ComparisonType, ComparisonType> OppositeComparison = new Dictionary<ComparisonType, ComparisonType>
        {
            [ComparisonType.Equal] = ComparisonType.NotEqual,
            [ComparisonType.NotEqual] = ComparisonType.Equal,
            [ComparisonType.Greater] = ComparisonType.LessOrEqual,
            [ComparisonType.LessOrEqual] = ComparisonType.Greater,
            [ComparisonType.Less] = ComparisonType.GreaterOrEqual,
            [ComparisonType.GreaterOrEqual] = ComparisonType.Less
        };
        public static Dictionary<ComparisonType, string> StrComparison = new Dictionary<ComparisonType, string>
        {
            [ComparisonType.Equal] = "==",
            [ComparisonType.NotEqual] = "!=",
            [ComparisonType.Greater] = ">",
            [ComparisonType.Less] = "<",
            [ComparisonType.LessOrEqual] = "<=",
            [ComparisonType.GreaterOrEqual] = ">="
        };

        // Misc utilities

        public class FancyNotSupportedException : Exception
        {
            public Intermediate Im { get; set; }

            public FancyNotSupportedException(string message, Intermediate im = null) : base(message)
            {
                Im = im;
            }
        }

        public class LineTrackingWriter : TextWriter
        {
            public TextWriter Writer { get; set; }
            private int Line = 0;
            public List<LineMapping> Mappings = new List<LineMapping>();

            public void RecordMapping(LineMapping mapping)
            {
                if (mapping == null) return;
                // 1-indexed
                mapping.PrintedLine = Line + 1;
                Mappings.Add(mapping);
            }

            public void PostMapping(LineMapping mapping)
            {
                if (mapping == null) return;
                // +1 for line indexing, but -1 for applying to the previous line.
                mapping.PrintedEndLine = Line;
            }

            public override void Write(char value)
            {
                if (value == '\n')
                {
                    Line++;
                }
                Writer.Write(value);
            }

            // May need to add more methods to support whatever this is used for.
            public override Encoding Encoding => Writer.Encoding;
            public override string ToString() => Writer.ToString();
        }

        // For pretty printing recursive structures
        public class StringTree
        {
            public string Start = "";
            public List<StringTree> Children { get; set; }
            public string Sep = "";
            public string End = "";
            private int _Length;

            private int Length
            {
                get
                {
                    if (_Length == 0)
                    {
                        _Length = (Children != null ? Children.Sum(c => c.Length) + Sep.Length * (Children.Count - 1) : 0) + Start.Length + End.Length;
                    }
                    return _Length;
                }
            }

            // A simple unbreakable string
            public static StringTree Of(object s) => new StringTree { Start = s.ToString() };
            // A start which can appear on its own line when the rest of it is too long
            public static StringTree IsolatedStart(string start, StringTree mid, string end) => new StringTree
            {
                Children = new List<StringTree> { Of(start), mid },
                End = end
            };
            // A start where the rest of it can be broken up, but it doesn't appear on its own line.
            public static StringTree CombinedStart(string start, StringTree mid, string end) => new StringTree
            {
                Start = start,
                Children = new List<StringTree> { mid },
                End = end
            };

            private string RenderOneLine() => Start + (Children == null ? "" : string.Join(Sep, Children.Select(c => c.RenderOneLine()))) + End;

            public string Render(string sp)
            {
                // TODO: Consider passing in a StringBuilder to use recursively for a rather minor efficiency gain.
                // Most printed lines won't use StringTree in any case.
                if (Children == null || Children.Count == 0 || sp.Length + Length <= columnLimit)
                {
                    return RenderOneLine();
                }
                string sp2 = sp + SingleIndent;
                if (Children.Count == 1)
                {
                    return Start + Children[0].Render(sp) + End;
                }
                StringBuilder ret = new StringBuilder();
                ret.AppendLine(Start + Children[0].Render(sp));
                for (int i = 1; i < Children.Count - 1; i++)
                {
                    ret.AppendLine(sp2 + Sep.TrimStart() + Children[i].Render(sp2));
                }
                ret.Append(sp2 + Sep.TrimStart() + Children[Children.Count - 1].Render(sp2) + End);
                return ret.ToString();
            }
        }
    }
}
