using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using SoulsFormats;

namespace DarkScript3
{
    public partial class GUI : Form
    {
        public EventScripter Scripter;

        public string EVD_Path;

        public GUI()
        {
            InitializeComponent();

            menuStrip1.Renderer = new DarkToolStripRenderer();
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null) return;
            try
            {
                Scripter.Pack(editor.Text).Write(EVD_Path);
                File.WriteAllText($"{EVD_Path}.js", editor.Text);
            }
            catch (Exception ex)
            {
                var scriptException = ex as IScriptEngineException;
                if (scriptException != null)
                {
                    string details = scriptException.ErrorDetails;
                    Console.WriteLine(details);
                    MessageBox.Show(details);
                }
                else
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                var chooser = new GameChooser();
                chooser.ShowDialog();
                try
                {
                    Scripter = new EventScripter(ofd.FileName, chooser.GameDocs);
                    if (File.Exists($"{ofd.FileName}.js"))
                    {
                        editor.Text = File.ReadAllText($"{ofd.FileName}.js");
                    }
                    else
                    {
                        editor.Text = Scripter.Unpack();
                        EVD_Path = ofd.FileName;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        private void GUI_Load(object sender, EventArgs e)
        {
            //var sb = new StringBuilder();
            //sb.AppendLine("Event(0, Default, null, function () {");
            //sb.AppendLine("\tInitializeEvent(0, 260, 11810000);");
            //sb.AppendLine("});");
            //editor.Text = sb.ToString();
        }

        private void EmevdDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null) return;
            (new InfoViewer(Scripter)).ShowDialog();
        }

        public static TextStyle MakeStyle(int r, int g, int b, FontStyle f = FontStyle.Regular)
        {
            var color = Color.FromArgb(r, g, b);
            return MakeStyle(new SolidBrush(color), f);
        }


        private static TextStyle MakeStyle(Brush b, FontStyle f = FontStyle.Regular)
        {
            Styles.Add(new TextStyle(b, Brushes.Transparent, f));
            return Styles.Last();
        }

        private static List<TextStyle> Styles = new List<TextStyle>();

        private static class TextStyles
        {
            public static TextStyle Comment = MakeStyle(87, 166, 74);
            public static TextStyle String = MakeStyle(214, 157, 133);
            public static TextStyle Keyword = MakeStyle(86, 156, 214);
            public static TextStyle FunctionCall = MakeStyle(78, 201, 176);
            public static TextStyle EnumConstant = MakeStyle(181, 206, 168);
            public static TextStyle Number = MakeStyle(181, 206, 168);
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.ClearStyle(Styles.ToArray());
            e.ChangedRange.SetStyle(TextStyles.Comment, JSRegex.Comment1);
            e.ChangedRange.SetStyle(TextStyles.Comment, JSRegex.Comment2);
            e.ChangedRange.SetStyle(TextStyles.Comment, JSRegex.Comment1);
            e.ChangedRange.SetStyle(TextStyles.String, JSRegex.String);
            e.ChangedRange.SetStyle(TextStyles.Keyword, JSRegex.Keyword);
            e.ChangedRange.SetStyle(TextStyles.Number, JSRegex.Number);
            e.ChangedRange.SetStyle(TextStyles.EnumConstant, new Regex(@"(^|\W)(?<range>\$\w*)")); //accessors starting with $

        }

        public static RegexOptions RegexCompiledOption
        {
            get
            {
                if (PlatformType.GetOperationSystemPlatform() == Platform.X86)
                    return RegexOptions.Compiled;
                else
                    return RegexOptions.None;
            }
        }

        public static class JSRegex
        {
            public static Regex String = new Regex(@"""""|''|"".*?[^\\]""|'.*?[^\\]'", RegexCompiledOption);
            public static Regex Comment1 = new Regex(@"//.*$", RegexOptions.Multiline | RegexCompiledOption);
            public static Regex Comment2 = new Regex(@"(/\*.*?\*/)|(/\*.*)", RegexOptions.Singleline | RegexCompiledOption);
            public static Regex Comment3 = new Regex(@"(/\*.*?\*/)|(.*\*/)",
                                             RegexOptions.Singleline | RegexOptions.RightToLeft | RegexCompiledOption);
            public static Regex Number = new Regex(@"\b\d+[\.]?\d*([eE]\-?\d+)?[lLdDfF]?\b|\b0x[a-fA-F\d]+\b",
                                           RegexCompiledOption);
            public static Regex Keyword =
                new Regex(
                    @"\b(true|false|break|case|catch|const|continue|default|delete|do|else|export|for|function|if|in|instanceof|new|null|return|switch|this|throw|try|var|void|while|with|typeof)\b",
                    RegexCompiledOption);
        }
    }
}
