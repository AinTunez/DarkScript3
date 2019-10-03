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
    public partial class CustomToolTip : Form
    {
        private Control FocusControl = new Control();

        public CustomToolTip()
        {
            InitializeComponent();
        }

        public CustomToolTip(Control c)
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
            Size = new Size(width, height);
        }

        private void TipBox_Click(object sender, EventArgs e) => FocusControl.Focus();

        private void TipBox_SelectionChanged(object sender, EventArgs e) => FocusControl.Focus();
    }
}
