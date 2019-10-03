namespace DarkScript3
{
    partial class CustomToolTip
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
            this.tipBox = new FastColoredTextBoxNS.FastColoredTextBox();
            this.tipPanel = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.tipBox)).BeginInit();
            this.tipPanel.SuspendLayout();
            this.SuspendLayout();
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
            this.tipBox.AutoScrollMinSize = new System.Drawing.Size(106, 16);
            this.tipBox.AutoSize = true;
            this.tipBox.BackBrush = null;
            this.tipBox.BackColor = System.Drawing.Color.Transparent;
            this.tipBox.CharHeight = 16;
            this.tipBox.CharWidth = 8;
            this.tipBox.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.tipBox.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.tipBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tipBox.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.tipBox.ForeColor = System.Drawing.Color.Gainsboro;
            this.tipBox.IsReplaceMode = false;
            this.tipBox.Location = new System.Drawing.Point(5, 5);
            this.tipBox.Margin = new System.Windows.Forms.Padding(0);
            this.tipBox.Name = "tipBox";
            this.tipBox.Paddings = new System.Windows.Forms.Padding(0);
            this.tipBox.ReadOnly = true;
            this.tipBox.SelectionColor = System.Drawing.Color.Transparent;
            this.tipBox.ServiceColors = null;
            this.tipBox.ShowLineNumbers = false;
            this.tipBox.ShowScrollBars = false;
            this.tipBox.Size = new System.Drawing.Size(399, 175);
            this.tipBox.TabIndex = 0;
            this.tipBox.Text = "customToolTip";
            this.tipBox.Zoom = 100;
            this.tipBox.SelectionChanged += new System.EventHandler(this.TipBox_SelectionChanged);
            this.tipBox.Click += new System.EventHandler(this.TipBox_Click);
            // 
            // tipPanel
            // 
            this.tipPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.tipPanel.Controls.Add(this.tipBox);
            this.tipPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tipPanel.Location = new System.Drawing.Point(1, 1);
            this.tipPanel.Name = "tipPanel";
            this.tipPanel.Padding = new System.Windows.Forms.Padding(5);
            this.tipPanel.Size = new System.Drawing.Size(409, 185);
            this.tipPanel.TabIndex = 1;
            // 
            // CustomToolTip
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(411, 187);
            this.Controls.Add(this.tipPanel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "CustomToolTip";
            this.Padding = new System.Windows.Forms.Padding(1);
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this.tipBox)).EndInit();
            this.tipPanel.ResumeLayout(false);
            this.tipPanel.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        public FastColoredTextBoxNS.FastColoredTextBox tipBox;
        private System.Windows.Forms.Panel tipPanel;
    }
}