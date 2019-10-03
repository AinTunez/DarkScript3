using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DarkScript3
{
    public partial class ToolControl : UserControl
    {

        private Control FocusControl = new Control();

        public ToolControl()
        {
            InitializeComponent();
        }

        public ToolControl(Control c)
        {
            InitializeComponent();
            FocusControl = c;
        }

        public void SetText(string s)
        {
            tipBox.Font = FocusControl.Font;
            tipBox.Text = s;
            Point start = tipBox.PlaceToPoint(tipBox.Range.Start);
            Point end = tipBox.PlaceToPoint(tipBox.Range.End);
            int width = end.X - start.X + Padding.Horizontal + tipPanel.Padding.Horizontal;
            int height = tipBox.CharHeight + tipBox.LineInterval;
            height *= tipBox.LinesCount;
            height += Padding.Vertical + tipPanel.Padding.Vertical;
            height += 2;
            Size = new Size(width, height);
        }

        private void TipBox_Click(object sender, EventArgs e)
        {
            FocusControl.Focus();
        }

        private void TipBox_SelectionChanged(object sender, EventArgs e)
        {
            FocusControl.Focus();
        }
    }
}
