using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using System.IO;

namespace DarkScript3
{
    public partial class GameChooser : Form
    {
        public string GameDocs = "";

        public GameChooser()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
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
    }
}
