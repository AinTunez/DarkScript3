using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Esprima;
using Esprima.Ast;
using static DarkScript3.ScriptAst;
using static DarkScript3.InstructionTranslator;
using static SoulsFormats.EMEVD;

namespace DarkScript3
{
    public class FancyJSCompiler
    {
        private EventCFG.CFGOptions options;

        public FancyJSCompiler(EventCFG.CFGOptions options = null)
        {
            this.options = options;
        }

        public class SourceContext
        {
            public string Code { get; set; }
            public List<(int, string)> Lines { get; set; }
            public Stack<SourceDecoration> PendingDecorations { get; set; }

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

            public void SkipTo(Node node)
            {
                if (PendingDecorations.Count == 0) return;
                int pos = GetOffset(node.Location.Start);
                while (PendingDecorations.Count > 0 && PendingDecorations.Peek().Position < pos)
                {
                    PendingDecorations.Pop();
                }
            }

            public List<SourceDecoration> GetDecorationsForNode(Node node)
            {
                if (PendingDecorations.Count == 0) return null;
                int endLine = node.Location.End.Line;
                int lineStart = Lines[endLine - 1].Item1;
                int nextLineStart = endLine < Lines.Count ? Lines[endLine].Item1 : Code.Length;
                List<SourceDecoration> ret = null;
                while (PendingDecorations.Count > 0 && PendingDecorations.Peek().Position < nextLineStart)
                {
                    SourceDecoration dec = PendingDecorations.Pop();
                    if (dec.Position < lineStart && dec.Comment != null)
                    {
                        dec.Type = SourceDecoration.DecorationType.PRE_COMMENT;
                    }
                    if (ret == null) ret = new List<SourceDecoration>();
                    ret.Add(dec);
                }
                return ret;
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

            public void TransformErrors(List<CompileError> errors, string header)
            {
                foreach (CompileError err in errors)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"{header}{(err.Event == null ? "" : $" in event {err.Event}")}: {err.Message}");
                    if (err.Loc is Position sourceLoc && Lines.Count > 0)
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

            // Possible feature: provide a range? Or do it before calling pack
            public static SourceContext FromText(string code, bool decorating)
            {
                List<(int, string)> lines = new List<(int, string)>();
                PositionTrackingReader reader = new PositionTrackingReader { Reader = new StringReader(code) };
                List<SourceDecoration> decorations = new List<SourceDecoration>();
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
                        decorations.Add(new SourceDecoration
                        {
                            Type = SourceDecoration.DecorationType.PRE_BLANK,
                            Position = position,
                        });
                    }
                    lines.Add((position, line));
                }
                if (decorating)
                {
                    // For every line: Pre (# of blank lines, arbitrary comments), post (post-comment)
                    Scanner commentScanner = new Scanner(code, new ParserOptions { Comment = true });
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
                    PendingDecorations = new Stack<SourceDecoration>(decorations.OrderByDescending(d => d.Position))
                };

            }
        }

        public class SourceNode
        {
            public Node Node { get; set; }
            public string Source { get; set; }

            public override string ToString() => Source;
        }

        public class CompileError
        {
            public Position? Loc { get; set; }
            public int Line { get; set; }
            public string Message { get; set; }
            public object Event { get; set; }

            public static CompileError FromNode(Node node, string message, object ev)
            {
                Position? loc = node == null ? (Position?)null : node.Location.Start;
                return new CompileError
                {
                    Loc = loc,
                    Line = loc?.Line ?? 0,
                    Message = message,
                    Event = ev,
                };
            }

            public static CompileError FromInstr(Intermediate im, string message, object ev)
            {
                Position? loc = im?.LineMapping == null ? (Position?)null : new Position(im.LineMapping.SourceLine, 0);
                return new CompileError
                {
                    Loc = loc,
                    Line = loc?.Line ?? 0,
                    Message = message,
                    Event = ev,
                };
            }
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

            // Clone
            public WalkContext Copy(object ev = null)
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

        public class EventParser
        {
            public SourceContext source { get; set; }
            public WalkContext context { get; set; }
            public InstructionDocs docs { get; set; }

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
                if (doc.OptionalArgs == 0)
                {
                    if (call.Arguments.Count != doc.Args.Count)
                    {
                        context.Error(call, $"Expected {Plural(doc.Args.Count)} for {f} but {call.Arguments.Count} given");
                        return null;
                    }
                }
                else
                {
                    int min = doc.Args.Count - doc.OptionalArgs;
                    if (call.Arguments.Count < min || call.Arguments.Count > doc.Args.Count)
                    {
                        context.Error(call, $"Expected {min} to {Plural(doc.Args.Count)} for {f} but {call.Arguments.Count} given");
                        return null;
                    }
                }
                return new CmdCond { Name = f, Args = call.Arguments.Select(a => (object)source.GetSourceNode(a)).ToList() };
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
                else if (id.Name.StartsWith("X"))
                {
                    // TODO: May want to check actual parameters, especially if parameter names change
                    context.Error(id, $"Condition variable {id.Name} looks like a parameter?");
                }
            }

