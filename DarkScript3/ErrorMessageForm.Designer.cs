
namespace DarkScript3
{
    partial class ErrorMessageForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ErrorMessageForm));
            this.box = new FastColoredTextBoxNS.FastColoredTextBox();
            this.button = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.box)).BeginInit();
            this.SuspendLayout();
            // 
            // box
            // 
            this.box.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.box.AutoCompleteBracketsList = new char[] {
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
            this.box.AutoScrollMinSize = new System.Drawing.Size(0, 14);
            this.box.BackBrush = null;
            this.box.BackColor = System.Drawing.SystemColors.Control;
            this.box.CharHeight = 14;
            this.box.CharWidth = 8;
            this.box.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.box.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.box.Font = new System.Drawing.Font("Courier New", 9.75F);
            this.box.IsReplaceMode = false;
            this.box.Location = new System.Drawing.Point(9, 9);
            this.box.Margin = new System.Windows.Forms.Padding(0);
            this.box.Name = "box";
            this.box.Paddings = new System.Windows.Forms.Padding(0);
            this.box.ReadOnly = true;
            this.box.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.box.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("box.ServiceColors")));
            this.box.ShowLineNumbers = false;
            this.box.ShowScrollBars = false;
            this.box.Size = new System.Drawing.Size(566, 214);
            this.box.TabIndex = 0;
            this.box.WordWrap = true;
            this.box.WordWrapAutoIndent = false;
            this.box.Zoom = 100;
            this.box.MouseMove += new System.Windows.Forms.MouseEventHandler(this.box_MouseMove);
            this.box.MouseUp += new System.Windows.Forms.MouseEventHandler(this.box_MouseDown);
            // 
            // button
            // 
            this.button.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button.Location = new System.Drawing.Point(497, 226);
            this.button.Name = "button";
            this.button.Size = new System.Drawing.Size(75, 23);
            this.button.TabIndex = 1;
            this.button.Text = "OK";
            this.button.UseVisualStyleBackColor = true;
            this.button.Click += new System.EventHandler(this.button_Click);
            // 
            // ErrorMessageForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 261);
            this.Controls.Add(this.button);
            this.Controls.Add(this.box);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "ErrorMessageForm";
            this.Text = "Error";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ErrorMessageForm_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.box)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private FastColoredTextBoxNS.FastColoredTextBox box;
        private System.Windows.Forms.Button button;
    }
}