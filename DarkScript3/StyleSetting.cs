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
    public partial class StyleSetting : UserControl
    {
        public Color Color
        {
            get => colorBox.BackColor;
            set => colorBox.BackColor = value;
        }

        public override string Text
        {
            get
            {
                return textLabel.Text;
            }
            set
            {
                textLabel.Text = value ?? "placeholder";
            }
        }

        public StyleSetting()
        {
            InitializeComponent();
        }
        private void colorBox_Click(object sender, EventArgs e)
        {
            ColorDialog picker = new ColorDialog();
            picker.Color = Color;
            picker.FullOpen = true;
            if (picker.ShowDialog() == DialogResult.OK)
                Color = picker.Color;
        }

        public void Init(string s, Brush b) => Init(s, (b as SolidBrush).Color);

        public void Init(string s, Color c)
        {
            Text = s;
            Color = c;
        }
    }
}
