using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Esprima;
using Esprima.Ast;
using Esprima.Utils;
using static DarkScript3.ScriptAst;
using static DarkScript3.InstructionTranslator;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    public class FancyJSCompiler
    {
        private readonly string fileName;
        private readonly string code;
        private readonly EventCFG.CFGOptions options;
        // Filled in as necessary
        private Esprima.Ast.Program program;

        public FancyJSCompiler(string fileName, string code, EventCFG.CFGOptions options = null)
        {
            this.fileName = fileName;
            this.code = code;
            this.options = options;
        }

        public class SourceContext
        {
            public string Code { get; set; }
            private List<(int, string)> Lines { get; set; }
            private List<SourceDecoration> PendingDecorations { get; set; }
            private int DecorationIndex {get; set;}

            private int GetOffset(Position pos)
            {
                return Lines[pos.Line - 1].Item1 + pos.Column;
            }

            public int LineCount => Lines.Count;

            public string GetLine(int index)
            {
                // 1-indexed, also bounds-checked because this number can come from JS
                index--;
                if (index < 0 || index >= Lines.Count) return null;
                return Lines[index].Item2;
            }

            private bool HasDecorations()
            {
                return DecorationIndex < PendingDecorations.Count;
            }

            private bool TryPeekDecoration(out SourceDecoration dec)
            {
                if (DecorationIndex < PendingDecorations.Count)
                {
                    dec = PendingDecorations[DecorationIndex];
                    return true;
                }
                else
                {
                    dec = null;
                    return false;
                }
            }

            private void PopDecoration()
            {
                if (DecorationIndex < PendingDecorations.Count)
                {
                    DecorationIndex++;
                }
            }

            public IEnumerable<SourceDecoration> MostRecentDecorations()
            {
                for (int i = DecorationIndex - 1; i >= 0; i--)
                {
                    yield return PendingDecorations[i];
                }
            }

            public void SkipTo(Node node)
            {
                if (!HasDecorations()) return;
                int pos = GetOffset(node.Location.Start);
                while (TryPeekDecoration(out SourceDecoration dec) && dec.Position < pos)
                {
                    PopDecoration();
                }
            }

            public List<SourceDecoration> GetDecorationsForNode(Node node, bool forCond = false)
            {
                if (PendingDecorations.Count == 0) return null;
                int endLine = node.Location.End.Line;
                int lineStart = Lines[endLine - 1].Item1;
                int nextLineStart = endLine < Lines.Count ? Lines[endLine].Item1 : Code.Length;
                List<SourceDecoration> ret = null;
                while (TryPeekDecoration(out SourceDecoration dec) && dec.Position < nextLineStart)
                {
                    PopDecoration();
                    if (dec.Position < lineStart && dec.Comment != null)
                    {
                        dec.Type = SourceDecoration.DecorationType.PRE_COMMENT;
                    }
                    dec.ForCond = forCond;
                    ret ??= new List<SourceDecoration>();
                    ret.Add(dec);
                }
                return ret;
            }

            public List<SourceDecoration> GetDecorationsBeforeNode(Node node)
            {
                if (PendingDecorations.Count == 0) return null;
                int startLine = node.Location.Start.Line;
                int lineStart = Lines[startLine - 1].Item1;
                List<SourceDecoration> ret = null;
                while (TryPeekDecoration(out SourceDecoration dec) && dec.Position < lineStart)
                {
                    PopDecoration();
                    if (dec.Comment != null)
                    {
                        dec.Type = SourceDecoration.DecorationType.PRE_COMMENT;
                    }
                    ret ??= new List<SourceDecoration>();
                    ret.Add(dec);
                }
                return ret;
            }

            public string GetMostRecentDocComment(Node annotatedNode)
            {
                int commentLine = annotatedNode.Location.Start.Line - 1;
                string doc = null;
                foreach (SourceDecoration dec in MostRecentDecorations())
                {
                    if (dec.Line != commentLine || dec.Comment == null)
                    {
                        break;
                    }
                    doc = dec.Comment;
                    if (doc.StartsWith("/*"))
                    {
                        break;
                    }
                    commentLine--;
                }
                if (doc != null)
                {
                    doc = SourceContext.Decomment(doc);
                }
                return doc;
            }

            public string GetTextBetweenNodes(Node last, Node next, out int startLine)
            {
                startLine = 0;
                if (last == null && next == null) return Code;
                int sa = 0;
                if (last != null)
                {
                    startLine = last.Location.End.Line;
                    sa = GetOffset(last.Location.End);
                    // Hack to skip a newline, since it's not included in original Location, but always added whatever replaces 'last'
                    if (sa + 1 < Code.Length && Code[sa] == '\r' && Code[sa + 1] == '\n')
                    {
                        sa += 2;
                        startLine++;
                    }
                    else if (sa < Code.Length && Code[sa] == '\n')
                    {
                        sa += 1;
                        startLine++;
                    }
                }
                int sb = next == null ? Code.Length : GetOffset(next.Location.Start);
                return Code.Substring(sa, sb - sa);
            }

            public SourceNode GetSourceNode(Node node)
            {
                int sa = GetOffset(node.Location.Start);
                int sb = GetOffset(node.Location.End);
                string source = Code.Substring(sa, sb - sa);
                return new SourceNode { Node = node, Source = source };
            }

            // 0-indexed first non-whitespace character
            public static int IndexOfNonWhiteSpace(string line)
            {
                for (int i = 0; i < line.Length; i++)
                {
                    if (!char.IsWhiteSpace(line[i]))
                    {
                        return i;
                    }
                }
                return 0;
            }

            public void AnnotateErrors(List<CompileError> errors, string header)
            {
                foreach (CompileError err in errors)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"{header}{(err.Event == null ? "" : $" in event {err.Event}")}: {err.Message}");
                    // Sometimes the given position is out-of-bounds, TODO investigate
                    if (err.Loc is Position sourceLoc && Lines.Count > 0 && sourceLoc.Line - 1 < Lines.Count)
                    {
                        (int _, string line) = Lines[sourceLoc.Line - 1];
                        int col = sourceLoc.Column;
                        // In cases we don't have a precise column, use the actual start of the line at minimum
                        if (col == 0)
                        {
                            col = IndexOfNonWhiteSpace(line);
                        }
                        // In the editor, columns are 1-indexed.
                        string prefix = $"{sourceLoc.Line}:{col+1}:";
                        sb.AppendLine($"{prefix}{line}");
                        sb.AppendLine($"{new string(' ', prefix.Length + col)}^");
                    }
                    else
                    {
                        sb.AppendLine();
                    }
                    err.Loc = null;
                    err.Message = sb.ToString();
                }
            }

            private static readonly Regex commentLineRegex = new Regex(@"^/\*+\s*|\s*\*+/$|^\s*\*\s*");
            public static string Decomment(string comment)
            {
                if (comment.StartsWith("//"))
                {
                    return comment.Substring(2).Trim();
                }
                else if (comment.StartsWith("/*"))
                {
                    // Could alternatively use one mega-regex to handle this, but these comments shouldn't be that common
                    return string.Join("\n", comment.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
                        .Select(l => commentLineRegex.Replace(l, ""))
                        .Where(l => l.Length > 0));
                }
                else
                {
                    return comment;
                }
            }

            // Possible feature: provide a range? Or do it before calling pack
            public static SourceContext FromText(string code, bool decorating)
            {
                List<(int, string)> lines = new List<(int, string)>();
                PositionTrackingReader reader = new PositionTrackingReader { Reader = new StringReader(code) };
                List<SourceDecoration> decorations = new List<SourceDecoration>();
                string prevLine = null;
                while (true)
                {
                    int position = reader.Position;
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    if (decorating && string.IsNullOrWhiteSpace(line))
                    {
                        // Record all blank lines.
                        // Only blank lines within events are preserved. Blank lines between events will be skipped/filtered out later.
                        // TODO remove
                        // Console.WriteLine($"blank line at {position} after {prevLine}");
                        decorations.Add(new SourceDecoration
                        {
                            Type = SourceDecoration.DecorationType.PRE_BLANK,
                            Position = position,
                            Line = lines.Count + 1,
                        });
                    }
                    lines.Add((position, line));
                    prevLine = line;
                }
                if (decorating)
                {
                    // For every line: Pre (# of blank lines, arbitrary comments), post (post-comment)
                    Scanner commentScanner = new Scanner(code, new ScannerOptions { Comments = true });
                    Token token;
                    do
                    {
                        foreach (Comment comment in commentScanner.ScanComments())
                        {
                            string text = code.Substring(comment.Start, comment.End - comment.Start);
                            bool endline = comment.End >= code.Length || code[comment.End] == '\r' || code[comment.End] == '\n';
                            decorations.Add(new SourceDecoration
                            {
                                // Post-comments to empty lines will be turned into pre-comments for the next line, as soon as they are attached to one
                                Type = endline ? SourceDecoration.DecorationType.POST_COMMENT : SourceDecoration.DecorationType.PRE_COMMENT,
                                Position = comment.Start,
                                Line = comment.EndPosition.Line,
                                Comment = text
                            });
                        }
                        token = commentScanner.Lex();
                    } while (token.Type != TokenType.EOF);
                }

                return new SourceContext
                {
                    Code = code,
                    Lines = lines,
                    PendingDecorations = decorations.OrderBy(d => d.Position).ToList(),
                };

            }
        }

        public class SourceNode
        {
            public Node Node { get; set; }
            public string Source { get; set; }

            public bool GetName(out string name)
            {
                if (Node is Identifier id)
                {
                    name = id.Name;
                    return true;
                }
                else
                {
                    name = null;
                    return false;
                }
            }

            private static bool GetNumExpr(Node expr, out double val)
            {
                bool negate = false;
                if (expr is UnaryExpression ue && ue.Operator == UnaryOperator.Minus)
                {
                    expr = ue.Argument;
                    negate = true;
                }
                if (expr is Literal lit && lit.NumericValue is double d)
                {
                    val = negate ? -(int)d : (int)d;
                    return true;
                }
                val = 0;
                return false;
            }

            public bool GetIntValue(out int val)
            {
                if (GetNumExpr(Node, out double dval))
                {
                    val = (int)dval;
                    return true;
                }
                else if (Node is CallExpression lcall && lcall.Callee is Identifier lid && lid.Name == "floatArg"
                    && lcall.Arguments.Count == 1 && GetNumExpr(lcall.Arguments[0], out dval))
                {
                    val = BitConverter.ToInt32(BitConverter.GetBytes((float)dval), 0);
                    return true;
                }
                val = 0;
                return false;
            }

            public override string ToString() => Source;
        }

        public class WalkContext
        {
            // Global properties
            public bool Debug { get; set; }
            public List<CompileError> Errors = new List<CompileError>();
            public List<CompileError> Warnings = new List<CompileError>();

            public void Error(Node node, string text = null)
            {
                if (Debug) Console.WriteLine($"ERROR: {text}");
                Errors.Add(CompileError.FromNode(node, text, Event));
            }

            public void Error(string text)
            {
                Error(null, text);
            }

            // Local properties for error reporting.
            // The preferred way to change this is with Copy, but can also be mutated in a single-thread context.
            public object Event { get; set; }
            // Arg info, collected directly for regular compilation and after CFG processing for fancy compilation
            // Can be ignored if there are errors
            public ICollection<string> Params { get; set; }

            public bool IsParam(string name)
            {
                return Params != null && Params.Contains(name);
            }

            // Clone
            // Unused for now, would need merging logic as well.
            internal WalkContext Copy(object ev = null)
            {
                WalkContext other = (WalkContext)MemberwiseClone();
                if (ev != null)
                {
                    other.Event = ev;
                }
                return other;
            }
        }

        private static string Plural(int amount, string s = null, string alt = null)
        {
            s = s ?? "argument";
            return amount == 1 ? $"{amount} {s}" : (alt == null ? $"{amount} {s}s" : $"{amount} {alt}");
        }

        private class ArgChecker : AstVisitor
        {
            public WalkContext Context { get; init; }

            protected override object VisitIdentifier(Identifier identifier)
            {
                if (Context.IsParam(identifier.Name))
                {
                    Context.Error(identifier, "Cannot use event parameter outside of commands or conditions");
                }
                return identifier;
            }
        }

        private class RegularArgVisitor : AstVisitor
        {
            public SourceContext Source { get; init; }
            public WalkContext Context { get; init; }
            public InstructionDocs Docs { get; init; }
            public List<Intermediate> Instrs { get; } = new();

            protected override object VisitIdentifier(Identifier identifier)
            {
                if (Context.IsParam(identifier.Name))
                {
                    Context.Error(identifier, "Cannot use event parameter outside of direct instruction usage");
                }
                return identifier;
            }

            protected override object VisitCallExpression(CallExpression call)
            {
                // Simpler variant of instruction parsing which should be compatible with plain JS execution
                string f = null;
                if (call.Callee is Identifier id)
                {
                    f = id.Name;
                    if (Docs.DisplayAliases.TryGetValue(f, out string realName))
                    {
                        f = realName;
                    }
                }
                if (f != null && Docs.Functions.TryGetValue(f, out (int, int) pos))
                {
                    EMEDF.InstrDoc instrDoc = Docs.DOC[pos.Item1][pos.Item2];
                    string cmd = InstructionDocs.FormatInstructionID(pos.Item1, pos.Item2);
                    // The actual docs are not needed here. EventInit will error out if there's a parameter in an invalid position.
                    // IList<EMEDF.ArgDoc> argDocs = instrDoc.Arguments;
                    // bool variableLength = Docs.IsVariableLength(instrDoc);
                    object layers = null;
                    List<Expression> args = call.Arguments.ToList();
                    if (args.Count > 0 && args[args.Count - 1] is CallExpression lcall && lcall.Callee is Identifier lid && lid.Name == "$LAYERS")
                    {
                        layers = Source.GetSourceNode(lcall);
                        args.RemoveAt(args.Count - 1);
                    }
                    // Variable length instructions are still added here so that they can be rejected later if they have args.
                    // And also allow identifiers through
                    List<object> sourceArgs = new(args.Count);
                    foreach (Expression arg in args)
                    {
                        SourceNode node = Source.GetSourceNode(arg);
                        if (arg is not Identifier)
                        {
                            Visit(arg);
                        }
                        // No need to clean up enums, it's only needed for fancy processing
                        sourceArgs.Add(node);
                    }
                    Intermediate im = new Instr
                    {
                        Cmd = cmd,
                        Name = f,
                        Args = sourceArgs,
                        Layers = layers
                    };
                    Instrs.Add(im);
                    return call;
                }
                return base.VisitCallExpression(call);
            }
        }

        public class EventParser
        {
            public SourceContext source { get; private init; }
            public WalkContext context { get; private init; }
            public InstructionDocs docs { get; private init; }
            private ArgChecker argChecker { get; init; }
            private List<SourceDecoration> preDecs { get; set; } = new();

            public EventParser(SourceContext source, WalkContext context, InstructionDocs docs)
            {
                this.source = source;
                this.context = context;
                this.docs = docs;
                argChecker = new ArgChecker { Context = context };
            }

            // State variables mean that this is not safe to use in parallel, but multiple instances can be created and used in parallel
            // (as long as error reporting is aggregated at the end)
            List<string> extraLabels = new List<string>();
            int cmdId = 0;

            private int ConvertIntExpression(Expression expr)
            {
                bool negate = false;
                if (expr is UnaryExpression unary && unary.Operator == UnaryOperator.Minus)
                {
                    expr = unary.Argument;
                    negate = true;
                }
                if (expr is Literal lit)
                {
                    if (lit.TokenType == TokenType.NumericLiteral)
                    {
                        int ret = (int)lit.NumericValue;
                        if (negate) ret = -ret;
                        return ret;
                    }
                    else
                    {
                        context.Error(expr, $"Expected int but found {lit.TokenType}");
                    }
                }
                else
                {
                    context.Error(expr, $"Expected int but found {expr.Type}");
                }
                return 0;
            }

            private SourceNode PassThroughSource(Node node)
            {
                argChecker.Visit(node);
                return source.GetSourceNode(node);
            }

            public SourceNode PassThroughArg(Node node)
            {
                // Args can be params directly, but params cannot be part of subexpressions
                if (node is not Identifier)
                {
                    argChecker.Visit(node);
                }
                return source.GetSourceNode(node);
            }

            private bool CheckExpectedArgs(CallExpression call, IReadOnlyList<Expression> args, string f, int docArgs, int optionalArgs)
            {
                if (optionalArgs == 0)
                {
                    if (args.Count != docArgs)
                    {
                        context.Error(call, $"Expected {Plural(docArgs)} for {f} but {args.Count} given");
                        return false;
                    }
                }
                else
                {
                    int min = docArgs - optionalArgs;
                    if (args.Count < min || args.Count > docArgs)
                    {
                        context.Error(call, $"Expected {min} to {Plural(docArgs)} for {f} but {args.Count} given");
                        return false;
                    }
                }
                return true;
            }

            // Returns null on error, which caller should transform to an appropriate error object.
            private CmdCond ConvertCommandCond(CallExpression call)
            {
                if (!(call.Callee is Identifier id))
                {
                    context.Error(call, "Expected a function call with a regular function name");
                    return null;
                }
                string f = id.Name;
                if (docs.Translator.DisplayAliases.TryGetValue(f, out string realName))
                {
                    f = realName;
                }
                if (!docs.Translator.CondDocs.TryGetValue(f, out FunctionDoc doc) || doc.ConditionDoc.Hidden)
                {
                    context.Error(call, $"Unknown condition function {f}");
                    return null;
                }
                if (!CheckExpectedArgs(call, call.Arguments, f, doc.Args.Count, doc.OptionalArgs))
                {
                    return null;
                }
                return new CmdCond
                {
                    Name = f,
                    Args = call.Arguments.Select(a => (object)PassThroughArg(a)).ToList()
                };
            }

            private static readonly Dictionary<BinaryOperator, ComparisonType> compares = new Dictionary<BinaryOperator, ComparisonType>
            {
                [BinaryOperator.Equal] = ComparisonType.Equal,
                [BinaryOperator.NotEqual] = ComparisonType.NotEqual,
                [BinaryOperator.Greater] = ComparisonType.Greater,
                [BinaryOperator.Less] = ComparisonType.Less,
                [BinaryOperator.GreaterOrEqual] = ComparisonType.GreaterOrEqual,
                [BinaryOperator.LessOrEqual] = ComparisonType.LessOrEqual,
            };

            private void ValidateConditionVariable(Identifier id)
            {
                if (ReservedWords.ContainsKey(id.Name))
                {
                    context.Error(id, $"Using reserved word {id.Name} as a condition variable");
                }
                else if (docs.Enums.ContainsKey(id.Name))
                {
                    context.Error(id, $"Using enum name {id.Name} as a condition variable");
                }
                else if (docs.Functions.ContainsKey(id.Name) || docs.DisplayAliases.ContainsKey(id.Name))
                {
                    context.Error(id, $"Using function name {id.Name} as a condition variable");
                }
                else if (id.Name.StartsWith("X") || (context.IsParam(id.Name)))
                {
                    // X convention seems fine to use for now, since it's excluded from general argument tracking
                    context.Error(id, $"Condition variable {id.Name} cannot be a parameter");
                }
            }

            private Cond ConvertCondExpression(Expression expr)
            {
                Cond c;
                if (expr is UnaryExpression unary)
                {
                    c = ConvertCondExpression(unary.Argument);
                    if (unary.Operator == UnaryOperator.LogicalNot)
                    {
                        c.Negate = !c.Negate;
                    }
                    else
                    {
                        context.Error(expr, $"Operator {unary.Operator} used when only ! is supported for a condition");
                    }
                }
                else if (expr is BinaryExpression bin)
                {
                    if (bin.Operator == BinaryOperator.LogicalAnd || bin.Operator == BinaryOperator.LogicalOr)
                    {
                        Cond lhs = ConvertCondExpression(bin.Left);
                        Cond rhs = ConvertCondExpression(bin.Right);
                        // This is later rewritten from a binary tree into a list according to operator associativity.
                        return new OpCond { And = bin.Operator == BinaryOperator.LogicalAnd, Ops = new List<Cond> { lhs, rhs } };
                    }
                    else
                    {
                        if (!compares.TryGetValue(bin.Operator, out ComparisonType comp))
                        {
                            context.Error(expr, $"Operator {bin.Operator} is not permitted, only || and && for conditions, or == != > < >= <= for comparisons");
                        }
                        // RHS should be a number, but this source is copied over so it can be a variable etc.
                        CompareCond cmp = new CompareCond { Type = comp, Rhs = PassThroughArg(bin.Right) };
                        if (bin.Left is CallExpression call)
                        {
                            cmp.CmdLhs = ConvertCommandCond(call) ?? new CmdCond { Name = "Error" };
                        }
                        else
                        {
                            cmp.Lhs = PassThroughArg(bin.Left);
                        }
                        c = cmp;
                    }
                }
                else if (expr is CallExpression call)
                {
                    c = (Cond)ConvertCommandCond(call) ?? new ErrorCond();
                }
                else if (expr is MemberExpression mem)
                {
                    if (mem is StaticMemberExpression stat && stat.Object is Identifier objId
                        && stat.Property is Identifier propId && propId.Name == "Passed")
                    {
                        ValidateConditionVariable(objId);
                        c = new CondRef { Name = objId.Name, Compiled = true };
                    }
                    else
                    {
                        context.Error(expr, $"Can't use member or array accesses in conditions other than compiled condition syntax");
                        return new ErrorCond();
                    }
                }
                else if (expr is Identifier id)
                {
                    ValidateConditionVariable(id);
                    c = new CondRef { Name = id.Name };
                }
                else
                {
                    context.Error(expr, $"Unexpected condition {expr.Type}. Should be a function call, condition variable, comparison, or combination of these");
                    return new ErrorCond();
                }
                List<SourceDecoration> decs = source.GetDecorationsForNode(expr, true);
                if (decs != null)
                {
                    c.Decorations = decs;
                    // Console.WriteLine($"### {c} -> {string.Join("; ", decs)}");
                }
                return c;
            }

            public CondAssign ConvertAssign(Node lhs, AssignmentOperator assignOp, Expression rhs)
            {
                CondAssign assign = new CondAssign();
                if (lhs is Identifier id)
                {
                    ValidateConditionVariable(id);
                    Dictionary<AssignmentOperator, CondAssignOp> assigns = new Dictionary<AssignmentOperator, CondAssignOp>
                    {
                        [AssignmentOperator.Assign] = CondAssignOp.Assign,
                        [AssignmentOperator.BitwiseAndAssign] = CondAssignOp.AssignAnd,
                        [AssignmentOperator.BitwiseOrAssign] = CondAssignOp.AssignOr,
                    };
                    if (!assigns.TryGetValue(assignOp, out CondAssignOp comp))
                    {
                        context.Error(lhs, $"Operator {assignOp} used in condition assignment when only = &= or |= are permitted");
                    }
                    assign.Op = comp;
                    assign.ToVar = id.Name;
                }
                // Don't use cond[1] cond[-1] etc syntax at present. The and01 or08 etc convention
                // works just fine and can coexist with other named condition variables.
                else if (false && lhs is MemberExpression mem)
                {
                    // This should ideally check valid ranges per game if reimplemented.
                    if (mem.Object is Identifier condId && condId.Name == "cond")
                    {
                        assign.ToCond = ConvertIntExpression(mem.Property);
                    }
                    else
                    {
                        context.Error(lhs, $"Can't assign conditions to arrays");
                    }
                }
                else
                {
                    context.Error(lhs, "Can't assign to anything other than a condition name, or a JS variable using const");
                }
                assign.Cond = ConvertCondExpression(rhs);
                return assign;
            }

            // This should be called in order of source, meaning that outer statements should be called before inner.
            private void ProcessIntermediate(Intermediate im, Node decorationNode)
            {
                if (extraLabels.Count > 0)
                {
                    im.Labels = extraLabels;
                    extraLabels = new List<string>();
                }
                im.ID = cmdId++;
                if (preDecs.Count > 0)
                {
                    im.Decorations = preDecs.ToList();
                    preDecs.Clear();
                }
                List<SourceDecoration> nodeDecs = source.GetDecorationsForNode(decorationNode);
                if (nodeDecs != null)
                {
                    if (im.Decorations == null)
                    {
                        im.Decorations = nodeDecs;
                    }
                    else
                    {
                        im.Decorations.AddRange(nodeDecs);
                    }
                }
                // if (im.Decorations != null && im.Decorations.Count > 0) Console.WriteLine($"Decorations for {im}: {string.Join(" // ", im.Decorations)}");
                // else Console.WriteLine($"No decorations for {im} ({pending} pending)");
                im.LineMapping = new LineMapping
                {
                    SourceLine = decorationNode.Location.Start.Line,
                    SourceEndLine = decorationNode.Location.End.Line
                };
                // Rewrite entire condition. Because of associativity, this can't be done while transforming the expressions directly.
                if (im is CondIntermediate condIm && condIm.Cond != null)
                {
                    condIm.Cond = condIm.Cond.RewriteCond(c =>
                    {
                        if (c is OpCond op)
                        {
                            List<Cond> conds = new List<Cond>();
                            void addConds(OpCond subop)
                            {
                                foreach (Cond arg in subop.Ops)
                                {
                                    if (arg is OpCond subsubop && subsubop.And == op.And && !subsubop.Negate)
                                    {
                                        addConds(subsubop);
                                    }
                                    else
                                    {
                                        conds.Add(arg);
                                    }
                                }
                            }
                            addConds(op);
                            op.Ops = conds;
                            return op;
                        }
                        return null;
                    });
                }
            }

            private List<Intermediate> ConvertStatement(Statement statement)
            {
                List<SourceDecoration> statementDecs = source.GetDecorationsBeforeNode(statement);
                if (statementDecs != null)
                {
                    preDecs.AddRange(statementDecs);
                }
                List<Intermediate> ret = new List<Intermediate>();
                // Peel off labels into either label commands or synthetic labels.
                while (statement is LabeledStatement labelStmt)
                {
                    string label = labelStmt.Label.Name;
                    // New addition for Elden Ring because of V8 error with label redeclaration
                    label = label.TrimEnd('_');
                    if (LabelIds.TryGetValue(label, out int labelNum))
                    {
                        Intermediate toAdd = new Label { Num = labelNum };
                        ProcessIntermediate(toAdd, labelStmt.Label);
                        ret.Add(toAdd);
                    }
                    else
                    {
                        extraLabels.Add(label);
                    }
                    statement = labelStmt.Body;
                }
                if (statement is BlockStatement block)
                {
                    foreach (Statement stmt in block.Body)
                    {
                        foreach (Intermediate toAdd in ConvertStatement(stmt))
                        {
                            if (toAdd != null)
                            {
                                if (toAdd.ID < 0) ProcessIntermediate(toAdd, stmt);
                                ret.Add(toAdd);
                            }
                        }
                    }
                }
                else if (statement is ExpressionStatement exprStmt)
                {
                    if (exprStmt.Expression is CallExpression call)
                    {
                        // Built-in commands and plain emevd instructions.
                        string f = null;
                        if (call.Callee is Identifier id)
                        {
                            f = id.Name;
                            if (docs.DisplayAliases.TryGetValue(f, out string realName))
                            {
                                f = realName;
                            }
                        }
                        else
                        {
                            // There shouldn't be anything complicated here, just simple named invocations
                            if (call.Callee.DescendantNodesAndSelf().Any(subExpr => subExpr is IFunction))
                            {
                                context.Error(call, $"Expected a function call with a simple named function, not a {call.Callee.Type}");
                            }
                        }
                        List<Expression> args = call.Arguments.ToList();
                        bool hasExpectedArgs(int expect, int opt = 0) => CheckExpectedArgs(call, args, f, expect, opt);
                        Intermediate im = null;
                        if (f == "EndEvent" || f == "RestartEvent")
                        {
                            if (hasExpectedArgs(0))
                            {
                                im = new End { Type = f == "EndEvent" ? 0 : 1, Cond = Cond.ALWAYS };
                            }
                        }
                        else if (f == "EndIf" || f == "RestartIf")
                        {
                            if (hasExpectedArgs(1))
                            {
                                im = new End { Type = f == "EndIf" ? 0 : 1, Cond = ConvertCondExpression(args[0]) };
                            }
                        }
                        else if (f == "Goto")
                        {
                            if (hasExpectedArgs(1))
                            {
                                if (args[0] is Identifier goId)
                                {
                                    im = new Goto { ToLabel = goId.Name, Cond = Cond.ALWAYS };
                                }
                                else
                                {
                                    context.Error(args[0], $"Expected a label name like L0, received {args[0].Type}");
                                }
                            }
                        }
                        else if (f == "GotoIf")
                        {
                            if (hasExpectedArgs(2))
                            {
                                if (args[0] is Identifier goId)
                                {
                                    im = new Goto { ToLabel = goId.Name, Cond = ConvertCondExpression(args[1]) };
                                }
                                else
                                {
                                    context.Error(args[0], $"Expected a label name like L0, received {args[0].Type}");
                                }
                            }
                        }
                        else if (f == "NoOp")
                        {
                            if (hasExpectedArgs(0))
                            {
                                im = new NoOp();
                            }
                        }
                        else if (f == "WaitFor")
                        {
                            if (hasExpectedArgs(1))
                            {
                                im = new Wait { Cond = ConvertCondExpression(args[0]) };
                            }
                        }
                        else if (f != null && (docs.Functions.ContainsKey(f) || docs.Translator.ShortDocs.ContainsKey(f)))
                        {
                            bool variableLength = false;
                            IList<EMEDF.ArgDoc> argDocs;
                            int optionalArgs;
                            string cmd;
                            // Combine processing these two for the moment. This can be made less janky in the future,
                            // like by using InstrDoc more widely for various instruction-like things.
                            if (docs.Functions.TryGetValue(f, out (int, int) pos))
                            {
                                EMEDF.InstrDoc instrDoc = docs.DOC[pos.Item1][pos.Item2];
                                argDocs = instrDoc.Arguments;
                                optionalArgs = instrDoc.OptionalArgs;
                                cmd = InstructionDocs.FormatInstructionID(pos.Item1, pos.Item2);
                                variableLength = docs.IsVariableLength(instrDoc);
                            }
                            else
                            {
                                ShortVariant variant = docs.Translator.ShortDocs[f];
                                argDocs = variant.Args;
                                optionalArgs = variant.OptionalArgs;
                                cmd = variant.Cmd;
                            }
                            object layers = null;
                            if (args.Count > 0 && args[args.Count - 1] is CallExpression lcall && lcall.Callee is Identifier lid && lid.Name == "$LAYERS")
                            {
                                layers = PassThroughSource(lcall);
                                args.RemoveAt(args.Count - 1);
                            }
                            if (variableLength || hasExpectedArgs(argDocs.Count, optionalArgs))
                            {
                                // Getting the int value is required when compiling things with control/negate arguments etc.
                                object getSourceArg(Expression arg)
                                {
                                    SourceNode node = PassThroughArg(arg);
                                    if (docs.EnumValues.TryGetValue(node.Source, out int val))
                                    {
                                        return new DisplayArg { DisplayValue = node.Source, Value = val };
                                    }
                                    return node;
                                }
                                // We do pretty minimal checking of arguments; further validation is saved for JS execution time.
                                im = new Instr
                                {
                                    Cmd = cmd,
                                    Name = f,
                                    Args = args.Select(getSourceArg).ToList(),
                                    Layers = layers
                                };
                            }
                        }
                        else if (f != null && char.IsLower(f[0]))
                        {
                            // Allow function calls through unmodified if they are lowercase
                            im = new JSStatement { Code = PassThroughSource(statement).ToString() };
                        }
                        else
                        {
                            if (f != null)
                            {
                                context.Error(call, $"Unknown function name {f}. Use lowercase names to call regular JS functions.");
                            }
                            im = null;
                        }
                        if (im != null)
                        {
                            ProcessIntermediate(im, statement);
                        }
                        ret.Add(im);
                    }
                    else if (exprStmt.Expression is AssignmentExpression assign)
                    {
                        Intermediate im = ConvertAssign(assign.Left, assign.Operator, assign.Right);
                        ProcessIntermediate(im, statement);
                        ret.Add(im);
                    }
                    else
                    {
                        context.Error(exprStmt, $"{exprStmt.Type} not supported here");
                    }
                }
                else if (statement is VariableDeclaration decls)
                {
                    if (decls.Kind == VariableDeclarationKind.Const)
                    {
                        // Allow const variable declarations through unmodified
                        ret.Add(new JSStatement
                        {
                            Code = PassThroughSource(statement).ToString(),
                            Declared = decls.Declarations.SelectMany(d => d.Id is Identifier id ? new[] { id.Name } : Array.Empty<string>()).ToList(),
                        });
                    }
                    else
                    {
                        context.Error(decls, "Variable declarations with var/let are not supported."
                            + " Use const declarations like \"const bossEntity = 1400100;\" to declare constants"
                            + " and plain \"myCond = EventFlag(3);\" to assign to condition groups.");
                    }
                }
                else if (statement is IfStatement ifs)
                {
                    IfElse ifelse = new IfElse
                    {
                        Cond = ConvertCondExpression(ifs.Test),
                    };
                    // The decoration node is the test expression, not the whole thing
                    ProcessIntermediate(ifelse, ifs.Test);
                    ifelse.True = ConvertStatement(ifs.Consequent);
                    if (ifs.Alternate != null)
                    {
                        ifelse.False = ConvertStatement(ifs.Alternate);
                    }
                    ret.Add(ifelse);
                }
                else if (statement is ForStatement fors)
                {
                    if (fors.Init != null && !(fors.Init is VariableDeclaration dec && dec.Kind == VariableDeclarationKind.Let))
                    {
                        context.Error(fors.Init, $"For statement initialization should be of the form: let <variable> = <value>");
                    }
                    Node[] parts = new[] { fors.Init, fors.Test, fors.Update };
                    string inner = string.Join("; ", parts.Select(p => p == null ? null : PassThroughSource(p).ToString().TrimEnd(';')));
                    LoopStatement loop = new LoopStatement
                    {
                        Code = $"for ({inner})",
                    };
                    Node first = parts.Where(p => p != null).FirstOrDefault();
                    if (first != null)
                    {
                        ProcessIntermediate(loop, first);
                    }
                    loop.Body = ConvertStatement(fors.Body);
                    ret.Add(loop);
                }
                else if (statement is ForOfStatement forof)
                {
                    if (forof.Await)
                    {
                        context.Error(forof, $"Await-based for-of is not supported");
                    }
                    if (!(forof.Left is VariableDeclaration dec && dec.Kind == VariableDeclarationKind.Const))
                    {
                        context.Error(forof.Left, $"For-of statements should use const to assign loop variables: for (const <id> of <ids>)");
                    }
                    LoopStatement loop = new LoopStatement
                    {
                        Code = $"for ({PassThroughSource(forof.Left)} of {PassThroughSource(forof.Right)})",
                    };
                    ProcessIntermediate(loop, forof.Left);
                    loop.Body = ConvertStatement(forof.Body);
                    ret.Add(loop);
                }
                else
                {
                    // May need to explain in greater detail per unsupported type or what alternatives there are or might be.
                    context.Error(statement, $"{statement.Type} not supported here");
                }
                return ret;
            }

            private void ProcessEvent(EventFunction eventFunc, FunctionExpression func, bool repack)
            {
                // func.Id is currently meaningless, like making an anonymous function with function somename() {}
                // Otherwise, should have plain params, block statement body, and no attributes like Generator/Expression/Async/Strict.

                // Allow previous mode of using X0_4, but don't allow them to mix
                bool partialParse = !eventFunc.Fancy && !repack;
                if (eventFunc.Params.Count > 0)
                {
                    List<string> args = eventFunc.Params;
                    int xCount = args.Count(a => a.StartsWith('X'));
                    if (xCount == 0 || repack)
                    {
                        HashSet<string> distinctParams = new(args);
                        if (distinctParams.Count == args.Count)
                        {
                            context.Params = args;
                        }
                        else
                        {
                            IEnumerable<string> dupeArgs = args.GroupBy(a => a).Where(g => g.Count() > 1).Select(g => g.Key);
                            context.Error(func, $"Duplicate names not allowed for named arguments, but found {string.Join(", ", dupeArgs)})");
                        }
                    }
                    else if (xCount != args.Count)
                    {
                        context.Error(func, "Arguments should either all start with X, indicating position/width, or none start with X");
                    }
                }
                if (partialParse)
                {
                    if (context.Params == null)
                    {
                        return;
                    }
                    RegularArgVisitor visitor = new RegularArgVisitor { Source = source, Context = context, Docs = docs };
                    visitor.Visit(func.Body);
                    eventFunc.Body = visitor.Instrs;
                    return;
                }
                // Reset state and parse
                extraLabels = new List<string>();
                cmdId = 0;
                eventFunc.Body = ConvertStatement(func.Body);
                eventFunc.EndComments = source.GetDecorationsForNode(func);
                eventFunc.LineMapping = new LineMapping { SourceLine = func.Location.Start.Line };
                if (extraLabels.Count > 0)
                {
                    context.Error(func, $"Extra labels {string.Join(",", extraLabels)} at the end not assigned to any instruction");
                }
                // strict is now implied by ParseModule
                if (func.Generator || func.Async)
                {
                    context.Error(func, "Event function shouldn't have extra annotations, but found generator/async");
                }
            }

            public EventFunction ParseEventCall(CallExpression call, bool repack)
            {
                if (call.Callee is Identifier funcName
                    && call.Arguments.Count == 3
                    && call.Arguments[1] is Identifier rest
                    && behaviorTypes.TryGetValue(rest.Name, out Event.RestBehaviorType restBehavior)
                    && call.Arguments[2] is FunctionExpression funcExpr)
                {
                    object eventId = source.GetSourceNode(call.Arguments[0]);
                    if (long.TryParse(eventId.ToString(), out long actualId))
                    {
                        eventId = InstructionDocs.FixEventID(actualId);
                    }
                    context.Event = eventId;
                    context.Params = null;
                    int errCount = 1;
                    List<string> args = funcExpr.Params.Select(param =>
                    {
                        if (param is Identifier pid)
                        {
                            return pid.Name;
                        }
                        context.Error(param, "Param not a plain identifier");
                        return $"#error_{errCount++}#";
                    }).ToList();
                    EventFunction eventFunc = new EventFunction
                    {
                        ID = eventId,
                        RestBehavior = restBehavior,
                        Params = args,
                        Fancy = funcName.Name == "$Event",
                    };
                    ProcessEvent(eventFunc, funcExpr, repack);
                    return eventFunc;
                }
                else
                {
                    context.Error(call, "Expected event call with three arguments: an integer id, a rest behavior, and a function expression");
                    return null;
                }
            }

            public static CallExpression GetEventCall(Statement stmt)
            {
                if (stmt is ExpressionStatement exprStmt
                    && exprStmt.Expression is CallExpression call
                    && call.Callee is Identifier id && (id.Name == "$Event" || id.Name == "Event"))
                {
                    return call;
                }
                return null;
            }

            private static readonly Dictionary<string, Event.RestBehaviorType> behaviorTypes =
                ((Event.RestBehaviorType[])Enum.GetValues(typeof(Event.RestBehaviorType))).ToDictionary(t => t.ToString(), t => t);
        }

        // May be reused for various operations
        private void Parse()
        {
            if (program != null)
            {
                return;
            }
            WalkContext context = new WalkContext();
            try
            {
                JavaScriptParser parser = new JavaScriptParser(new ParserOptions { Tokens = true, Comments = true });
                program = parser.ParseModule(code);
            }
            catch (ParserException ex)
            {
                Console.WriteLine($"Ah {ex}");
                if (ex.Error is ParseError err)
                {
                    Position? pos = null;
                    if (err.IsPositionDefined)
                    {
                        // These columns appear to be mostly 1-indexed, so change them to 0-indexed to match Node positions.
                        pos = Position.From(err.Position.Line, Math.Max(0, err.Position.Column - 1));
                    }
                    context.Errors.Add(new CompileError { Line = err.LineNumber, Loc = pos, Message = err.Description });
                    SourceContext tempSource = SourceContext.FromText(code, decorating: false);
                    tempSource.AnnotateErrors(context.Errors, "ERROR");
                }
                else
                {
                    context.Errors.Add(new CompileError { Message = "ERROR: " + ex.ToString() });
                }
                throw new FancyCompilerException { Errors = context.Errors };
            }
        }

        public enum Mode
        {
            // Do nothing but rewrite using EventFunction, to test decoration tracking
            Reparse,
            // Output for EventScripter
            Pack,
            // Output for EventScripter for preview
            PackPreview,
            // Compile then decompile for regular-to-fancy conversion
            Repack,
            // No text output, just return FileInit. For linked files and regular compilation
            ParseOnly,
        }

        public CompileOutput Compile(InstructionDocs docs, Mode mode, InitData.Links repackLinks = null, Dictionary<long, string> repackNames = null)
        {
            Parse();

            bool outputFull = mode == Mode.Repack || mode == Mode.Reparse;
            bool writeAny = mode != Mode.ParseOnly;

            WalkContext context = new WalkContext();
            SourceContext source = SourceContext.FromText(code, decorating: mode != Mode.PackPreview);
            EventParser eventParser = new EventParser(source, context, docs);
            InitData.FileInit init = new(InitData.GetEmevdName(fileName), true);

            Node lastRewrittenNode = null;
            StringWriter stringOutput = new StringWriter();
            LineTrackingWriter writer = writeAny ? new LineTrackingWriter { Writer = stringOutput } : null;
            int blockSourceLine = 0;
            List<EventFunction> functionsToProcess = mode == Mode.Repack ? new() : null;
            foreach (Statement stmt in program.Body)
            {
                CallExpression call = EventParser.GetEventCall(stmt);
                if (call == null)
                {
                    continue;
                }
                // This may include entire events if they were not rewritten
                string header = source.GetTextBetweenNodes(lastRewrittenNode, stmt, out blockSourceLine);
                if (outputFull)
                {
                    header = header.Replace("\t", SingleIndent);
                }
                // Any decorations in between should be already incorporated in header
                source.SkipTo(call);
                // Even when writeAny is false, we'd still like doc
                string doc = source.GetMostRecentDocComment(call);

                int errorCount = context.Errors.Count;
                EventFunction func = eventParser.ParseEventCall(call, repack: outputFull);
                // Go to CFG compilation if there are no errors
                // Should probably isolate contexts from eachother to make this less hacky and also enable parallelism
                if (errorCount == context.Errors.Count)
                {
                    func.Name = doc;
                    func.Header = header;
                    func.HeaderLine = new LineMapping { SourceLine = blockSourceLine };

                    if (mode == Mode.Reparse)
                    {
                        func.Print(writer);
                        lastRewrittenNode = stmt;
                        continue;
                    }
                    if (!outputFull && !func.Fancy)
                    {
                        // Only has partial instructions, but enough to reconstruct params
                        InitData.AddFromSource(init, docs, func);
                        continue;
                    }
                    if (mode == Mode.Repack)
                    {
                        // Track extra cosmetic state
                        func.CondHints = new Dictionary<int, string>();
                    }
                    EventCFG f = new EventCFG(func.ID, options);
                    EventCFG.Result res = f.Compile(func, docs.Translator);
                    if (res.Errors.Count == 0)
                    {
                        foreach (EventCFG.ResultError err in res.Warnings)
                        {
                            context.Warnings.Add(CompileError.FromInstr(err.Im, err.Message, func));
                        }
                        InitData.AddFromSource(init, docs, func);
                        if (mode == Mode.PackPreview)
                        {
                            // Do this here while everything is flat
                            // Enum values emitted from expanding conditions are just numbers otherwise
                            foreach (Intermediate im in func.Body)
                            {
                                if (!(im is Instr instr)) continue;
                                docs.Translator.AddDisplayEnums(instr);
                            }
                        }
                        if (mode == Mode.Repack)
                        {
                            f = new EventCFG(func.ID, options);
                            try
                            {
                                res = f.Decompile(func, docs.Translator);
                            }
                            catch (FancyNotSupportedException fancyEx)
                            {
                                // Fallback to existing definition. Continue top-level loop to avoid updating lastRewrittenNode
                                // This is necessary for vanilla Bloodborne 12425250
                                // TODO: This means that parameter names won't get changed to typed init
                                context.Warnings.Add(CompileError.FromInstr(fancyEx.Im, "Decompile skipped: " + fancyEx.Message, func));
                                continue;
                            }
                            // Add event names if given
                            if (doc == null && repackNames != null
                                && (func.Header.EndsWith('\n') || func.Header == "")
                                && func.ID is long nameId && repackNames.TryGetValue(nameId, out string newName))
                            {
                                func.Name = newName;
                                func.Header += $"// {newName}{Environment.NewLine}";
                            }
                        }
                        if (functionsToProcess != null)
                        {
                            functionsToProcess.Add(func);
                        }
                        else if (writeAny)
                        {
                            func.Print(writer);
                        }
                    }
                    else
                    {
                        foreach (EventCFG.ResultError err in res.Errors)
                        {
                            context.Errors.Add(CompileError.FromInstr(err.Im, err.Message, func));
                        }
                    }
                }
                lastRewrittenNode = stmt;
            }
            if (functionsToProcess != null)
            {
                // Do this afterwards, after all event inits have been analyzed
                if (repackLinks != null)
                {
                    repackLinks.Main = init;
                    foreach (EventFunction func in functionsToProcess)
                    {
                        foreach (InitData.ConvertError err in InitData.ConvertEventFunction(func, init))
                        {
                            context.Warnings.Add(CompileError.FromInstr(err.Im, err.Message, func));
                        }
                    }
                }
                foreach (EventFunction func in functionsToProcess)
                {
                    if (repackLinks != null)
                    {
                        foreach (InitData.ConvertError err in InitData.RewriteInits(func, docs, repackLinks))
                        {
                            context.Warnings.Add(CompileError.FromInstr(err.Im, err.Message, func));
                        }
                    }
                    func.Print(writer);
                }
            }
            if (writeAny)
            {
                string footer = source.GetTextBetweenNodes(lastRewrittenNode, null, out blockSourceLine);
                if (outputFull)
                {
                    footer = footer.Replace("\t", SingleIndent);
                }
                writer.RecordMapping(new LineMapping { SourceLine = blockSourceLine });
                writer.Write(footer);
                writer.Mappings.Sort();
            }

            source.AnnotateErrors(context.Errors, "ERROR");
            source.AnnotateErrors(context.Warnings, "WARNING");

            if (context.Errors.Count != 0)
            {
                throw new FancyCompilerException { Errors = context.Errors };
            }
            else
            {
                return new CompileOutput
                {
                    Code = writer?.ToString(),
                    LineMappings = writer?.Mappings,
                    Warnings = context.Warnings,
                    SourceContext = source,
                    MainInit = init,
                };
            }
        }

        public class CompileOutput
        {
            // Code for EventScripter
            public string Code { get; set; }
            // Mapping from old code to new code, for errors
            public List<LineMapping> LineMappings { get; set; }
            // Warnings to show
            public List<CompileError> Warnings { get; set; }
            // Original source for errors and diffing
            public SourceContext SourceContext { get; set; }
            // Declared events. Should set SourceInfo after writing the file
            public InitData.FileInit MainInit { get; set; }

            // Utility methods

            public void RewriteStackFrames(JSScriptException ex, string fileFilter)
            {
                List<LineMapping> mappings = LineMappings;
                if (mappings == null || mappings.Count == 0) return;
                foreach (JSScriptException.StackFrame frame in ex.Stack)
                {
                    if (frame.File != fileFilter) continue;
                    int printedLine = frame.Line;
                    int sourceLine;
                    int mapIndex = mappings.BinarySearch(new LineMapping { PrintedLine = printedLine });
                    if (mapIndex >= 0)
                    {
                        sourceLine = mappings[mapIndex].SourceLine;
                    }
                    else
                    {
                        // No exact match. mapIndex is the complement of the next eligible mapping
                        mapIndex = ~mapIndex;
                        if (mapIndex == mappings.Count)
                        {
                            // Printed line is past the last mapping. In this case, the last one is the closest.
                            mapIndex = mappings.Count - 1;
                        }
                        LineMapping closest = mappings[mapIndex];
                        sourceLine = closest.SourceLine + (printedLine - closest.PrintedLine);
                    }
                    string line = SourceContext.GetLine(sourceLine);
                    frame.Line = sourceLine;
                    frame.Column = 1;
                    if (line != null)
                    {
                        frame.Column = SourceContext.IndexOfNonWhiteSpace(line) + 1;
                        line = line.Trim();
                        if (line != frame.Text)
                        {
                            frame.ActualText = frame.Text;
                            frame.Text = line;
                        }
                    }
                }
            }

            public List<DiffSegment> GetDiffSegments()
            {
                List<DiffSegment> diffs = new List<DiffSegment>();
                SourceContext leftSrc = SourceContext;
                if (LineMappings.Count == 0)
                {
                    diffs.Add(new DiffSegment { Left = leftSrc.Code, Right = Code });
                    return diffs;
                }
                SourceContext rightSrc = SourceContext.FromText(Code, false);
                Stack<LineMapping> mappings = new Stack<LineMapping>(
                    LineMappings
                        .Concat(new[] { new LineMapping { SourceLine = leftSrc.LineCount + 1, PrintedLine = rightSrc.LineCount + 1 } })
                        .OrderBy(l => l.PrintedLine)
                        .Reverse());
                Stack<CompileError> warnings = new Stack<CompileError>(Warnings.OrderBy(w => w.Line).Reverse());
                int left = 0;
                int right = 0;
                StringBuilder leftPart = new StringBuilder();
                StringBuilder rightPart = new StringBuilder();
                void addLeft()
                {
                    leftPart.AppendLine(leftSrc.GetLine(left) ?? "");
                    left++;
                }
                void addRight()
                {
                    rightPart.AppendLine(rightSrc.GetLine(right) ?? "");
                    right++;
                }
                void addSegment()
                {
                    string leftStr = leftPart.ToString();
                    string rightStr = rightPart.ToString();
                    if (leftStr.Length > 0 || rightStr.Length > 0)
                    {
                        while (warnings.Count > 0 && left > warnings.Peek().Line)
                        {
                            CompileError warning = warnings.Pop();
                            diffs.Add(new DiffSegment { Left = warning.Message, Right = "", Warning = true });
                        }
                        diffs.Add(new DiffSegment { Left = leftStr, Right = rightStr });
                        leftPart.Clear();
                        rightPart.Clear();
                    }
                }
                while (mappings.Count > 0)
                {
                    LineMapping next = mappings.Pop();
                    // Catch up lines
                    if (left != next.SourceLine || right != next.PrintedLine)
                    {
                        while (left < next.SourceLine)
                        {
                            addLeft();
                        }
                        while (right < next.PrintedLine)
                        {
                            addRight();
                        }
                        addSegment();
                    }
                    // Matching lines
                    if (mappings.Count > 0)
                    {
                        LineMapping target = next;
                        while (mappings.Count > 0
                            && (mappings.Peek().SourceLine == next.SourceLine || mappings.Peek().PrintedLine == next.PrintedLine))
                        {
                            target = mappings.Pop();
                        }
                        int endLeft = target.SourceEndLine > 0 ? target.SourceEndLine : target.SourceLine;
                        while (left <= endLeft)
                        {
                            addLeft();
                        }
                        int endRight = target.PrintedEndLine > 0 ? target.PrintedEndLine : target.PrintedLine;
                        while (right <= endRight)
                        {
                            addRight();
                        }
                        addSegment();
                    }
                }
                return diffs;
            }
        }

        public class DiffSegment
        {
            public bool Warning { get; set; }
            public string Left { get; set; }
            public string Right { get; set; }
        }

        // https://stackoverflow.com/questions/2594125/reading-text-files-line-by-line-with-exact-offset-position-reporting
        // Also used in ESDLang
        private class PositionTrackingReader : TextReader
        {
            public TextReader Reader { get; set; }
            public int Position = 0;

            public override int Read()
            {
                Position++;
                return Reader.Read();
            }

            public override int Peek()
            {
                return Reader.Peek();
            }
        }

        public class FancyCompilerException : Exception
        {
            public List<CompileError> Errors { get; set; }
            public bool Warning { get; set; }

            public override string Message => string.Join("", Errors.Select(e => e.Message));
            public override string ToString() => Message;
        }
    }
}
