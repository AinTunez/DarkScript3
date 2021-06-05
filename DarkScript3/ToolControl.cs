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

        private void SetFocus()
        {
            // TODO: Determine if *all* interaction can be disabled for this control.
            /*if (BFF.Visible)
            {
                BFF.Focus();
                Hide();
            } else
            {
                FocusControl.Focus();
            }*/
        }

        private void TipBox_Click(object sender, EventArgs e)
        {
            SetFocus();
        }

        private void TipBox_SelectionChanged(object sender, EventArgs e)
        {
            SetFocus();
        }
    }
}
