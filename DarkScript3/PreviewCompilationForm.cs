using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
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
        public string StartingCode { get; private set; }
        public string PendingCode { get; private set; }
        public List<FancyJSCompiler.DiffSegment> Segments { get; set; }

        private static TextStyle AddStyle = MakeHighlightStyle(0xE4, 0xFF, 0xE0);
        private static TextStyle ChangeStyle = MakeHighlightStyle(0xFF, 0xFF, 0xF0);
        private static TextStyle RemoveStyle = MakeHighlightStyle(0xFD, 0xEC, 0xF5);
        private static TextStyle WarningStyle = MakeHighlightStyle(0xE9, 0xDB, 0xF3);

        private static TextStyle MakeHighlightStyle(int r, int g, int b)
        {
            Color color = Color.FromArgb(r, g, b);
            Brush brush = new SolidBrush(color);
            return new TextStyle(null, brush, FontStyle.Regular);
        }

        public PreviewCompilationForm(Font font)
        {
            InitializeComponent();
            fctb1.Font = (Font)font.Clone();
            fctb2.Font = (Font)font.Clone();
        }

        public void SetSegments(List<FancyJSCompiler.DiffSegment> segments, string startingCode = null, string pendingCode = null)
        {
            Segments = segments;
            RewriteSegments();
            action.Visible = pendingCode != null;
            action.Enabled = true;
            Confirmed = false;
            StartingCode = startingCode;
            PendingCode = pendingCode;
        }

        private void RewriteSegments()
        {
            if (Segments == null) return;
            int warnings = 0;
            StringBuilder left = new();
            StringBuilder right = new();
            fctb1.Clear();
            fctb2.Clear();
            // Batch these together because AppendText is extremely slow and always redraws
            // This isn't an issue before the form is shown, at least
            TextStyle lastStyle = null;
            foreach (FancyJSCompiler.DiffSegment diff in Segments)
            {
                if (diff.Warning && !lineup.Checked) continue;
                TextStyle style = null;
                if (diff.Warning)
                {
                    style = WarningStyle;
                    warnings++;
                }
                else if (diff.Left.Length == 0)
                {
                    style = AddStyle;
                }
                else if (diff.Right.Length == 0)
                {
                    style = RemoveStyle;
                }
                else if (diff.Left != diff.Right)
                {
                    style = ChangeStyle;
                }
                if (style != lastStyle)
                {
                    fctb1.AppendText(left.ToString(), lastStyle == AddStyle ? null : lastStyle);
                    fctb2.AppendText(right.ToString(), lastStyle == RemoveStyle || lastStyle == WarningStyle ? null : lastStyle);
                    left.Clear();
                    right.Clear();
                }
                lastStyle = style;
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
            fctb1.AppendText(left.ToString(), lastStyle);
            fctb2.AppendText(right.ToString(), lastStyle);
            // ErrorMessageForm could be used, but it is modal and tied to EditorGUI where this form is persistent
            if (warnings == 0)
            {
                warningL.Visible = false;
            }
            else
            {
                warningL.Visible = true;
                warningL.Text = $"{warnings} warning{(warnings == 1 ? "" : "s")} produced";
            }
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

        private void fctb1_ZoomChanged(object sender, EventArgs e)
        {
            fctb2.Font = (Font)fctb1.Font.Clone();
        }

        private void fctb2_ZoomChanged(object sender, EventArgs e)
        {
            fctb1.Font = (Font)fctb2.Font.Clone();
        }
    }
}
