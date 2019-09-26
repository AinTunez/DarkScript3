namespace DarkScript3
{
    partial class InfoViewer
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.linkedFilesBox = new System.Windows.Forms.ListBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.stringBox = new System.Windows.Forms.ListBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.linkedFilesBox);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(391, 137);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Linked Files";
            // 
            // linkedFilesBox
            // 
            this.linkedFilesBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.linkedFilesBox.FormattingEnabled = true;
            this.linkedFilesBox.ItemHeight = 16;
            this.linkedFilesBox.Location = new System.Drawing.Point(3, 18);
            this.linkedFilesBox.Name = "linkedFilesBox";
            this.linkedFilesBox.Size = new System.Drawing.Size(385, 116);
            this.linkedFilesBox.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.stringBox);
            this.groupBox2.Location = new System.Drawing.Point(12, 155);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(394, 272);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Strings";
            // 
            // stringBox
            // 
            this.stringBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.stringBox.FormattingEnabled = true;
            this.stringBox.ItemHeight = 16;
            this.stringBox.Location = new System.Drawing.Point(3, 18);
            this.stringBox.Name = "stringBox";
            this.stringBox.Size = new System.Drawing.Size(388, 251);
            this.stringBox.TabIndex = 0;
            // 
            // InfoViewer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(415, 439);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "InfoViewer";
            this.Text = "InfoViewer";
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.ListBox linkedFilesBox;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ListBox stringBox;
    }
}