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
        private readonly SoapstoneMetadata Metadata;
        private string game;
        private object data;

        public ToolControl(SoapstoneMetadata Metadata)
        {
            InitializeComponent();
            this.Metadata = Metadata;
        }

        public void SetText(string s, string game = null, object data = null)
        {
            this.game = game;
            this.data = data;
            tipBox.Text = s;

            int width = tipBox.Lines.Max(e => e.Length) * tipBox.CharWidth;
            width += Padding.Horizontal + tipPanel.Padding.Horizontal + 2;

            int height = (tipBox.CharHeight + tipBox.LineInterval) * tipBox.LinesCount;
            height += Padding.Vertical + tipPanel.Padding.Vertical + 2;

            Size = new Size(width, height);
        }

        public void ShowAtPosition(Control origin, Point p, int afterHeight)
        {
            if (Parent == null || origin.IsDisposed)
            {
                // This should be added to the parent before the editor uses it,
                // or after editor tab is closed, but not really show-stopping if not.
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

        private void TipBox_MouseDown(object sender, MouseEventArgs e)
        {
            // For now, only allow right-click, because left-click with window switch causes accidental selection
            if (game != null && e.Button == MouseButtons.Right)
            {
                if (data is SoapstoneMetadata.EntityData entity && entity.Type != "Self")
                {
                    _ = Metadata.OpenEntityData(game, entity);
                }
                else if (data is SoapstoneMetadata.EntryData entry)
                {
                    _ = Metadata.OpenEntryData(game, entry);
                }
            }
            Hide();
        }
    }
}
