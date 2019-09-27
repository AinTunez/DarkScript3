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

        private void ExecToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                Scripter.Pack(editor.Text);
            } catch (Exception ex)
            {
                var scriptException = ex as IScriptEngineException;
                if (scriptException != null)
                {
                    string details = scriptException.ErrorDetails;
                    Console.WriteLine(details);
                    //details = Regex.Replace(details, @"(\s+)at Script\s*\[.*\]:", "$1at ");
                    MessageBox.Show(details);
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
    }
}
