using SoulsFormats;
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
    public partial class InfoViewer : Form
    {
        public InfoViewer(EventScripter scripter)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            BinaryReaderEx reader = new BinaryReaderEx(false, scripter.EVD.StringData);

            Dictionary<long, string> stringPositions = new Dictionary<long, string>();

            while (reader.Position < scripter.EVD.StringData.Length)
            {
                long pos = reader.Position;
                string str = reader.ReadUTF16();
                string display = $"{pos}: {str}";

                bool isLinkedFile = scripter.EVD.LinkedFileOffsets.Contains(pos);
                if (isLinkedFile) display += "*";

                stringBox.Items.Add(display);

            }

        }

        private void CloseBtn_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
