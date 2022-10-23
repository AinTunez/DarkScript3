using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DarkScript3
{
    public partial class ScrollDialog : Form
    {
        public ScrollDialog(string title, string text)
        {
            InitializeComponent();
            Text = title;
            textbox.Text = text;
        }

        public static DialogResult Show(Form parent, string text, string title = "")
        {
            using (ScrollDialog form = new ScrollDialog(title, text))
            {
                form.Icon = parent.Icon;
                form.StartPosition = FormStartPosition.CenterParent;
                return form.ShowDialog(parent);
            }
        }

        private void act_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void ScrollDialog_Shown(object sender, EventArgs e)
        {
            act.Focus();
        }
    }
}
