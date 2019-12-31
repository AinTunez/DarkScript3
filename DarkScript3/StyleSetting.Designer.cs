namespace DarkScript3
{
    partial class StyleSetting
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
            this.colorBox = new System.Windows.Forms.Panel();
            this.textLabel = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // colorBox
            // 
            this.colorBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.colorBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.colorBox.Location = new System.Drawing.Point(129, 3);
            this.colorBox.Name = "colorBox";
            this.colorBox.Size = new System.Drawing.Size(118, 23);
            this.colorBox.TabIndex = 1;
            this.colorBox.Click += new System.EventHandler(this.colorBox_Click);
            // 
            // textLabel
            // 
            this.textLabel.Location = new System.Drawing.Point(3, 3);
            this.textLabel.Name = "textLabel";
            this.textLabel.Size = new System.Drawing.Size(120, 23);
            this.textLabel.TabIndex = 2;
            this.textLabel.Text = "label1";
            this.textLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // StyleSetting
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.textLabel);
            this.Controls.Add(this.colorBox);
            this.Name = "StyleSetting";
            this.Size = new System.Drawing.Size(250, 31);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Panel colorBox;
        private System.Windows.Forms.Label textLabel;
    }
}