            private Cond ConvertCondExpression(Expression expr)
            {
                if (expr is UnaryExpression unary)
                {
                    Cond c = ConvertCondExpression(unary.Argument);
                    if (unary.Operator == UnaryOperator.LogicalNot)
                    {
                        c.Negate = !c.Negate;
                    }
                    else
                    {
                        context.Error(expr, $"Operator {unary.Operator} used when only ! is supported for a condition");
                    }
                    return c;
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
                        CompareCond cmp = new CompareCond { Type = comp, Rhs = source.GetSourceNode(bin.Right) };
                        if (bin.Left is CallExpression call)
                        {
                            cmp.CmdLhs = ConvertCommandCond(call) ?? new CmdCond { Name = "Error" };
                        }
                        else
                        {
                            cmp.Lhs = source.GetSourceNode(bin.Left);
                        }
                        return cmp;
                    }
                }
                else if (expr is CallExpression call)
                {
                    return (Cond)ConvertCommandCond(call) ?? new ErrorCond();
                }
                else if (expr is MemberExpression mem)
                {
                    if (mem is StaticMemberExpression stat && stat.Object is Identifier objId
                        && stat.Property is Identifier propId && propId.Name == "Passed")
                    {
                        ValidateConditionVariable(objId);
                        return new CondRef { Name = objId.Name, Compiled = true };
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
                    return new CondRef { Name = id.Name };
                }
                else
                {
                    context.Error(expr, $"Unexpected condition {expr.Type}. Should be a function call, condition variable, comparison, or combination of these");
                    return new ErrorCond();
                }
            }

