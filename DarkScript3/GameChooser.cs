using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace DarkScript3
{
    public partial class GameChooser : Form
    {
        private bool showFancy;
        public string GameDocs { get; private set; }
        public bool Fancy { get; set; }

        public GameChooser(bool showFancy)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            this.showFancy = showFancy;
            if (showFancy)
            {

                fancy.Checked = Properties.Settings.Default.DefaultFancy;
            }
            else
            {
                // Hide everything below the fold by just hiding them and roughly cutting the window height
                Point windowLoc = PointToClient(fancy.Parent.PointToScreen(fancy.Location));
                fancy.Hide();
                fancyLabel.Hide();
                Height = windowLoc.Y + 7;
            }
        }

        private void SetResult(string docs)
        {
            GameDocs = docs;
            Fancy = showFancy && fancy.Checked;
        }

        private void Ds3Btn_Click(object sender, EventArgs e)
        {
            SetResult("ds3-common.emedf.json");
            Close();
        }

        private void BbBtn_Click(object sender, EventArgs e)
        {
            SetResult("bb-common.emedf.json");
            Close();
        }

        private void Ds1Btn_Click(object sender, EventArgs e)
        {
            SetResult("ds1-common.emedf.json");
            Close();
        }

        private void SekiroBtn_Click(object sender, EventArgs e)
        {
            SetResult("sekiro-common.emedf.json");
            Close();
        }

        private void ds2Btn_Click(object sender, EventArgs e)
        {
            SetResult("ds2-common.emedf.json");
            Close();
        }

        private void ds2scholarBtn_Click(object sender, EventArgs e)
        {
            SetResult("ds2scholar-common.emedf.json");
            Close();
        }

        private void eldenBtn_Click(object sender, EventArgs e)
        {
            SetResult("er-common.emedf.json");
            Close();
        }

        private void GameChooser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        private void customBtn_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.InitialDirectory = "Resources";
            ofd.Filter = "EMEDF Files|*.emedf.json";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                SetResult(ofd.FileName);
                Close();
            }
        }

        private void fancy_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DefaultFancy = fancy.Checked;
            Properties.Settings.Default.Save();
        }
    }
}
