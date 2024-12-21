using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using FastColoredTextBoxNS;

namespace DarkScript3
{
    public partial class ErrorMessageForm : Form
    {
        private static readonly TextStyle linkStyle = new TextStyle(Brushes.Blue, null, FontStyle.Underline);
        private static readonly Regex placePartsRe = new Regex(@"(\d+):(\d+)");

        public Place? Place { get; set; }

        private readonly int textMarginHeight;
        private readonly int textMarginWidth;
        private Regex placeRe;

        public ErrorMessageForm(Font font)
        {
            InitializeComponent();

            textMarginHeight = Height - box.Height;
            textMarginWidth = Width - box.Width;
            MinimumSize = new Size(Width, Height);
            box.Font = (Font)font.Clone();
        }

        public void SetMessage(string file, List<Exception> exs, string extra, bool isWarning)
        {
            string errorText = string.Join("\n", exs.Select(ex => ex + "\n")) + extra;
            SetTextAndResize(errorText.Trim());
            // Limit script exceptions to open file for the moment, as only one file open at a time
            placeRe = new Regex($@"(?<={Regex.Escape(file)}:)\d+:\d+|(?<=\n)\d+:\d+(?=:)");
            if (placeRe != null)
            {
                box.Range.SetStyle(linkStyle, placeRe);
            }
            Text = $"{(isWarning ? "Warning" : "Error")} saving {file}";
        }

        private void SetTextAndResize(string text)
        {
            box.Text = text;
            Rectangle area = Screen.FromControl(this).WorkingArea;
            string[] lines = text.Split('\n');
            Width = Math.Max(MinimumSize.Width, Math.Min(area.Width, textMarginWidth + box.CharWidth * lines.Max(l => l.Length)));
            box.NeedRecalc(true);
            Height = Math.Max(MinimumSize.Height, Math.Min(area.Height, textMarginHeight + box.TextHeight));
            Location = new Point
            {
                X = Math.Max(area.X, area.X + (area.Width - Width) / 2),
                Y = Math.Max(area.Y, area.Y + (area.Height - Height) / 2),
            };
        }

        private void box_MouseMove(object sender, MouseEventArgs e)
        {
            bool linkHover = box.GetStylesOfChar(box.PointToPlace(e.Location)).Contains(linkStyle);
            box.Cursor = linkHover ? Cursors.Hand : Cursors.Default;
        }

        private void box_MouseUp(object sender, MouseEventArgs e)
        {
            Place place = box.PointToPlace(e.Location);
            bool linkHover = box.GetStylesOfChar(place).Contains(linkStyle);
            if (linkHover)
            {
                string lineText = box.GetLineText(place.iLine);
                Match m = placePartsRe.Match(lineText);
                if (m.Success)
                {
                    Place = new Place(Math.Max(0, int.Parse(m.Groups[2].Value) - 1), Math.Max(0, int.Parse(m.Groups[1].Value) - 1));
                    Close();
                }
            }
        }

        private void button_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ErrorMessageForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }
    }
}
