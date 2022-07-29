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
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.closeTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.saveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.batchDumpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.batchResaveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.nextTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.previousTabToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.cutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pasteToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.findToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findInFilesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.selectAllToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            this.goToEventIDUnderCursorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.replaceFloatUnderCursorToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.showArgumentsInTooltipToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.showArgumentsInPanelToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewEMEDFToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewEMEVDTutorialToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewEldenRingEMEVDTutorialToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.viewFancyDocumentationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.checkForDarkScript3UpdatesToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.display = new System.Windows.Forms.SplitContainer();
            this.display2 = new System.Windows.Forms.SplitContainer();
            this.docBox = new FastColoredTextBoxNS.FastColoredTextBox();
            this.fileView = new System.Windows.Forms.TreeView();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            this.menuStrip.SuspendLayout();
            this.statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.display)).BeginInit();
            this.display.Panel1.SuspendLayout();
            this.display.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.display2)).BeginInit();
            this.display2.Panel1.SuspendLayout();
            this.display2.Panel2.SuspendLayout();
            this.display2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.docBox)).BeginInit();
            this.SuspendLayout();
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
            this.closeTabToolStripMenuItem,
            this.saveToolStripMenuItem,
            this.batchDumpToolStripMenuItem,
            this.batchResaveToolStripMenuItem,
            this.exitToolStripMenuItem,
            this.nextTabToolStripMenuItem,
            this.previousTabToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(37, 20);
            this.fileToolStripMenuItem.Text = "File";
            // 
            // openToolStripMenuItem
            // 
            this.openToolStripMenuItem.Name = "openToolStripMenuItem";
            this.openToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.openToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.openToolStripMenuItem.Text = "Open...";
            this.openToolStripMenuItem.Click += new System.EventHandler(this.OpenToolStripMenuItem_Click);
            // 
            // closeTabToolStripMenuItem
            // 
            this.closeTabToolStripMenuItem.Name = "closeTabToolStripMenuItem";
            this.closeTabToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.W)));
            this.closeTabToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.closeTabToolStripMenuItem.Text = "Close Tab";
            this.closeTabToolStripMenuItem.Click += new System.EventHandler(this.closeTabToolStripMenuItem_Click);
            // 
            // saveToolStripMenuItem
            // 
            this.saveToolStripMenuItem.Name = "saveToolStripMenuItem";
            this.saveToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.saveToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.saveToolStripMenuItem.Text = "Save";
            this.saveToolStripMenuItem.Click += new System.EventHandler(this.SaveToolStripMenuItem_Click);
            // 
            // batchDumpToolStripMenuItem
            // 
            this.batchDumpToolStripMenuItem.Name = "batchDumpToolStripMenuItem";
            this.batchDumpToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.batchDumpToolStripMenuItem.Text = "Batch Dump (EMEVD→JS)...";
            this.batchDumpToolStripMenuItem.Click += new System.EventHandler(this.batchDumpToolStripMenuItem_Click);
            // 
            // batchResaveToolStripMenuItem
            // 
            this.batchResaveToolStripMenuItem.Name = "batchResaveToolStripMenuItem";
            this.batchResaveToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.batchResaveToolStripMenuItem.Text = "Batch Resave (JS→EMEVD)...";
            this.batchResaveToolStripMenuItem.Click += new System.EventHandler(this.batchResaveToolStripMenuItem_Click);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItem_Click);
            // 
            // nextTabToolStripMenuItem
            // 
            this.nextTabToolStripMenuItem.Name = "nextTabToolStripMenuItem";
            this.nextTabToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Tab)));
            this.nextTabToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.nextTabToolStripMenuItem.Text = "NextTabHidden";
            this.nextTabToolStripMenuItem.Visible = false;
            this.nextTabToolStripMenuItem.Click += new System.EventHandler(this.nextTabToolStripMenuItem_Click);
            // 
            // previousTabToolStripMenuItem
            // 
            this.previousTabToolStripMenuItem.Name = "previousTabToolStripMenuItem";
            this.previousTabToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.Tab)));
            this.previousTabToolStripMenuItem.Size = new System.Drawing.Size(260, 22);
            this.previousTabToolStripMenuItem.Text = "PreviousTabHidden";
            this.previousTabToolStripMenuItem.Visible = false;
            this.previousTabToolStripMenuItem.Click += new System.EventHandler(this.previousTabToolStripMenuItem_Click);
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
            this.findInFilesToolStripMenuItem,
            this.toolStripSeparator2,
            this.selectAllToolStripMenuItem,
            this.toolStripSeparator4,
            this.goToEventIDUnderCursorToolStripMenuItem,
            this.replaceFloatUnderCursorToolStripMenuItem,
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
            this.pasteToolStripMenuItem.Click += new System.EventHandler(this.pasteToolStripMenuItem_Click);
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
            // findInFilesToolStripMenuItem
            // 
            this.findInFilesToolStripMenuItem.Name = "findInFilesToolStripMenuItem";
            this.findInFilesToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.F)));
            this.findInFilesToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.findInFilesToolStripMenuItem.Text = "Find in Files...";
            this.findInFilesToolStripMenuItem.Click += new System.EventHandler(this.findInFilesToolStripMenuItem_Click);
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
            // toolStripSeparator4
            // 
            this.toolStripSeparator4.Name = "toolStripSeparator4";
            this.toolStripSeparator4.Size = new System.Drawing.Size(274, 6);
            // 
            // goToEventIDUnderCursorToolStripMenuItem
            // 
            this.goToEventIDUnderCursorToolStripMenuItem.Name = "goToEventIDUnderCursorToolStripMenuItem";
            this.goToEventIDUnderCursorToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+Enter";
            this.goToEventIDUnderCursorToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.goToEventIDUnderCursorToolStripMenuItem.Text = "Go to Event ID";
            this.goToEventIDUnderCursorToolStripMenuItem.Click += new System.EventHandler(this.goToEventIDUnderCursorToolStripMenuItem_Click);
            // 
            // replaceFloatUnderCursorToolStripMenuItem
            // 
            this.replaceFloatUnderCursorToolStripMenuItem.Name = "replaceFloatUnderCursorToolStripMenuItem";
            this.replaceFloatUnderCursorToolStripMenuItem.ShortcutKeyDisplayString = "Ctrl+1";
            this.replaceFloatUnderCursorToolStripMenuItem.Size = new System.Drawing.Size(277, 22);
            this.replaceFloatUnderCursorToolStripMenuItem.Text = "Replace Float";
            this.replaceFloatUnderCursorToolStripMenuItem.Click += new System.EventHandler(this.replaceFloatUnderCursorToolStripMenuItem_Click);
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
            this.decompileToolStripMenuItem.Text = "Preview Conversion to MattScript...";
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
            this.documentationToolStripMenuItem,
            this.toolStripSeparator5,
            this.showArgumentsInTooltipToolStripMenuItem,
            this.showArgumentsInPanelToolStripMenuItem});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            this.viewToolStripMenuItem.Size = new System.Drawing.Size(44, 20);
            this.viewToolStripMenuItem.Text = "View";
            // 
            // emevdDataToolStripMenuItem
            // 
            this.emevdDataToolStripMenuItem.Name = "emevdDataToolStripMenuItem";
            this.emevdDataToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.emevdDataToolStripMenuItem.Text = "EMEVD Data...";
            this.emevdDataToolStripMenuItem.Click += new System.EventHandler(this.EmevdDataToolStripMenuItem_Click);
            // 
            // previewCompilationOutputToolStripMenuItem
            // 
            this.previewCompilationOutputToolStripMenuItem.Name = "previewCompilationOutputToolStripMenuItem";
            this.previewCompilationOutputToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.previewCompilationOutputToolStripMenuItem.Text = "Preview Compilation Output...";
            this.previewCompilationOutputToolStripMenuItem.Click += new System.EventHandler(this.previewCompilationOutputToolStripMenuItem_Click);
            // 
            // documentationToolStripMenuItem
            // 
            this.documentationToolStripMenuItem.Name = "documentationToolStripMenuItem";
            this.documentationToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)(((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.Shift) 
            | System.Windows.Forms.Keys.D)));
            this.documentationToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.documentationToolStripMenuItem.Text = "Toggle Panel";
            this.documentationToolStripMenuItem.Click += new System.EventHandler(this.DocumentationToolStripMenuItem_Click);
            // 
            // showArgumentsInTooltipToolStripMenuItem
            // 
            this.showArgumentsInTooltipToolStripMenuItem.CheckOnClick = true;
            this.showArgumentsInTooltipToolStripMenuItem.Name = "showArgumentsInTooltipToolStripMenuItem";
            this.showArgumentsInTooltipToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.showArgumentsInTooltipToolStripMenuItem.Text = "Show Arguments in Tooltip";
            this.showArgumentsInTooltipToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showArgumentTooltipsToolStripMenuItem_CheckedChanged);
            // 
            // showArgumentsInPanelToolStripMenuItem
            // 
            this.showArgumentsInPanelToolStripMenuItem.CheckOnClick = true;
            this.showArgumentsInPanelToolStripMenuItem.Name = "showArgumentsInPanelToolStripMenuItem";
            this.showArgumentsInPanelToolStripMenuItem.Size = new System.Drawing.Size(234, 22);
            this.showArgumentsInPanelToolStripMenuItem.Text = "Show Arguments in Panel";
            this.showArgumentsInPanelToolStripMenuItem.CheckedChanged += new System.EventHandler(this.showArgumentsInPanelToolStripMenuItem_CheckedChanged);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.viewEMEDFToolStripMenuItem,
            this.viewEMEVDTutorialToolStripMenuItem,
            this.viewEldenRingEMEVDTutorialToolStripMenuItem,
            this.viewFancyDocumentationToolStripMenuItem,
            this.toolStripMenuItem1,
            this.aboutToolStripMenuItem,
            this.checkForDarkScript3UpdatesToolStripMenuItem});
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
            // viewEldenRingEMEVDTutorialToolStripMenuItem
            // 
            this.viewEldenRingEMEVDTutorialToolStripMenuItem.Name = "viewEldenRingEMEVDTutorialToolStripMenuItem";
            this.viewEldenRingEMEVDTutorialToolStripMenuItem.Size = new System.Drawing.Size(247, 22);
            this.viewEldenRingEMEVDTutorialToolStripMenuItem.Text = "View Elden Ring EMEVD Tutorial";
            this.viewEldenRingEMEVDTutorialToolStripMenuItem.Click += new System.EventHandler(this.viewEldenRingEMEVDTutorialToolStripMenuItem_Click);
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
            // checkForDarkScript3UpdatesToolStripMenuItem
            // 
            this.checkForDarkScript3UpdatesToolStripMenuItem.Name = "checkForDarkScript3UpdatesToolStripMenuItem";
            this.checkForDarkScript3UpdatesToolStripMenuItem.Size = new System.Drawing.Size(247, 22);
            this.checkForDarkScript3UpdatesToolStripMenuItem.Text = "Check for DarkScript3 Updates";
            this.checkForDarkScript3UpdatesToolStripMenuItem.Click += new System.EventHandler(this.checkForDarkScript3UpdatesToolStripMenuItem_Click);
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
            this.statusStrip.Location = new System.Drawing.Point(0, 535);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(905, 22);
            this.statusStrip.SizingGrip = false;
            this.statusStrip.TabIndex = 3;
            this.statusStrip.Text = "statusStrip1";
            // 
            // display
            // 
            this.display.Dock = System.Windows.Forms.DockStyle.Fill;
            this.display.IsSplitterFixed = true;
            this.display.Location = new System.Drawing.Point(0, 47);
            this.display.Name = "display";
            // 
            // display.Panel1
            // 
            this.display.Panel1.Controls.Add(this.display2);
            // 
            // display.Panel2
            // 
            this.display.Panel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.display.Size = new System.Drawing.Size(905, 488);
            this.display.SplitterDistance = 322;
            this.display.TabIndex = 4;
            this.display.Resize += new System.EventHandler(this.Display_Resize);
            // 
            // display2
            // 
            this.display2.BackColor = System.Drawing.Color.Transparent;
            this.display2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.display2.IsSplitterFixed = true;
            this.display2.Location = new System.Drawing.Point(0, 0);
            this.display2.Name = "display2";
            this.display2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // display2.Panel1
            // 
            this.display2.Panel1.Controls.Add(this.docBox);
            // 
            // display2.Panel2
            // 
            this.display2.Panel2.Controls.Add(this.fileView);
            this.display2.Size = new System.Drawing.Size(322, 488);
            this.display2.SplitterDistance = 241;
            this.display2.TabIndex = 0;
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
            this.docBox.Size = new System.Drawing.Size(322, 241);
            this.docBox.TabIndex = 1;
            this.docBox.TabStop = false;
            this.docBox.WordWrap = true;
            this.docBox.Zoom = 100;
            // 
            // fileView
            // 
            this.fileView.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.fileView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.fileView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fileView.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.fileView.Location = new System.Drawing.Point(0, 0);
            this.fileView.Name = "fileView";
            this.fileView.Size = new System.Drawing.Size(322, 243);
            this.fileView.TabIndex = 0;
            this.fileView.NodeMouseDoubleClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.fileView_NodeMouseDoubleClick);
            // 
            // tabControl
            // 
            this.tabControl.Dock = System.Windows.Forms.DockStyle.Top;
            this.tabControl.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl.Location = new System.Drawing.Point(0, 24);
            this.tabControl.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(905, 23);
            this.tabControl.TabIndex = 5;
            this.tabControl.SelectedIndexChanged += new System.EventHandler(this.tabControl_SelectedIndexChanged);
            // 
            // toolStripSeparator5
            // 
            this.toolStripSeparator5.Name = "toolStripSeparator5";
            this.toolStripSeparator5.Size = new System.Drawing.Size(231, 6);
            // 
            // GUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.ClientSize = new System.Drawing.Size(905, 557);
            this.Controls.Add(this.display);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.menuStrip);
            this.Font = new System.Drawing.Font("Consolas", 8.25F);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip;
            this.Name = "GUI";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.Text = "DARKSCRIPT 3";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GUI_FormClosing);
            this.Load += new System.EventHandler(this.GUI_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.GUI_KeyDown);
            this.Move += new System.EventHandler(this.GUI_Move);
            this.Resize += new System.EventHandler(this.GUI_Resize);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.display.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.display)).EndInit();
            this.display.ResumeLayout(false);
            this.display2.Panel1.ResumeLayout(false);
            this.display2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.display2)).EndInit();
            this.display2.ResumeLayout(false);
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
        private System.Windows.Forms.SplitContainer display2;
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
        private System.Windows.Forms.TreeView fileView;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.ToolStripMenuItem closeTabToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem nextTabToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem previousTabToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findInFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
        private System.Windows.Forms.ToolStripMenuItem goToEventIDUnderCursorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem replaceFloatUnderCursorToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem viewEldenRingEMEVDTutorialToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem checkForDarkScript3UpdatesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showArgumentsInTooltipToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem showArgumentsInPanelToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
    }
}

