using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FastColoredTextBoxNS;

namespace DarkScript3
{
    public static class JSRegex
    {
        private static RegexOptions RegexCompiledOption
        {
            get
            {
                if (PlatformType.GetOperationSystemPlatform() == Platform.X86)
                    return RegexOptions.Compiled;
                else
                    return RegexOptions.None;
            }
        }

        public static Regex GetGlobalConstantRegex(IEnumerable<string> tokens)
        {
            // The tokens should all be alphanumeric
            return new Regex($@"[^.]\b(?<range>{string.Join("|", tokens)})\b", RegexCompiledOption);
        }

        public static Regex Property = new Regex(@"(\w|\$)+\.(?<range>(\w|\$)+)", RegexCompiledOption);
        public static Regex StringArg = new Regex(@"\bX\d+_\d+\b", RegexCompiledOption);
        public static Regex String = new Regex(@"""""|''|"".*?[^\\]""|'.*?[^\\]'", RegexCompiledOption);
        public static Regex Comment1 = new Regex(@"//.*$", RegexOptions.Multiline | RegexCompiledOption);
        public static Regex Comment2 = new Regex(@"(/\*.*?\*/)|(/\*.*)", RegexOptions.Singleline | RegexCompiledOption);
        public static Regex Comment3 = new Regex(@"(/\*.*?\*/)|(.*\*/)",
                                         RegexOptions.Singleline | RegexOptions.RightToLeft | RegexCompiledOption);
        // Allow negative sign here so that negative numbers can be highlighted as tokens.
        public static Regex Number = new Regex(@"(\b|-)\d+[\.]?\d*([eE]\-?\d+)?[lLdDfF]?\b|\b0x[a-fA-F\d]+\b",
                                       RegexCompiledOption);
        public static Regex Keyword =
            new Regex(
                @"\b("
                    // Reserved in ECMAScript 2015
                    + "break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|finally|for|function"
                    + "|if|import|in|instanceof|new|return|super|switch|this|throw|try|typeof|var|void|while|with|yield"
                    // Reserved in strict mode/modules
                    + "|await|implements|interface|let|package|private|protected|public|static|yield"
                    // Custom
                    + "|true|false"
                    // TODO: Make this look not awful
                    // + string.Join("", ScriptAst.ReservedWords.Where(b => b.Value.Highlight).Select(b => $"|{b.Key}"))
                + @")\b"
                + @"|\$Event\b",
                RegexCompiledOption);
        public static Regex DataType = new Regex(@"\b(byte|short|int|sbyte|ushort|uint|enum|bool|float)\b", RegexCompiledOption);
    }
}
