using System;
using System.Drawing;
using System.Windows.Forms;

namespace DarkScript3
{
    public partial class StyleConfig : Form
    {
        public Font FontSetting { get; set; }

        public StyleConfig()
        {
            InitializeComponent();
            StartPosition = FormStartPosition.CenterParent;
            commentSetting.Init("Comment", TextStyles.Comment.ForeBrush);
            stringSetting.Init("String", TextStyles.String.ForeBrush);
            keywordSetting.Init("Keyword", TextStyles.Keyword.ForeBrush);
            ttKeywordSetting.Init("Keyword (tooltips)", TextStyles.ToolTipKeyword.ForeBrush);
            enumPropSetting.Init("Enum Property", TextStyles.EnumProperty.ForeBrush);
            globalConstSetting.Init("Global Enum Constant", TextStyles.EnumConstant.ForeBrush);
            numberSetting.Init("Number", TextStyles.Number.ForeBrush);
            toolTipEnumType.Init("Enum Type", TextStyles.EnumType.ForeBrush);
            backgroundSetting.Init("Background", TextStyles.BackColor);
            highlightSetting.Init("Highlight", TextStyles.SelectionColor);
            highlightBoxSetting.Init("Highlight Box", TextStyles.HighlightToken.BorderPen.Brush);
            plainSetting.Init("Default Text", TextStyles.ForeColor);
            selectFont.Font = FontSetting = (Font)TextStyles.Font.Clone();
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
