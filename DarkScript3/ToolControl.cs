using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastColoredTextBoxNS;

namespace DarkScript3
{
    public partial class ToolControl : UserControl
    {
        public ToolControl()
        {
            InitializeComponent();
        }

        public void SetText(string s)
        {
            tipBox.Text = s;

            int width = tipBox.Lines.Max(e => e.Length) * tipBox.CharWidth;
            width += Padding.Horizontal + tipPanel.Padding.Horizontal + 2;

            int height = (tipBox.CharHeight + tipBox.LineInterval) * tipBox.LinesCount;
            height += Padding.Vertical + tipPanel.Padding.Vertical + 2;

            Size = new Size(width, height);
        }

        public void ShowAtPosition(Control origin, Point p, int afterHeight)
        {
            if (Parent == null)
            {
                // This should be added to the parent before the editor uses it, but not really show-stopping if not.
                Hide();
                return;
            }
            p = new Point(p.X, p.Y);

            // Translate to parent coords via screen coords.
            Point originLoc = origin.PointToScreen(new Point());
            Point parentLoc = Parent.PointToScreen(new Point());
            p.Offset(originLoc.X - parentLoc.X, originLoc.Y - parentLoc.Y);

            // By default, try to show below with enough space
            if (p.Y + afterHeight + 2 + Height < Parent.Height)
            {
                p.Offset(0, afterHeight + 2);
            }
            else
            {
                p.Offset(0, -Height - 5);
            }

            // Move leftward if not enough screen space
            int leftPos = Math.Max(0, Parent.Width - Width - 15);
            if (p.X > leftPos)
            {
                p.Offset(leftPos - p.X, 0);
            }

            if (!Location.Equals(p))
            {
                Location = p;
            }
            if (!Visible)
            {
                Show();
            }
            BringToFront();
        }

        private void TipBox_Hide(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
