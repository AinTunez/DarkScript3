namespace DarkScript3
{
    partial class GUI
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUI));
            this.editor = new FastColoredTextBoxNS.FastColoredTextBox();
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.batchDumpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.batchResaveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.findToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.selectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.optionsToolStripMenuItem = new System.Windows.Forms.ToolStripSeparator();
            this.decompileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.customizeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.scriptCompilationSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openAutoCompleteMenuToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.emevdDataToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.previewCompilationOutputToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.documentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewEMEDFToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewEMEVDTutorialToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewFancyDocumentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.display = new System.Windows.Forms.SplitContainer();
            this.docBox = new FastColoredTextBoxNS.FastColoredTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.editor)).BeginInit();
            this.menuStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.display)).BeginInit();
            this.display.Panel1.SuspendLayout();
            this.display.Panel2.SuspendLayout();
            this.display.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.docBox)).BeginInit();
            this.SuspendLayout();
            // 
            // editor
            // 
            this.editor.AutoCompleteBracketsList = new char[] {
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
            this.editor.AutoIndentChars = false;
            this.editor.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            this.editor.AutoScrollMinSize = new System.Drawing.Size(23, 12);
            this.editor.BackBrush = null;
            this.editor.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.editor.BracketsHighlightStrategy = FastColoredTextBoxNS.BracketsHighlightStrategy.Strategy2;
            this.editor.CaretColor = System.Drawing.Color.White;
            this.editor.CharHeight = 12;
            this.editor.CharWidth = 6;
            this.editor.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.editor.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.editor.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editor.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.editor.ForeColor = System.Drawing.Color.Gainsboro;
            this.editor.IndentBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.editor.IsReplaceMode = false;
            this.editor.LeftBracket = '(';
            this.editor.LeftBracket2 = '{';
            this.editor.LineNumberColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.editor.Location = new System.Drawing.Point(0, 0);
            this.editor.Name = "editor";
            this.editor.Paddings = new System.Windows.Forms.Padding(0);
            this.editor.RightBracket = ')';
            this.editor.RightBracket2 = '}';
            this.editor.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.editor.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("editor.ServiceColors")));
            this.editor.Size = new System.Drawing.Size(573, 475);
            this.editor.TabIndex = 0;
            this.editor.Zoom = 100;
            this.editor.ToolTipNeeded += new System.EventHandler<FastColoredTextBoxNS.ToolTipNeededEventArgs>(this.Editor_ToolTipNeeded);
            this.editor.TextChanged += new System.EventHandler<FastColoredTextBoxNS.TextChangedEventArgs>(this.Editor_TextChanged);
            this.editor.SelectionChanged += new System.EventHandler(this.Editor_SelectionChanged);
            this.editor.VisibleRangeChangedDelayed += new System.EventHandler(this.Editor_VisibleRangeChangedDelayed);
            this.editor.ZoomChanged += new System.EventHandler(this.Editor_ZoomChanged);
            this.editor.Scroll += new System.Windows.Forms.ScrollEventHandler(this.Editor_Scroll);
            this.editor.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Editor_KeyDown);
            this.editor.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Editor_MouseUp);
            // 
            // menuStrip
            // 
            this.menuStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.helpToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(905, 24);
            this.menuStrip.TabIndex = 1;
            this.menuStrip.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.openToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.batchDumpToolStripMenuItem,
            this.batchResaveToolStripMenuItem,
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
            this.openToolStripMenuItem.Text = "Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.SaveToolStripMenuItem_Click);
            // 
            // batchDumpToolStripMenuItem
            // 
            this.batchDumpToolStripMenuItem.Name = "batchDumpToolStripMenuItem";
            this.batchDumpToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
            this.batchDumpToolStripMenuItem.Text = "Batch Dump (EMEVD→JS)...";
            this.batchDumpToolStripMenuItem.Click += new System.EventHandler(this.batchDumpToolStripMenuItem_Click);
            // 
            // batchResaveToolStripMenuItem
            // 
            this.batchResaveToolStripMenuItem.Name = "batchResaveToolStripMenuItem";
            this.batchResaveToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
            this.batchResaveToolStripMenuItem.Text = "Batch Resave (JS→EMEVD)...";
            this.batchResaveToolStripMenuItem.Click += new System.EventHandler(this.batchResaveToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(221, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItem_Click);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.cutToolStripMenuItem,
            this.copyToolStripMenuItem,
            this.pasteToolStripMenuItem,
            this.toolStripSeparator1,
            this.findToolStripMenuItem,
            this.replaceToolStripMenuItem,
            this.toolStripSeparator2,
            this.selectAllToolStripMenuItem,
            this.optionsToolStripMenuItem,
            this.decompileToolStripMenuItem,
            this.toolStripSeparator3,
            this.customizeToolStripMenuItem,
            this.scriptCompilationSettingsToolStripMenuItem,
            this.openAutoCompleteMenuToolStripMenuItem});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            this.editToolStripMenuItem.Size = new System.Drawing.Size(39, 20);
            this.editToolStripMenuItem.Text = "Edit";
            // 
            // cutToolStripMenuItem
            // 
            this.cutToolStripMenuItem.Name = "cutToolStripMenuItem";
            this.cutToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+X";
            this.cutToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.cutToolStripMenuItem.Text = "Cut";
            this.cutToolStripMenuItem.Click += new System.EventHandler(this.CutToolStripMenuItem_Click);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+C";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.CopyToolStripMenuItem_Click);
            // 
            // pasteToolStripMenuItem
            // 
            this.pasteToolStripMenuItem.Name = "pasteToolStripMenuItem";
            this.pasteToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+V";
            this.pasteToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.pasteToolStripMenuItem.Text = "Paste";
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(274, 6);
            // 
            // findToolStripMenuItem
            // 
            this.findToolStripMenuItem.Name = "findToolStripMenuItem";
            this.findToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+F";
            this.findToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.findToolStripMenuItem.Text = "Find";
            this.findToolStripMenuItem.Click += new System.EventHandler(this.FindToolStripMenuItem_Click);
            // 
            // replaceToolStripMenuItem
            // 
            this.replaceToolStripMenuItem.Name = "replaceToolStripMenuItem";
            this.replaceToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+H";
            this.replaceToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.replaceToolStripMenuItem.Text = "Replace";
            this.replaceToolStripMenuItem.Click += new System.EventHandler(this.ReplaceToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(274, 6);
            // 
            // selectAllToolStripMenuItem
            // 
            this.selectAllToolStripMenuItem.Name = "selectAllToolStripMenuItem";
            this.selectAllToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+A";
            this.selectAllToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.selectAllToolStripMenuItem.Text = "Select All";
            this.selectAllToolStripMenuItem.Click += new System.EventHandler(this.SelectAllToolStripMenuItem_Click);
            // 
            // optionsToolStripMenuItem
            // 
            this.optionsToolStripMenuItem.Name = "optionsToolStripMenuItem";
            this.optionsToolStripMenuItem.Size = new System.Drawing.Size(274, 6);
            // 
            // decompileToolStripMenuItem
            // 
            this.decompileToolStripMenuItem.Name = "decompileToolStripMenuItem";
            this.decompileToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.decompileToolStripMenuItem.Text = "Convert to MattScript...";
            this.decompileToolStripMenuItem.Click += new System.EventHandler(this.decompileToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(274, 6);
            // 
            // customizeToolStripMenuItem
            // 
            this.customizeToolStripMenuItem.Name = "customizeToolStripMenuItem";
            this.customizeToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.customizeToolStripMenuItem.Text = "Customize Appearance...";
            this.customizeToolStripMenuItem.Click += new System.EventHandler(this.customizeToolStripMenuItem_Click);
            // 
            // scriptCompilationSettingsToolStripMenuItem
            // 
            this.scriptCompilationSettingsToolStripMenuItem.Name = "scriptCompilationSettingsToolStripMenuItem";
            this.scriptCompilationSettingsToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.scriptCompilationSettingsToolStripMenuItem.Text = "Script Compilation Settings...";
            this.scriptCompilationSettingsToolStripMenuItem.Click += new System.EventHandler(this.scriptCompilationSettingsToolStripMenuItem_Click);
            // 
            // openAutoCompleteMenuToolStripMenuItem
            // 
            this.openAutoCompleteMenuToolStripMenuItem.Name = "openAutoCompleteMenuToolStripMenuItem";
            this.openAutoCompleteMenuToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Space)));
            this.openAutoCompleteMenuToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.openAutoCompleteMenuToolStripMenuItem.Text = "OpenAutoCompleteMenu";
            this.openAutoCompleteMenuToolStripMenuItem.Visible = false;
            this.openAutoCompleteMenuToolStripMenuItem.Click += new System.EventHandler(this.openAutoCompleteMenuToolStripMenuItem_Click);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.emevdDataToolStripMenuItem,
            this.previewCompilationOutputToolStripMenuItem,
            this.documentationToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // emevdDataToolStripMenuItem
            // 
            this.emevdDataToolStripMenuItem.Name = "emevdDataToolStripMenuItem";
            this.emevdDataToolStripMenuItem.Size = new System.Drawing.Size(239, 22);
            this.emevdDataToolStripMenuItem.Text = "EMEVD Data...";
            this.emevdDataToolStripMenuItem.Click += new System.EventHandler(this.EmevdDataToolStripMenuItem_Click);
            // 
            // previewCompilationOutputToolStripMenuItem
            // 
            this.previewCompilationOutputToolStripMenuItem.Name = "previewCompilationOutputToolStripMenuItem";
            this.previewCompilationOutputToolStripMenuItem.Size = new System.Drawing.Size(239, 22);
            this.previewCompilationOutputToolStripMenuItem.Text = "Preview Compilation Output...";
            this.previewCompilationOutputToolStripMenuItem.Click += new System.EventHandler(this.previewCompilationOutputToolStripMenuItem_Click);
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D)));
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(239, 22);
            this.documentationToolStripMenuItem.Text = "Toggle Doc Panel";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.DocumentationToolStripMenuItem_Click);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewEMEDFToolStripMenuItem,
            this.viewEMEVDTutorialToolStripMenuItem,
            this.viewFancyDocumentationToolStripMenuItem,
            this.toolStripMenuItem1,
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            this.helpToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.helpToolStripMenuItem.Text = "Help";
            // 
            // viewEMEDFToolStripMenuItem
            // 
            this.viewEMEDFToolStripMenuItem.Name = "viewEMEDFToolStripMenuItem";
            this.viewEMEDFToolStripMenuItem.Size = new System.Drawing.Size(247, 22);
            this.viewEMEDFToolStripMenuItem.Text = "View EMEDF (List of Instructions)";
            this.viewEMEDFToolStripMenuItem.Click += new System.EventHandler(this.viewEMEDFToolStripMenuItem_Click);
            // 
            // viewEMEVDTutorialToolStripMenuItem
            // 
            this.viewEMEVDTutorialToolStripMenuItem.Name = "viewEMEVDTutorialToolStripMenuItem";
            this.viewEMEVDTutorialToolStripMenuItem.Size = new System.Drawing.Size(247, 22);
            this.viewEMEVDTutorialToolStripMenuItem.Text = "View EMEVD Tutorial";
            this.viewEMEVDTutorialToolStripMenuItem.Click += new System.EventHandler(this.viewEMEVDTutorialToolStripMenuItem_Click);
            // 
            // viewFancyDocumentationToolStripMenuItem
            // 
            this.viewFancyDocumentationToolStripMenuItem.Name = "viewFancyDocumentationToolStripMenuItem";
            this.viewFancyDocumentationToolStripMenuItem.Size = new System.Drawing.Size(247, 22);
            this.viewFancyDocumentationToolStripMenuItem.Text = "View MattScript Documentation";
            this.viewFancyDocumentationToolStripMenuItem.Click += new System.EventHandler(this.viewFancyDocumentationToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(244, 6);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(247, 22);
            this.aboutToolStripMenuItem.Text = "About DarkScript3";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            // 
            // statusLabel
            // 
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(66, 17);
            this.statusLabel.Text = "statusLabel";
            // 
            // statusStrip
            // 
            this.statusStrip.AutoSize = false;
            this.statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusLabel});
            this.statusStrip.Location = new System.Drawing.Point(0, 509);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(905, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip1";
            // 
            // display
            // 
            this.display.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.display.IsSplitterFixed = true;
            this.display.Location = new System.Drawing.Point(8, 31);
            this.display.Name = "display";
            // 
            // display.Panel1
            // 
            this.display.Panel1.Controls.Add(this.docBox);
            // 
            // display.Panel2
            // 
            this.display.Panel2.Controls.Add(this.editor);
            this.display.Size = new System.Drawing.Size(897, 475);
            this.display.SplitterDistance = 320;
            this.display.TabIndex = 4;
            this.display.Resize += new System.EventHandler(this.Display_Resize);
            // 
            // docBox
            // 
            this.docBox.AutoCompleteBracketsList = new char[] {
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
            this.docBox.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            this.docBox.AutoScrollMinSize = new System.Drawing.Size(0, 50);
            this.docBox.BackBrush = null;
            this.docBox.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.docBox.BracketsHighlightStrategy = FastColoredTextBoxNS.BracketsHighlightStrategy.Strategy2;
            this.docBox.CharHeight = 14;
            this.docBox.CharWidth = 7;
            this.docBox.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.docBox.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.docBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.docBox.Font = new System.Drawing.Font("Consolas", 9F);
            this.docBox.ForeColor = System.Drawing.Color.Gainsboro;
            this.docBox.IndentBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
            this.docBox.IsReplaceMode = false;
            this.docBox.LeftBracket = '(';
            this.docBox.LeftBracket2 = '{';
            this.docBox.LineNumberColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(240)))), ((int)(((byte)(240)))));
            this.docBox.Location = new System.Drawing.Point(0, 0);
            this.docBox.Name = "docBox";
            this.docBox.Paddings = new System.Windows.Forms.Padding(18);
            this.docBox.ReadOnly = true;
            this.docBox.RightBracket = ')';
            this.docBox.RightBracket2 = '}';
            this.docBox.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.docBox.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("docBox.ServiceColors")));
            this.docBox.ShowLineNumbers = false;
            this.docBox.ShowScrollBars = false;
            this.docBox.Size = new System.Drawing.Size(320, 475);
            this.docBox.TabIndex = 1;
            this.docBox.TabStop = false;
            this.docBox.WordWrap = true;
            this.docBox.Zoom = 100;
            this.docBox.ZoomChanged += new System.EventHandler(this.docBox_ZoomChanged);
            // 
            // GUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(40)))), ((int)(((byte)(40)))));
            this.ClientSize = new System.Drawing.Size(905, 531);
            this.Controls.Add(this.display);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip;
            this.Name = "GUI";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "DARKSCRIPT 3";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GUI_KeyDown);
            ((System.ComponentModel.ISupportInitialize)(this.editor)).EndInit();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.display.Panel1.ResumeLayout(false);
            this.display.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.display)).EndInit();
            this.display.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.docBox)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem saveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem emevdDataToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem documentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.SplitContainer display;
        private FastColoredTextBoxNS.FastColoredTextBox docBox;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem cutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem pasteToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripMenuItem findToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replaceToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem selectAllToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem batchDumpToolStripMenuItem;
        public FastColoredTextBoxNS.FastColoredTextBox editor;
        private System.Windows.Forms.ToolStripSeparator optionsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem customizeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openAutoCompleteMenuToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem decompileToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem previewCompilationOutputToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scriptCompilationSettingsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewEMEDFToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewEMEVDTutorialToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewFancyDocumentationToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem batchResaveToolStripMenuItem;
    }
}

