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
        }
    }
}
