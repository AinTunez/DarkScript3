using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ClearScript;

namespace DarkScript3
{
    public class JSScriptException : Exception
    {
        public List<StackFrame> Stack { get; set; }

        // ClearScript doesn't seem to provide any kind of structured way to get this info.
        // Example lines:
        // at SetEventFlag(Script[159]:7:13) ->             return Scripter.MakeInstruction(_event, bank, index, hostArray(args));
        // at common.emevd.dcx.js:4:5
        // at common.emevd.dcx [2].js [temp]:4:5 ->     SetEventFlag(760, hello());
        private static readonly Regex stackRe = new Regex(@"^\s*at ([^:\(]+)\s*(?:\(([^:]+))?\s*:(\d+):(\d+)\)?(?:\s*->\s*(.*))?$");
        private static readonly Regex versionRe = new Regex(@"\s*\[[^\]]*\]");

        public JSScriptException(string message, List<StackFrame> stack) : base(message)
        {
            Stack = stack;
        }

        public override string ToString()
        {
            StackFrame highlight = Stack.Find(stack => stack.File.EndsWith(".js"));
            return Message + string.Join("\n", Stack) + (highlight == null ? "" : $"\n\n{highlight.File} line {highlight.Line}");
        }

        public static JSScriptException FromV8(IScriptEngineException scriptException)
        {
            StringBuilder message = new StringBuilder();
            string[] lines = scriptException.ErrorDetails.Split('\n');
            List<StackFrame> frames = new List<StackFrame>();
            foreach (string line in lines)
            {
                Match match = stackRe.Match(line);
                if (match.Success)
                {
                    StackFrame frame = new StackFrame
                    {
                        Line = int.Parse(match.Groups[3].Value),
                        Column = int.Parse(match.Groups[4].Value),
                        Text = match.Groups[5].Success ? match.Groups[5].Value : null,
                    };
                    if (match.Groups[2].Success)
                    {
                        frame.Symbol = match.Groups[1].Value.Trim();
                        frame.File = versionRe.Replace(match.Groups[2].Value, "").Trim();
                    }
                    else
                    {
                        frame.File = versionRe.Replace(match.Groups[1].Value, "").Trim();
                    }
                    if (frame.File == "Script")
                    {
                        frame.File = "Editor";
                    }
                    frames.Add(frame);
                }
                else
                {
                    message.AppendLine(line);
                }
            }
            return new JSScriptException(Regex.Replace(message.ToString(), "^Error: ", ""), frames);
        }

        public class StackFrame
        {
            public string File { get; set; }
            public int Line { get; set; }
            public int Column { get; set; }
            // Optional
            // Function name, provided by V8
            public string Symbol { get; set; }
            // The source line
            public string Text { get; set; }
            // The line that was actualy evaluated, if it differs
            public string ActualText { get; set; }

            public override string ToString()
            {
                string loc = $"{File}:{Line}:{Column}";
                return $"    at "
                    + (Symbol == null ? loc : $"{Symbol} ({loc})")
                    + (Text == null ? "" : $" -> {Text}")
                    + (ActualText == null ? "" : $"\n      [compiled] {ActualText}");
            }
        }
    }
}
