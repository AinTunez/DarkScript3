using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace DarkScript3
{
    public partial class GameChooser : Form
    {
        public string GameDocs = "";
        public bool Fancy { get => fancy.Checked; }

        public GameChooser(bool showFancy)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            if (showFancy)
            {

                fancy.Checked = Properties.Settings.Default.DefaultFancyDecompile;
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

        private void Ds3Btn_Click(object sender, EventArgs e)
        {
            GameDocs = "ds3-common.emedf.json";
            Close();
        }

        private void BbBtn_Click(object sender, EventArgs e)
        {
            GameDocs = "bb-common.emedf.json";
            Close();
        }

        private void Ds1Btn_Click(object sender, EventArgs e)
        {
            GameDocs = "ds1-common.emedf.json";
            Close();
        }

        private void SekiroBtn_Click(object sender, EventArgs e)
        {
            GameDocs = "sekiro-common.emedf.json"; 
            Close();
        }

        private void ds2Btn_Click(object sender, EventArgs e)
        {
            GameDocs = "ds2-common.emedf.json";
            Close();
        }

        private void ds2scholarBtn_Click(object sender, EventArgs e)
        {
            GameDocs = "ds2scholar-common.emedf.json";
            Close();
        }

        private void GameChooser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                GameDocs = null;
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
                GameDocs = Path.GetFileName(ofd.FileName);
            }
            Close();
        }

        private void fancy_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DefaultFancyDecompile = fancy.Checked;
            Properties.Settings.Default.Save();
        }
    }
}
