using System.Windows.Forms;
using FastColoredTextBoxNS;

namespace DarkScript3
{
    partial class PreviewCompilationForm
    {

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PreviewCompilationForm));
            ofdFile = new OpenFileDialog();
            fctb1 = new FastColoredTextBox();
            fctb2 = new FastColoredTextBox();
            splitContainer1 = new SplitContainer();
            action = new Button();
            lineup = new CheckBox();
            warningL = new Label();
            ((System.ComponentModel.ISupportInitialize)fctb1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)fctb2).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            SuspendLayout();
            // 
            // fctb1
            // 
            fctb1.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            fctb1.AutoScrollMinSize = new System.Drawing.Size(27, 14);
            fctb1.BackBrush = null;
            fctb1.CaretVisible = false;
            fctb1.CharHeight = 14;
            fctb1.CharWidth = 8;
            fctb1.Cursor = Cursors.IBeam;
            fctb1.DisabledColor = System.Drawing.Color.FromArgb(100, 180, 180, 180);
            fctb1.Dock = DockStyle.Fill;
            fctb1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            fctb1.IsReplaceMode = false;
            fctb1.Location = new System.Drawing.Point(0, 0);
            fctb1.Margin = new Padding(4, 3, 4, 3);
            fctb1.Name = "fctb1";
            fctb1.Paddings = new Padding(0);
            fctb1.ReadOnly = true;
            fctb1.SelectionColor = System.Drawing.Color.FromArgb(60, 0, 0, 255);
            fctb1.ServiceColors = null;
            fctb1.Size = new System.Drawing.Size(489, 588);
            fctb1.TabIndex = 26;
            fctb1.Zoom = 100;
            fctb1.VisibleRangeChanged += fctb_VisibleRangeChanged;
            fctb1.ZoomChanged += fctb1_ZoomChanged;
            // 
            // fctb2
            // 
            fctb2.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            fctb2.AutoScrollMinSize = new System.Drawing.Size(2, 14);
            fctb2.BackBrush = null;
            fctb2.CaretVisible = false;
            fctb2.CharHeight = 14;
            fctb2.CharWidth = 8;
            fctb2.Cursor = Cursors.IBeam;
            fctb2.DisabledColor = System.Drawing.Color.FromArgb(100, 180, 180, 180);
            fctb2.Dock = DockStyle.Fill;
            fctb2.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            fctb2.IsReplaceMode = false;
            fctb2.Location = new System.Drawing.Point(0, 0);
            fctb2.Margin = new Padding(4, 3, 4, 3);
            fctb2.Name = "fctb2";
            fctb2.Paddings = new Padding(0);
            fctb2.ReadOnly = true;
            fctb2.SelectionColor = System.Drawing.Color.FromArgb(60, 0, 0, 255);
            fctb2.ServiceColors = null;
            fctb2.ShowLineNumbers = false;
            fctb2.Size = new System.Drawing.Size(509, 588);
            fctb2.TabIndex = 27;
            fctb2.Zoom = 100;
            fctb2.VisibleRangeChanged += fctb_VisibleRangeChanged;
            fctb2.ZoomChanged += fctb2_ZoomChanged;
            // 
            // splitContainer1
            // 
            splitContainer1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            splitContainer1.Location = new System.Drawing.Point(14, 14);
            splitContainer1.Margin = new Padding(4, 3, 4, 3);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(fctb1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(fctb2);
            splitContainer1.Size = new System.Drawing.Size(1003, 588);
            splitContainer1.SplitterDistance = 489;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 28;
            // 
            // action
            // 
            action.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            action.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            action.Location = new System.Drawing.Point(904, 612);
            action.Margin = new Padding(4, 3, 4, 3);
            action.Name = "action";
            action.Size = new System.Drawing.Size(113, 35);
            action.TabIndex = 30;
            action.Text = "Convert";
            action.UseVisualStyleBackColor = true;
            action.Click += action_Click;
            // 
            // lineup
            // 
            lineup.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lineup.AutoSize = true;
            lineup.Checked = true;
            lineup.CheckState = CheckState.Checked;
            lineup.Location = new System.Drawing.Point(31, 619);
            lineup.Margin = new Padding(4, 3, 4, 3);
            lineup.Name = "lineup";
            lineup.Size = new System.Drawing.Size(300, 19);
            lineup.TabIndex = 31;
            lineup.Text = "Insert ↪ lines to line up the diff (in the preview only)";
            lineup.UseVisualStyleBackColor = true;
            lineup.CheckedChanged += lineup_CheckedChanged;
            // 
            // warningL
            // 
            warningL.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            warningL.BackColor = System.Drawing.Color.Transparent;
            warningL.ForeColor = System.Drawing.Color.Firebrick;
            warningL.Location = new System.Drawing.Point(713, 620);
            warningL.Name = "warningL";
            warningL.Size = new System.Drawing.Size(184, 18);
            warningL.TabIndex = 32;
            warningL.Text = "No warnings produced";
            warningL.TextAlign = System.Drawing.ContentAlignment.TopRight;
            warningL.Visible = false;
            // 
            // PreviewCompilationForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1031, 652);
            Controls.Add(warningL);
            Controls.Add(lineup);
            Controls.Add(action);
            Controls.Add(splitContainer1);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Margin = new Padding(4, 3, 4, 3);
            Name = "PreviewCompilationForm";
            Text = "Compilation Output Preview";
            ((System.ComponentModel.ISupportInitialize)fctb1).EndInit();
            ((System.ComponentModel.ISupportInitialize)fctb2).EndInit();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private System.Windows.Forms.OpenFileDialog ofdFile;
        private FastColoredTextBoxNS.FastColoredTextBox fctb1;
        private FastColoredTextBoxNS.FastColoredTextBox fctb2;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private Button action;
        private CheckBox lineup;
        private Label warningL;
    }
}