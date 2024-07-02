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
            ds1Btn = new System.Windows.Forms.Button();
            bbBtn = new System.Windows.Forms.Button();
            ds3Btn = new System.Windows.Forms.Button();
            sekiroBtn = new System.Windows.Forms.Button();
            ds2Btn = new System.Windows.Forms.Button();
            ds2scholarBtn = new System.Windows.Forms.Button();
            fancy = new System.Windows.Forms.CheckBox();
            fancyLabel = new System.Windows.Forms.Label();
            customBtn = new System.Windows.Forms.Button();
            eldenBtn = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // ds1Btn
            // 
            ds1Btn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ds1Btn.Location = new System.Drawing.Point(14, 14);
            ds1Btn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            ds1Btn.Name = "ds1Btn";
            ds1Btn.Size = new System.Drawing.Size(164, 27);
            ds1Btn.TabIndex = 0;
            ds1Btn.Text = "Dark Souls";
            ds1Btn.UseVisualStyleBackColor = true;
            ds1Btn.Click += Ds1Btn_Click;
            // 
            // bbBtn
            // 
            bbBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            bbBtn.Location = new System.Drawing.Point(14, 47);
            bbBtn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            bbBtn.Name = "bbBtn";
            bbBtn.Size = new System.Drawing.Size(164, 27);
            bbBtn.TabIndex = 1;
            bbBtn.Text = "Bloodborne";
            bbBtn.UseVisualStyleBackColor = true;
            bbBtn.Click += BbBtn_Click;
            // 
            // ds3Btn
            // 
            ds3Btn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ds3Btn.Location = new System.Drawing.Point(14, 147);
            ds3Btn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            ds3Btn.Name = "ds3Btn";
            ds3Btn.Size = new System.Drawing.Size(164, 27);
            ds3Btn.TabIndex = 2;
            ds3Btn.Text = "Dark Souls III";
            ds3Btn.UseVisualStyleBackColor = true;
            ds3Btn.Click += Ds3Btn_Click;
            // 
            // sekiroBtn
            // 
            sekiroBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            sekiroBtn.Location = new System.Drawing.Point(14, 180);
            sekiroBtn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            sekiroBtn.Name = "sekiroBtn";
            sekiroBtn.Size = new System.Drawing.Size(164, 27);
            sekiroBtn.TabIndex = 3;
            sekiroBtn.Text = "Sekiro";
            sekiroBtn.UseVisualStyleBackColor = true;
            sekiroBtn.Click += SekiroBtn_Click;
            // 
            // ds2Btn
            // 
            ds2Btn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ds2Btn.Location = new System.Drawing.Point(14, 81);
            ds2Btn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            ds2Btn.Name = "ds2Btn";
            ds2Btn.Size = new System.Drawing.Size(164, 27);
            ds2Btn.TabIndex = 4;
            ds2Btn.Text = "Dark Souls II";
            ds2Btn.UseVisualStyleBackColor = true;
            ds2Btn.Click += ds2Btn_Click;
            // 
            // ds2scholarBtn
            // 
            ds2scholarBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            ds2scholarBtn.Location = new System.Drawing.Point(14, 114);
            ds2scholarBtn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            ds2scholarBtn.Name = "ds2scholarBtn";
            ds2scholarBtn.Size = new System.Drawing.Size(164, 27);
            ds2scholarBtn.TabIndex = 5;
            ds2scholarBtn.Text = "Dark Souls II SOTFS";
            ds2scholarBtn.UseVisualStyleBackColor = true;
            ds2scholarBtn.Click += ds2scholarBtn_Click;
            // 
            // fancy
            // 
            fancy.AutoSize = true;
            fancy.Location = new System.Drawing.Point(14, 279);
            fancy.Margin = new System.Windows.Forms.Padding(2);
            fancy.Name = "fancy";
            fancy.Size = new System.Drawing.Size(103, 19);
            fancy.TabIndex = 6;
            fancy.Text = "Use MattScript";
            fancy.UseVisualStyleBackColor = true;
            fancy.CheckedChanged += fancy_CheckedChanged;
            // 
            // fancyLabel
            // 
            fancyLabel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            fancyLabel.Location = new System.Drawing.Point(10, 301);
            fancyLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            fancyLabel.Name = "fancyLabel";
            fancyLabel.Size = new System.Drawing.Size(168, 39);
            fancyLabel.TabIndex = 7;
            fancyLabel.Text = "(Formats scripts to make them easier to understand)";
            // 
            // customBtn
            // 
            customBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            customBtn.Location = new System.Drawing.Point(14, 245);
            customBtn.Margin = new System.Windows.Forms.Padding(2);
            customBtn.Name = "customBtn";
            customBtn.Size = new System.Drawing.Size(164, 30);
            customBtn.TabIndex = 6;
            customBtn.Text = "Custom EMEDF...";
            customBtn.UseVisualStyleBackColor = true;
            customBtn.Click += customBtn_Click;
            // 
            // eldenBtn
            // 
            eldenBtn.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            eldenBtn.Location = new System.Drawing.Point(14, 213);
            eldenBtn.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            eldenBtn.Name = "eldenBtn";
            eldenBtn.Size = new System.Drawing.Size(164, 27);
            eldenBtn.TabIndex = 8;
            eldenBtn.Text = "Elden Ring";
            eldenBtn.UseVisualStyleBackColor = true;
            eldenBtn.Click += eldenBtn_Click;
            // 
            // GameChooser
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(192, 355);
            Controls.Add(eldenBtn);
            Controls.Add(fancyLabel);
            Controls.Add(fancy);
            Controls.Add(customBtn);
            Controls.Add(ds2scholarBtn);
            Controls.Add(ds2Btn);
            Controls.Add(sekiroBtn);
            Controls.Add(ds3Btn);
            Controls.Add(bbBtn);
            Controls.Add(ds1Btn);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            KeyPreview = true;
            Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            Name = "GameChooser";
            Text = "GameChooser";
            KeyDown += GameChooser_KeyDown;
            ResumeLayout(false);
            PerformLayout();
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
        private System.Windows.Forms.Button eldenBtn;
    }
}