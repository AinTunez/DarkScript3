
namespace DarkScript3
{
    partial class ScriptSettingsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ScriptSettingsForm));
            this.ds1r = new System.Windows.Forms.CheckBox();
            this.preprocess = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // ds1r
            // 
            this.ds1r.AutoSize = true;
            this.ds1r.Location = new System.Drawing.Point(13, 13);
            this.ds1r.Name = "ds1r";
            this.ds1r.Size = new System.Drawing.Size(357, 17);
            this.ds1r.TabIndex = 0;
            this.ds1r.Text = "Dark Souls 1 Remastered (compilation can use more condition groups)";
            this.ds1r.UseVisualStyleBackColor = true;
            this.ds1r.CheckedChanged += new System.EventHandler(this.ds1r_CheckedChanged);
            // 
            // preprocess
            // 
            this.preprocess.AutoSize = true;
            this.preprocess.Location = new System.Drawing.Point(13, 37);
            this.preprocess.Name = "preprocess";
            this.preprocess.Size = new System.Drawing.Size(298, 17);
            this.preprocess.TabIndex = 1;
            this.preprocess.Text = "Allow JS preprocessing, including compilation with $Event";
            this.preprocess.UseVisualStyleBackColor = true;
            this.preprocess.CheckedChanged += new System.EventHandler(this.preprocess_CheckedChanged);
            // 
            // ScriptSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(382, 65);
            this.Controls.Add(this.preprocess);
            this.Controls.Add(this.ds1r);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.Name = "ScriptSettingsForm";
            this.Text = "Additional script settings";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ScriptSettingsForm_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox ds1r;
        private System.Windows.Forms.CheckBox preprocess;
    }
}