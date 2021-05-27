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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PreviewCompilationForm));
            this.ofdFile = new System.Windows.Forms.OpenFileDialog();
            this.fctb1 = new FastColoredTextBoxNS.FastColoredTextBox();
            this.fctb2 = new FastColoredTextBoxNS.FastColoredTextBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.action = new System.Windows.Forms.Button();
            this.lineup = new System.Windows.Forms.CheckBox();
            ((System.ComponentModel.ISupportInitialize)(this.fctb1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.fctb2)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // fctb1
            // 
            this.fctb1.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.fctb1.AutoScrollMinSize = new System.Drawing.Size(27, 14);
            this.fctb1.BackBrush = null;
            this.fctb1.CaretVisible = false;
            this.fctb1.CharHeight = 14;
            this.fctb1.CharWidth = 8;
            this.fctb1.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.fctb1.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.fctb1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fctb1.Font = new System.Drawing.Font("Courier New", 9.75F);
            this.fctb1.IsReplaceMode = false;
            this.fctb1.Location = new System.Drawing.Point(0, 0);
            this.fctb1.Name = "fctb1";
            this.fctb1.Paddings = new System.Windows.Forms.Padding(0);
            this.fctb1.ReadOnly = true;
            this.fctb1.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.fctb1.ServiceColors = null;
            this.fctb1.Size = new System.Drawing.Size(420, 510);
            this.fctb1.TabIndex = 26;
            this.fctb1.Zoom = 100;
            this.fctb1.VisibleRangeChanged += new System.EventHandler(this.fctb_VisibleRangeChanged);
            this.fctb1.ZoomChanged += new System.EventHandler(this.fctb1_ZoomChanged);
            // 
            // fctb2
            // 
            this.fctb2.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.fctb2.AutoScrollMinSize = new System.Drawing.Size(2, 14);
            this.fctb2.BackBrush = null;
            this.fctb2.CaretVisible = false;
            this.fctb2.CharHeight = 14;
            this.fctb2.CharWidth = 8;
            this.fctb2.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.fctb2.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.fctb2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fctb2.Font = new System.Drawing.Font("Courier New", 9.75F);
            this.fctb2.IsReplaceMode = false;
            this.fctb2.Location = new System.Drawing.Point(0, 0);
            this.fctb2.Name = "fctb2";
            this.fctb2.Paddings = new System.Windows.Forms.Padding(0);
            this.fctb2.ReadOnly = true;
            this.fctb2.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.fctb2.ServiceColors = null;
            this.fctb2.ShowLineNumbers = false;
            this.fctb2.Size = new System.Drawing.Size(436, 510);
            this.fctb2.TabIndex = 27;
            this.fctb2.Zoom = 100;
            this.fctb2.VisibleRangeChanged += new System.EventHandler(this.fctb_VisibleRangeChanged);
            this.fctb2.ZoomChanged += new System.EventHandler(this.fctb2_ZoomChanged);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.splitContainer1.Location = new System.Drawing.Point(12, 12);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.fctb1);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.fctb2);
            this.splitContainer1.Size = new System.Drawing.Size(860, 510);
            this.splitContainer1.SplitterDistance = 420;
            this.splitContainer1.TabIndex = 28;
            // 
            // action
            // 
            this.action.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.action.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.action.Location = new System.Drawing.Point(775, 530);
            this.action.Name = "action";
            this.action.Size = new System.Drawing.Size(97, 30);
            this.action.TabIndex = 30;
            this.action.Text = "Convert";
            this.action.UseVisualStyleBackColor = true;
            this.action.Click += new System.EventHandler(this.action_Click);
            // 
            // lineup
            // 
            this.lineup.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lineup.AutoSize = true;
            this.lineup.Checked = true;
            this.lineup.CheckState = System.Windows.Forms.CheckState.Checked;
            this.lineup.Location = new System.Drawing.Point(27, 538);
            this.lineup.Name = "lineup";
            this.lineup.Size = new System.Drawing.Size(266, 17);
            this.lineup.TabIndex = 31;
            this.lineup.Text = "Insert ↪ lines to line up the diff (in the preview only)";
            this.lineup.UseVisualStyleBackColor = true;
            this.lineup.CheckedChanged += new System.EventHandler(this.lineup_CheckedChanged);
            // 
            // PreviewCompilationForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(884, 565);
            this.Controls.Add(this.lineup);
            this.Controls.Add(this.action);
            this.Controls.Add(this.splitContainer1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "PreviewCompilationForm";
            this.Text = "Compilation Output Preview";
            ((System.ComponentModel.ISupportInitialize)(this.fctb1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.fctb2)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.OpenFileDialog ofdFile;
        private FastColoredTextBoxNS.FastColoredTextBox fctb1;
        private FastColoredTextBoxNS.FastColoredTextBox fctb2;
        private System.Windows.Forms.SplitContainer splitContainer1;
        private Button action;
        private CheckBox lineup;
    }
}