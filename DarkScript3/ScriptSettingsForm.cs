using System;
using System.Windows.Forms;

namespace DarkScript3
{
    public partial class ScriptSettingsForm : Form
    {
        private ScriptSettings settings;
        private bool dataInitialized;

        public ScriptSettingsForm(ScriptSettings settings)
        {
            InitializeComponent();
            this.settings = settings;
            ds1r.Checked = settings.IsRemastered;
            ds1r.Enabled = settings.IsRemasteredSettable;
            preprocess.Checked = settings.AllowPreprocess;
            preprocess.Enabled = settings.AllowPreprocessSettable;
            dataInitialized = true;
        }

        private void ds1r_CheckedChanged(object sender, EventArgs e)
        {
            if (!dataInitialized) return;
            settings.IsRemastered = ds1r.Checked;
        }

        private void preprocess_CheckedChanged(object sender, EventArgs e)
        {
            if (!dataInitialized) return;
            settings.AllowPreprocess = preprocess.Checked;
        }

        private void ScriptSettingsForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }
    }
}
