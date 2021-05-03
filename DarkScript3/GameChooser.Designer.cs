namespace DarkScript3
{
    partial class GameChooser
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
            this.ds1Btn = new System.Windows.Forms.Button();
            this.bbBtn = new System.Windows.Forms.Button();
            this.ds3Btn = new System.Windows.Forms.Button();
            this.sekiroBtn = new System.Windows.Forms.Button();
            this.ds2Btn = new System.Windows.Forms.Button();
            this.ds2scholarBtn = new System.Windows.Forms.Button();
            this.fancy = new System.Windows.Forms.CheckBox();
            this.fancyLabel = new System.Windows.Forms.Label();
            this.customBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ds1Btn
            // 
            this.ds1Btn.Location = new System.Drawing.Point(12, 12);
            this.ds1Btn.Name = "ds1Btn";
            this.ds1Btn.Size = new System.Drawing.Size(125, 23);
            this.ds1Btn.TabIndex = 0;
            this.ds1Btn.Text = "Dark Souls";
            this.ds1Btn.UseVisualStyleBackColor = true;
            this.ds1Btn.Click += new System.EventHandler(this.Ds1Btn_Click);
            // 
            // bbBtn
            // 
            this.bbBtn.Location = new System.Drawing.Point(12, 41);
            this.bbBtn.Name = "bbBtn";
            this.bbBtn.Size = new System.Drawing.Size(125, 23);
            this.bbBtn.TabIndex = 1;
            this.bbBtn.Text = "Bloodborne";
            this.bbBtn.UseVisualStyleBackColor = true;
            this.bbBtn.Click += new System.EventHandler(this.BbBtn_Click);
            // 
            // ds3Btn
            // 
            this.ds3Btn.Location = new System.Drawing.Point(12, 127);
            this.ds3Btn.Name = "ds3Btn";
            this.ds3Btn.Size = new System.Drawing.Size(125, 23);
            this.ds3Btn.TabIndex = 2;
            this.ds3Btn.Text = "Dark Souls III";
            this.ds3Btn.UseVisualStyleBackColor = true;
            this.ds3Btn.Click += new System.EventHandler(this.Ds3Btn_Click);
            // 
            // sekiroBtn
            // 
            this.sekiroBtn.Location = new System.Drawing.Point(12, 156);
            this.sekiroBtn.Name = "sekiroBtn";
            this.sekiroBtn.Size = new System.Drawing.Size(125, 23);
            this.sekiroBtn.TabIndex = 3;
            this.sekiroBtn.Text = "Sekiro";
            this.sekiroBtn.UseVisualStyleBackColor = true;
            this.sekiroBtn.Click += new System.EventHandler(this.SekiroBtn_Click);
            // 
            // ds2Btn
            // 
            this.ds2Btn.Location = new System.Drawing.Point(12, 70);
            this.ds2Btn.Name = "ds2Btn";
            this.ds2Btn.Size = new System.Drawing.Size(125, 23);
            this.ds2Btn.TabIndex = 4;
            this.ds2Btn.Text = "Dark Souls II";
            this.ds2Btn.UseVisualStyleBackColor = true;
            this.ds2Btn.Click += new System.EventHandler(this.ds2Btn_Click);
            // 
            // ds2scholarBtn
            // 
            this.ds2scholarBtn.Location = new System.Drawing.Point(12, 99);
            this.ds2scholarBtn.Name = "ds2scholarBtn";
            this.ds2scholarBtn.Size = new System.Drawing.Size(125, 23);
            this.ds2scholarBtn.TabIndex = 5;
            this.ds2scholarBtn.Text = "Dark Souls II SOTFS";
            this.ds2scholarBtn.UseVisualStyleBackColor = true;
            this.ds2scholarBtn.Click += new System.EventHandler(this.ds2scholarBtn_Click);
            // 
            // fancy
            // 
            this.fancy.AutoSize = true;
            this.fancy.Location = new System.Drawing.Point(11, 214);
            this.fancy.Margin = new System.Windows.Forms.Padding(2);
            this.fancy.Name = "fancy";
            this.fancy.Size = new System.Drawing.Size(126, 17);
            this.fancy.TabIndex = 6;
            this.fancy.Text = "Convert to MattScript";
            this.fancy.UseVisualStyleBackColor = true;
            this.fancy.CheckedChanged += new System.EventHandler(this.fancy_CheckedChanged);
            // 
            // fancyLabel
            // 
            this.fancyLabel.Location = new System.Drawing.Point(8, 233);
            this.fancyLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.fancyLabel.Name = "fancyLabel";
            this.fancyLabel.Size = new System.Drawing.Size(128, 34);
            this.fancyLabel.TabIndex = 7;
            this.fancyLabel.Text = "(Can also be done from Edit menu)";
            // 
            // customBtn
            // 
            this.customBtn.Enabled = false;
            this.customBtn.Location = new System.Drawing.Point(12, 184);
            this.customBtn.Margin = new System.Windows.Forms.Padding(2);
            this.customBtn.Name = "customBtn";
            this.customBtn.Size = new System.Drawing.Size(125, 26);
            this.customBtn.TabIndex = 6;
            this.customBtn.Text = "Custom EMEDF...";
            this.customBtn.UseVisualStyleBackColor = true;
            this.customBtn.Click += new System.EventHandler(this.customBtn_Click);
            // 
            // GameChooser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(149, 266);
            this.Controls.Add(this.fancyLabel);
            this.Controls.Add(this.fancy);
            this.Controls.Add(this.customBtn);
            this.Controls.Add(this.ds2scholarBtn);
            this.Controls.Add(this.ds2Btn);
            this.Controls.Add(this.sekiroBtn);
            this.Controls.Add(this.ds3Btn);
            this.Controls.Add(this.bbBtn);
            this.Controls.Add(this.ds1Btn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.KeyPreview = true;
            this.Name = "GameChooser";
            this.Text = "GameChooser";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GameChooser_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button ds1Btn;
        private System.Windows.Forms.Button bbBtn;
        private System.Windows.Forms.Button ds3Btn;
        private System.Windows.Forms.Button sekiroBtn;
        private System.Windows.Forms.Button ds2Btn;
        private System.Windows.Forms.Button ds2scholarBtn;
        private System.Windows.Forms.CheckBox fancy;
        private System.Windows.Forms.Label fancyLabel;
        private System.Windows.Forms.Button customBtn;
    }
}