            public CondAssign ConvertAssign(Expression lhs, AssignmentOperator assignOp, Expression rhs)
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
                im.Decorations = source.GetDecorationsForNode(decorationNode);
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
                        bool hasExpectedArgs(int expected)
                        {
                            if (args.Count != expected)
                            {
                                context.Error(call, $"Expected {Plural(expected)} for {f} but {args.Count} given");
                                return false;
                            }
                            return true;
                        }
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
                        else if (f != null && docs.Functions.TryGetValue(f, out (int, int) pos))
                        {
                            EMEDF.InstrDoc instrDoc = docs.DOC[pos.Item1][pos.Item2];
                            object layers = null;
                            if (args.Count > 0 && args[args.Count - 1] is CallExpression lcall && lcall.Callee is Identifier lid && lid.Name == "$LAYERS")
                            {
                                layers = source.GetSourceNode(lcall);
                                args.RemoveAt(args.Count - 1);
                            }
                            if (docs.IsVariableLength(instrDoc) || hasExpectedArgs(instrDoc.Arguments.Length))
                            {
                                // Getting the int value is required when compiling things with control/negate arguments etc.
                                object getSourceArg(Expression arg)
                                {
                                    SourceNode node = source.GetSourceNode(arg);
                                    if (docs.EnumValues.TryGetValue(node.Source, out int val))
                                    {
                                        return new DisplayArg { DisplayValue = node.Source, Value = val };
                                    }
                                    return node;
                                }
                                // We do pretty minimal checking of arguments; further validation is saved for JS execution time.
                                im = new Instr
                                {
                                    Cmd = InstructionDocs.FormatInstructionID(pos.Item1, pos.Item2),
                                    Name = f,
                                    Args = args.Select(getSourceArg).ToList(),
                                    Layers = layers
                                };
                            }
                        }
                        else if (f != null && char.IsLower(f[0]))
                        {
                            // Allow function calls through unmodified if they are lowercase
                            im = new JSStatement { Code = source.GetSourceNode(statement).ToString() };
                        }
                        else
                        {
                            if (f != null)
                            {
                                context.Error(call, $"Unknown function name {f}. Use lowercase names to call regular JS functions.");
                            }
                            im = null;
                        }
                        ret.Add(im);
                    }
                    else if (exprStmt.Expression is AssignmentExpression assign)
                    {
                        ret.Add(ConvertAssign(assign.Left, assign.Operator, assign.Right));
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
                        ret.Add(new JSStatement { Code = source.GetSourceNode(statement).ToString() });
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
                    if (ifs.Alternate != null) ifelse.False = ConvertStatement(ifs.Alternate);
                    ret.Add(ifelse);
                }
                else
                {
                    // May need to explain in greater detail per unsupported type or what alternatives there are or might be.
                    context.Error(statement, $"{statement.Type} not supported here");
                }
                return ret;
            }

            private EventFunction ConvertEvent(object id, Event.RestBehaviorType restBehavior, FunctionExpression func)
            {
                // func.Id is currently meaningless, like making an anonymous function with function somename() {}
                // Otherwise, should have plain params, block statement body, and no attributes like Generator/Expression/Async/Strict.
                List<string> args = func.Params.Select(param =>
                {
                    if (param is Identifier pid)
                    {
                        return pid.Name;
                    }
                    context.Error(param, "Param not a plain identifier");
                    return "error";
                }).ToList();
                EventFunction ret = new EventFunction { ID = id, RestBehavior = restBehavior, Params = args };
                if (func.Body is BlockStatement block)
                {
                    // Reset state and parse
                    extraLabels = new List<string>();
                    cmdId = 0;
                    ret.Body = ConvertStatement(block);
                    ret.EndComments = source.GetDecorationsForNode(func);
                    ret.LineMapping = new LineMapping { SourceLine = func.Location.Start.Line };
                    if (extraLabels.Count > 0)
                    {
                        context.Error(func, $"Extra labels {string.Join(",", extraLabels)} at the end not assigned to any instruction");
                    }
                }
                else
                {
                    context.Error(func.Body, $"Event function body should be a {{ bunch of statements }}, found {func.Body.Type}");
                }
                if (func.Generator || func.Async || func.Strict)
                {
                    context.Error(func, "Event function shouldn't have extra annotations, but found generator/async/strict");
                }
                return ret;
            }

            public static CallExpression GetEventCall(Statement stmt, bool allowVanilla)
            {
                if (stmt is ExpressionStatement exprStmt
                    && exprStmt.Expression is CallExpression call
                    && call.Callee is Identifier id && (id.Name == "$Event" || allowVanilla && id.Name == "Event"))
                {
                    return call;
                }
                return null;
            }

            private static readonly Dictionary<string, Event.RestBehaviorType> behaviorTypes =
                ((Event.RestBehaviorType[])Enum.GetValues(typeof(Event.RestBehaviorType))).ToDictionary(t => t.ToString(), t => t);

            public EventFunction ParseEventCall(CallExpression call)
            {
                if (call.Arguments.Count == 3
                    && call.Arguments[1] is Identifier rest
                    && behaviorTypes.TryGetValue(rest.Name, out Event.RestBehaviorType restBehavior)
                    && call.Arguments[2] is FunctionExpression funcExpr)
                {
                    object eventId = source.GetSourceNode(call.Arguments[0]);
                    if (int.TryParse(eventId.ToString(), out int actualId))
                    {
                        eventId = actualId;
                    }
                    context.Event = eventId;
                    return ConvertEvent(eventId, restBehavior, funcExpr);
                }
                else
                {
                    context.Error(call, "Expected event call with three arguments: an integer id, a rest behavior, and a function expression");
                    return null;
                }
            }
        }

        public CompileOutput Compile(string code, InstructionDocs docs, bool repack = false, bool printFancyEnums = true)
        {
            WalkContext context = new WalkContext();

            Esprima.Ast.Program program;
            try
            {
                JavaScriptParser parser = new JavaScriptParser(code, new ParserOptions { });
                program = parser.ParseScript(false);
                // "ERROR: <message>\n{line}:{col}: line"
            }
            catch (ParserException ex)
            {
                if (ex.Error is ParseError err)
                {
                    Position? pos = null;
                    if (err.IsPositionDefined)
                    {
                        // These columns appear to be mostly 1-indexed, so change them to 0-indexed to match Node positions.
                        pos = new Position(err.Position.Line, Math.Max(0, err.Position.Column - 1));
                    }
                    context.Errors.Add(new CompileError { Line = err.LineNumber, Loc = pos, Message = err.Description });
                    SourceContext tempSource = SourceContext.FromText(code, decorating: false);
                    tempSource.TransformErrors(context.Errors, "ERROR");
                }
                else
                {
                    context.Errors.Add(new CompileError { Message = "ERROR: " + ex.ToString() });
                }
                throw new FancyCompilerException { Errors = context.Errors };
            }

            SourceContext source = SourceContext.FromText(code, decorating: repack);

            EventParser eventParser = new EventParser { context = context, source = source, docs = docs };

            Node lastRewrittenNode = null;
            StringWriter stringOutput = new StringWriter();
            LineTrackingWriter writer = new LineTrackingWriter { Writer = stringOutput };
            int blockSourceLine;
            foreach (Statement stmt in program.Body)
            {
                CallExpression call = EventParser.GetEventCall(stmt, allowVanilla: repack);
                if (call == null) continue;
                string header = source.GetTextBetweenNodes(lastRewrittenNode, stmt, out blockSourceLine);
                source.SkipTo(call);

                int errorCount = context.Errors.Count;
                EventFunction func = eventParser.ParseEventCall(call);
                // Go to CFG compilation if there are no errors
                // Should probably isolate contexts from eachother to make this less hacky and also enable parallelism
                if (errorCount == context.Errors.Count)
                {
                    EventCFG f = new EventCFG(func.ID, options);
                    EventCFG.Result res = f.Compile(func, docs.Translator);
                    if (res.Errors.Count == 0)
                    {
                        foreach (EventCFG.ResultError err in res.Warnings)
                        {
                            context.Warnings.Add(CompileError.FromInstr(err.Im, err.Message, func.ID));
                        }
                        if (printFancyEnums)
                        {
                            // Do this here while everything is flat
                            foreach (Intermediate im in func.Body)
                            {
                                if (!(im is Instr instr)) continue;
                                docs.Translator.AddDisplayEnums(instr);
                            }
                        }
                        if (repack)
                        {
                            f = new EventCFG(func.ID, options);
                            try
                            {
                                res = f.Decompile(func, docs.Translator);
                            }
                            catch (FancyNotSupportedException fancyEx)
                            {
                                // Fallback to existing definition. Continue top-level loop to avoid updating lastRewrittenNode
                                context.Warnings.Add(CompileError.FromInstr(fancyEx.Im, "Decompile skipped: " + fancyEx.Message, func.ID));
                                continue;
                            }
                            // res.Errors.Count should be 0, as we assume that if it's an emevd, it must be valid.
                            // This might not necessarily be the case for repacking, it may throw an internal error, but we'll see.
                        }
                        if (repack)
                        {
                            header = header.Replace("\t", SingleIndent);
                        }
                        writer.RecordMapping(new LineMapping { SourceLine = blockSourceLine });
                        writer.Write(header);
                        func.Print(writer);
                    }
                    else
                    {
                        foreach (EventCFG.ResultError err in res.Errors)
                        {
                            context.Errors.Add(CompileError.FromInstr(err.Im, err.Message, func.ID));
                        }
                    }
                }
                lastRewrittenNode = stmt;
            }
            string footer = source.GetTextBetweenNodes(lastRewrittenNode, null, out blockSourceLine);
            if (repack)
            {
                footer = footer.Replace("\t", SingleIndent);
            }
            writer.RecordMapping(new LineMapping { SourceLine = blockSourceLine });
            writer.Write(footer);

            source.TransformErrors(context.Errors, "ERROR");
            source.TransformErrors(context.Warnings, "WARNING");
            writer.Mappings.Sort();

            if (context.Errors.Count != 0)
            {
                throw new FancyCompilerException { Errors = context.Errors };
            }
            else
            {
                return new CompileOutput
                {
                    Code = writer.ToString(),
                    LineMappings = writer.Mappings,
                    Warnings = context.Warnings,
                    SourceContext = source,
                };
            }
        }

        public class CompileOutput
        {
            public string Code { get; set; }
            public List<LineMapping> LineMappings { get; set; }
            public List<CompileError> Warnings { get; set; }
            public SourceContext SourceContext { get; set; }

            // Utility methods

            public void RewriteStackFrames(JSScriptException ex, string fileFilter)
            {
                List<LineMapping> mappings = LineMappings;
                if (mappings.Count == 0) return;
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
                        .OrderByDescending(l => l.PrintedLine));
                Stack<CompileError> warnings = new Stack<CompileError>(Warnings.OrderByDescending(w => w.Line));
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
                        diffs.Add(new DiffSegment { Left = leftStr, Right = rightStr });
                        leftPart.Clear();
                        rightPart.Clear();
                        if (warnings.Count > 0 && left > warnings.Peek().Line)
                        {
                            CompileError warning = warnings.Pop();
                            diffs.Add(new DiffSegment { Left = warning.Message, Right = "", Warning = true });
                        }
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
