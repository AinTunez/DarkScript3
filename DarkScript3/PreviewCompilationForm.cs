using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastColoredTextBoxNS;

namespace DarkScript3
{
    public partial class PreviewCompilationForm : Form
    {
        public bool Confirmed { get; private set; }
        public string PendingCode { get; private set; }
        public List<FancyJSCompiler.DiffSegment> Segments { get; set; }

        public PreviewCompilationForm(Font font)
        {
            InitializeComponent();
            fctb1.Font = font;
            fctb2.Font = font;
        }

        public void SetSegments(List<FancyJSCompiler.DiffSegment> segments, string pendingCode = null)
        {
            Segments = segments;
            RewriteSegments();
            action.Visible = pendingCode != null;
            action.Enabled = true;
            Confirmed = false;
            PendingCode = pendingCode;
        }

        private void RewriteSegments()
        {
            if (Segments == null) return;
            StringBuilder left = new StringBuilder();
            StringBuilder right = new StringBuilder();
            foreach (FancyJSCompiler.DiffSegment diff in Segments)
            {
                if (diff.Warning && !lineup.Checked) continue;
                left.Append(diff.Left);
                right.Append(diff.Right);
                int lines1 = diff.Left.Count(c => c == '\n');
                int lines2 = diff.Right.Count(c => c == '\n');
                if (lines1 != lines2 && lineup.Checked)
                {
                    StringBuilder pad = left;
                    int padCount = lines2 - lines1;
                    if (padCount < 0)
                    {
                        pad = right;
                        padCount = -padCount;
                    }
                    for (int i = 0; i < padCount; i++)
                    {
                        pad.AppendLine("↪");
                    }
                }
            }
            fctb1.Text = left.ToString();
            fctb2.Text = right.ToString();
        }

        public void DisableConversion()
        {
            if (!action.IsDisposed)
            {
                action.Enabled = false;
            }
        }

        private bool updating = false;
        private void fctb_VisibleRangeChanged(object sender, EventArgs e)
        {
            if (updating) return;
            FastColoredTextBox origin = sender as FastColoredTextBox;
            UpdateScroll(origin == fctb1 ? fctb2 : fctb1, origin.VerticalScroll.Value);
            fctb1.Refresh();
            fctb2.Refresh();
        }

        private void UpdateScroll(FastColoredTextBox tb, int y)
        {
            if (updating) return;

            updating = true;

            if (y <= tb.VerticalScroll.Maximum)
            {
                tb.VerticalScroll.Value = y;
                tb.UpdateScrollbars();
            }

            updating = false;
        }

        private void action_Click(object sender, EventArgs e)
        {
            Confirmed = true;
            Close();
        }

        private void lineup_CheckedChanged(object sender, EventArgs e)
        {
            RewriteSegments();
        }
    }
}
