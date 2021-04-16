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
            this.customBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ds1Btn
            // 
            this.ds1Btn.Location = new System.Drawing.Point(24, 23);
            this.ds1Btn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.ds1Btn.Name = "ds1Btn";
            this.ds1Btn.Size = new System.Drawing.Size(250, 44);
            this.ds1Btn.TabIndex = 0;
            this.ds1Btn.Text = "Dark Souls";
            this.ds1Btn.UseVisualStyleBackColor = true;
            this.ds1Btn.Click += new System.EventHandler(this.Ds1Btn_Click);
            // 
            // bbBtn
            // 
            this.bbBtn.Location = new System.Drawing.Point(24, 79);
            this.bbBtn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.bbBtn.Name = "bbBtn";
            this.bbBtn.Size = new System.Drawing.Size(250, 44);
            this.bbBtn.TabIndex = 1;
            this.bbBtn.Text = "Bloodborne";
            this.bbBtn.UseVisualStyleBackColor = true;
            this.bbBtn.Click += new System.EventHandler(this.BbBtn_Click);
            // 
            // ds3Btn
            // 
            this.ds3Btn.Location = new System.Drawing.Point(24, 244);
            this.ds3Btn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.ds3Btn.Name = "ds3Btn";
            this.ds3Btn.Size = new System.Drawing.Size(250, 44);
            this.ds3Btn.TabIndex = 2;
            this.ds3Btn.Text = "Dark Souls III";
            this.ds3Btn.UseVisualStyleBackColor = true;
            this.ds3Btn.Click += new System.EventHandler(this.Ds3Btn_Click);
            // 
            // sekiroBtn
            // 
            this.sekiroBtn.Location = new System.Drawing.Point(24, 300);
            this.sekiroBtn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.sekiroBtn.Name = "sekiroBtn";
            this.sekiroBtn.Size = new System.Drawing.Size(250, 44);
            this.sekiroBtn.TabIndex = 3;
            this.sekiroBtn.Text = "Sekiro";
            this.sekiroBtn.UseVisualStyleBackColor = true;
            this.sekiroBtn.Click += new System.EventHandler(this.SekiroBtn_Click);
            // 
            // ds2Btn
            // 
            this.ds2Btn.Location = new System.Drawing.Point(24, 135);
            this.ds2Btn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.ds2Btn.Name = "ds2Btn";
            this.ds2Btn.Size = new System.Drawing.Size(250, 44);
            this.ds2Btn.TabIndex = 4;
            this.ds2Btn.Text = "Dark Souls II";
            this.ds2Btn.UseVisualStyleBackColor = true;
            this.ds2Btn.Click += new System.EventHandler(this.ds2Btn_Click);
            // 
            // ds2scholarBtn
            // 
            this.ds2scholarBtn.Location = new System.Drawing.Point(24, 190);
            this.ds2scholarBtn.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.ds2scholarBtn.Name = "ds2scholarBtn";
            this.ds2scholarBtn.Size = new System.Drawing.Size(250, 44);
            this.ds2scholarBtn.TabIndex = 5;
            this.ds2scholarBtn.Text = "Dark Souls II SOTFS";
            this.ds2scholarBtn.UseVisualStyleBackColor = true;
            this.ds2scholarBtn.Click += new System.EventHandler(this.ds2scholarBtn_Click);
            // 
            // customBtn
            // 
            this.customBtn.Enabled = false;
            this.customBtn.Location = new System.Drawing.Point(24, 381);
            this.customBtn.Name = "customBtn";
            this.customBtn.Size = new System.Drawing.Size(250, 50);
            this.customBtn.TabIndex = 6;
            this.customBtn.Text = "Custom EMEDF...";
            this.customBtn.UseVisualStyleBackColor = true;
            this.customBtn.Click += new System.EventHandler(this.customBtn_Click);
            // 
            // GameChooser
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(298, 451);
            this.Controls.Add(this.customBtn);
            this.Controls.Add(this.ds2scholarBtn);
            this.Controls.Add(this.ds2Btn);
            this.Controls.Add(this.sekiroBtn);
            this.Controls.Add(this.ds3Btn);
            this.Controls.Add(this.bbBtn);
            this.Controls.Add(this.ds1Btn);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(6, 6, 6, 6);
            this.Name = "GameChooser";
            this.Text = "GameChooser";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button ds1Btn;
        private System.Windows.Forms.Button bbBtn;
        private System.Windows.Forms.Button ds3Btn;
        private System.Windows.Forms.Button sekiroBtn;
        private System.Windows.Forms.Button ds2Btn;
        private System.Windows.Forms.Button ds2scholarBtn;
        private System.Windows.Forms.Button customBtn;
    }
}