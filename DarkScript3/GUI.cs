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

        public GUI()
        {
            InitializeComponent();
        }

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Scripter.Pack(editor.Text).Write("output/test.emevd.dcx", DCX.Type.DarkSouls3);
            }
            catch (Exception ex)
            {
                var scriptException = ex as IScriptEngineException;
                if (scriptException != null)
                {
                    string details = scriptException.ErrorDetails;
                    Console.WriteLine(details);
                    //details = Regex.Replace(details, @"(\s+)at Script\s*\[.*\]:", "$1at ");
                    MessageBox.Show(details);
                } else
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
                Scripter = new EventScripter(ofd.FileName, chooser.GameDocs);
                editor.Text = Scripter.Unpack();
            }
        }

        private void GUI_Load(object sender, EventArgs e)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Event(0, Default, null, function () {");
            sb.AppendLine("\tInitializeEvent(0, 260, 11810000);");
            sb.AppendLine("});");
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
            public static TextStyle String = MakeStyle(214,157,133);
            public static TextStyle Keyword = MakeStyle(220,220,170);
            public static TextStyle FunctionCall = MakeStyle(78,201,176);
            public static TextStyle EnumConstant = MakeStyle(181,206,168);
            public static TextStyle Number = MakeStyle(181,206,168);
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.ClearStyle(Styles.ToArray());

            //strings
            e.ChangedRange.SetStyle(TextStyles.String, new Regex(@""".*"""));
            e.ChangedRange.SetStyle(TextStyles.Keyword, new Regex(@"[^$]\b(function|var|const|new|true|false)\b[^$]")); //keywords
            e.ChangedRange.SetStyle(TextStyles.FunctionCall, new Regex(@"(?<range>(\w|\$)+)\s*\(")); //global constants
            e.ChangedRange.SetStyle(TextStyles.EnumConstant, new Regex(@"(^|\W)(?<range>\$\w*)")); //accessors starting with $
            e.ChangedRange.SetStyle(TextStyles.Number, new Regex(@"[\d.]+")); //numbers
        }
    }
}
