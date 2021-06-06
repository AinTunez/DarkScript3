namespace DarkScript3
{
    partial class ToolControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.tipPanel = new System.Windows.Forms.Panel();
            this.tipBox = new FastColoredTextBoxNS.FastColoredTextBox();
            this.tipPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tipBox)).BeginInit();
            this.SuspendLayout();
            // 
            // tipPanel
            // 
            this.tipPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.tipPanel.Controls.Add(this.tipBox);
            this.tipPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tipPanel.Location = new System.Drawing.Point(0, 0);
            this.tipPanel.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tipPanel.Name = "tipPanel";
            this.tipPanel.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.tipPanel.Size = new System.Drawing.Size(111, 120);
            this.tipPanel.TabIndex = 2;
            // 
            // tipBox
            // 
            this.tipBox.AllowSeveralTextStyleDrawing = true;
            this.tipBox.AutoCompleteBracketsList = new char[] {
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
            this.tipBox.AutoScrollMinSize = new System.Drawing.Size(80, 12);
            this.tipBox.AutoSize = true;
            this.tipBox.BackBrush = null;
            this.tipBox.BackColor = System.Drawing.Color.Transparent;
            this.tipBox.CharHeight = 12;
            this.tipBox.CharWidth = 6;
            this.tipBox.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.tipBox.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.tipBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tipBox.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.tipBox.ForeColor = System.Drawing.Color.Gainsboro;
            this.tipBox.IsReplaceMode = false;
            this.tipBox.Location = new System.Drawing.Point(4, 4);
            this.tipBox.Margin = new System.Windows.Forms.Padding(0);
            this.tipBox.Name = "tipBox";
            this.tipBox.Paddings = new System.Windows.Forms.Padding(0);
            this.tipBox.ReadOnly = true;
            this.tipBox.SelectionColor = System.Drawing.Color.Transparent;
            this.tipBox.ServiceColors = null;
            this.tipBox.ShowLineNumbers = false;
            this.tipBox.ShowScrollBars = false;
            this.tipBox.Size = new System.Drawing.Size(103, 112);
            this.tipBox.TabIndex = 0;
            this.tipBox.Text = "customToolTip";
            this.tipBox.Zoom = 100;
            this.tipBox.SelectionChanged += new System.EventHandler(this.TipBox_Hide);
            this.tipBox.MouseDown += new System.Windows.Forms.MouseEventHandler(this.TipBox_Hide);
            this.tipBox.Scroll += new System.Windows.Forms.ScrollEventHandler(this.TipBox_Hide);
            // 
            // ToolControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.tipPanel);
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.Name = "ToolControl";
            this.Size = new System.Drawing.Size(111, 120);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(this.TipBox_Hide);
            this.Scroll += new System.Windows.Forms.ScrollEventHandler(this.TipBox_Hide);
            this.tipPanel.ResumeLayout(false);
            this.tipPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tipBox)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel tipPanel;
        public FastColoredTextBoxNS.FastColoredTextBox tipBox;
    }
}
