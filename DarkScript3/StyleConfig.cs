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
    public partial class StyleConfig : Form
    {
        public Font FontSetting { get; set; }

        public StyleConfig(GUI ds3, Font font)
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            commentSetting.Init("Comment", GUI.TextStyles.Comment.ForeBrush);
            stringSetting.Init("String", GUI.TextStyles.String.ForeBrush);
            keywordSetting.Init("Keyword", GUI.TextStyles.Keyword.ForeBrush);
            ttKeywordSetting.Init("Keyword (tooltips)", GUI.TextStyles.ToolTipKeyword.ForeBrush);
            enumPropSetting.Init("Enum Property", GUI.TextStyles.EnumProperty.ForeBrush);
            globalConstSetting.Init("Global Enum Constant", GUI.TextStyles.EnumConstant.ForeBrush);
            numberSetting.Init("Number", GUI.TextStyles.Number.ForeBrush);
            toolTipEnumType.Init("Enum Type", GUI.TextStyles.EnumType.ForeBrush);
            backgroundSetting.Init("Background", ds3.editor.BackColor);
            highlightSetting.Init("Highlight", ds3.editor.SelectionColor);
            highlightBoxSetting.Init("Highlight Box", GUI.TextStyles.HighlightToken.BorderPen.Brush);
            plainSetting.Init("Default Text", ds3.editor.ForeColor);
            FontSetting = font;
            selectFont.Font = font;
        }

        private void StyleConfig_Load(object sender, EventArgs e)
        {
            
        }

        private void okBtn_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void cancelBtn_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void selectFont_Click(object sender, EventArgs e)
        {
            FontDialog dialog = new FontDialog();
            dialog.Font = FontSetting;
            dialog.FontMustExist = true;
            dialog.ShowDialog();
            FontSetting = dialog.Font;
            selectFont.Font = FontSetting;
        }
    }
}
