using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SoulsFormats;
using System.Xml.Linq;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using DarkScript3.Properties;

namespace DarkScript3
{
    public partial class GUI : Form
    {
        private readonly SharedControls SharedControls;
        private readonly Dictionary<string, InstructionDocs> AllDocs = new Dictionary<string, InstructionDocs>();
        private readonly Dictionary<string, EditorGUI> AllEditors = new Dictionary<string, EditorGUI>();
        private readonly Dictionary<string, FileMetadata> DirectoryMetadata = new Dictionary<string, FileMetadata>();
        private readonly Dictionary<string, Dictionary<string, string>> AllMapNames = new Dictionary<string, Dictionary<string, string>>();
        private readonly FileSystemWatcher Watcher;
        private EditorGUI CurrentEditor;
        private string CurrentDirectory;

        public GUI()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
            menuStrip.Renderer = new DarkToolStripRenderer();
            statusStrip.Renderer = new DarkToolStripRenderer();
            TextStyles.LoadColors();
            SharedControls = new SharedControls(this, statusLabel, docBox);
            SharedControls.ResetStatus(true);
            SharedControls.BFF.Owner = this;
            // Prevent fuzzy line from showing up. Tab key is handled by the textbox in any case.
            display.TabStop = false;
            display2.TabStop = false;
            Controls.Add(SharedControls.InfoTip);
            tabControl.Visible = false;
            Watcher = new FileSystemWatcher();
            Watcher.Created += OnDirectoryContentsChanged;
            Watcher.Deleted += OnDirectoryContentsChanged;
            Watcher.Renamed += OnDirectoryContentsChanged;
            Watcher.EnableRaisingEvents = false;
            Watcher.SynchronizingObject = this;
            RefreshGlobalStyles();
        }

        #region File Handling

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.SaveJSAndEMEVDFile();

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SharedControls.HideTip();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx|EMEVD JS Files|*.emevd.js; *.emevd.dcx.js|All files|*.*";
            ofd.Multiselect = true;
            if (CurrentEditor != null)
            {
                string currentDir = Path.GetDirectoryName(CurrentEditor.EMEVDPath);
                if (Directory.Exists(currentDir))
                {
                    ofd.InitialDirectory = currentDir;
                }
            }
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            FileMetadata metadata = null;
            foreach (string fileName in ofd.FileNames)
            {
                metadata = OpenFile(fileName, metadata);
            }
        }

        private FileMetadata OpenFile(string fileName, FileMetadata metadata = null)
        {
            if (fileName.EndsWith(".js"))
            {
                OpenJSFile(fileName);
            }
            else if (File.Exists(fileName + ".js"))
            {
                OpenJSFile(fileName + ".js");
            }
            else if (fileName.EndsWith(".xml"))
            {
                OpenXMLFile(fileName);
            }
            else if (File.Exists(fileName + ".xml"))
            {
                OpenXMLFile(fileName + ".xml");
            }
            else
            {
                metadata = metadata ?? ChooseGame(true);
                if (metadata == null)
                {
                    return null;
                }
                OpenEMEVDFile(fileName, metadata);
            }
            return metadata;
        }

        private bool OpenEMEVDFile(
            string fileName,
            FileMetadata metadata,
            EMEVD evd = null,
            string jsText = null,
            Dictionary<string, string> extraFields = null)
        {
            if (AllEditors.ContainsKey(fileName))
            {
                ShowFile(fileName);
                return true;
            }
            // Can reuse docs if for the same game
            if (!AllDocs.TryGetValue(metadata.GameDocs, out InstructionDocs docs))
            {
                docs = AllDocs[metadata.GameDocs] = new InstructionDocs(metadata.GameDocs);
            }
            ScriptSettings settings = new ScriptSettings(docs, extraFields);
            EventScripter scripter;
            try
            {
                scripter = new EventScripter(fileName, docs, evd);
            }
            catch (Exception ex)
            {
                ScrollDialog.Show(this, ex.ToString());
                return false;
            }

            string fileVersion = ProgramVersion.VERSION;
            bool decompiled = false;
            if (jsText == null)
            {
                decompiled = true;
                try
                {
                    if (metadata.Fancy && docs.Translator != null)
                    {
                        jsText = new FancyEventScripter(scripter, docs, settings.CFGOptions).Unpack();
                    }
                    else
                    {
                        jsText = scripter.Unpack();
                    }
                }
                catch (Exception ex)
                {
                    // Also try to do it in compatibility mode, for emevd files which are no longer allowed, such as changing EMEDFs.
                    try
                    {
                        jsText = scripter.Unpack(compatibilityMode: true);
                    }
                    catch
                    {
                        // If this also fails, we only care about the original exception.
                    }
                    if (jsText == null)
                    {
                        ScrollDialog.Show(this, ex.Message);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine(ex.Message);
                        sb.AppendLine("Proceed anyway? You will have to fix instruction arguments before resaving.");
                        DialogResult result = MessageBox.Show(sb.ToString(), "Error", MessageBoxButtons.YesNoCancel);
                        if (result != DialogResult.Yes)
                        {
                            jsText = null;
                        }
                    }
                    if (jsText == null)
                    {
                        return false;
                    }
                }
            }
            else
            {
                fileVersion = extraFields != null && extraFields.TryGetValue("version", out string version) ? version : null;
            }
            // If properly decompiled, the metadata is reused by the directory sidebar
            if (decompiled)
            {
                DirectoryMetadata[Path.GetDirectoryName(fileName)] = metadata;
            }
            AddAndShowFile(new EditorGUI(SharedControls, scripter, docs, settings, fileVersion, jsText));
            // Notify about possible compatibility issues
            int versionCmp = ProgramVersion.CompareVersions(ProgramVersion.VERSION, fileVersion);
            if (versionCmp > 0)
            {
                SharedControls.SetStatus("Note: File was previously saved using an earlier version of DarkScript3");
            }
            else if (versionCmp < 0)
            {
                SharedControls.SetStatus("Note: File was previously saved using an newer version of DarkScript3. Please update!");
            }
            return true;
        }

        private Dictionary<string, string> GetHeaderValues(string fileText)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            // Some example lines are:
            // ==EMEVD==
            // @docs    sekiro-common.emedf.json
            // @game    Sekiro
            // ...
            // ==/EMEVD==
            Match headerText = Regex.Match(fileText, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==");
            if (headerText.Success)
            {
                string[] result = Regex.Split(headerText.Value, @"(\r\n|\r|\n)\s*");
                foreach (string headerLine in result.ToArray())
                {
                    Match lineMatch = Regex.Match(headerLine, @"^//\s+@(\w+)\s+(.*)");
                    if (lineMatch.Success)
                    {
                        ret[lineMatch.Groups[1].Value] = lineMatch.Groups[2].Value.Trim();
                    }
                }
            }
            return ret;
        }

        // Marked internal for Find in Files functionality. This access can be revoked or modified if necessary.
        internal bool OpenJSFile(string fileName)
        {
            string org = fileName.Substring(0, fileName.Length - 3);
            string text = File.ReadAllText(fileName);
            Dictionary<string, string> headers = GetHeaderValues(text);
            List<string> emevdFileHeaders = new List<string> { "docs", "compress", "game", "string", "linked" };

            EMEVD evd;
            FileMetadata metadata;
            if (emevdFileHeaders.All(name => headers.ContainsKey(name)))
            {
                metadata = new FileMetadata { GameDocs = headers["docs"] };
                string dcx = headers["compress"];
                if (!Enum.TryParse(dcx, out DCX.Type compression))
                {
                    // We also have to account for historical DCX.Type names, which mostly correspond
                    // to DCX.DefaultType.
                    if (Enum.TryParse(dcx, out DCX.DefaultType defaultComp))
                    {
                        compression = (DCX.Type)defaultComp;
                    }
                    else if (dcx == "SekiroKRAK" || dcx == "SekiroDFLT")
                    {
                        // This is turned into SekiroDFLT when it's actually written out. Store it as KRAK in the header
                        // just in case it's supported one day.
                        compression = (DCX.Type)DCX.DefaultType.Sekiro;
                    }
                    else
                    {
                        throw new Exception($"Unknown compression type in file header {headers["compress"]}");
                    }
                }
                if (!Enum.TryParse(headers["game"], out EMEVD.Game game))
                {
                    throw new Exception($"Unknown game type in file header {headers["game"]}");
                }
                string linked = headers["linked"].TrimStart('[').TrimEnd(']');
                evd = new EMEVD()
                {
                    Compression = compression,
                    Format = game,
                    StringData = Encoding.Unicode.GetBytes(headers["string"]),
                    LinkedFileOffsets = Regex.Split(linked, @"\s*,\s*")
                        .Where(o => !string.IsNullOrWhiteSpace(o))
                        .Select(o => long.Parse(o))
                        .ToList()
                };
            }
            else if (!File.Exists(org))
            {
                MessageBox.Show($"{fileName} requires either a corresponding emevd file or JS headers to open");
                return false;
            }
            else
            {
                evd = null;
                metadata = ChooseGame(false);
                if (metadata == null)
                {
                    return false;
                }
            }

            text = Regex.Replace(text, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==", "");
            return OpenEMEVDFile(org, metadata, evd: evd, jsText: text.Trim(), extraFields: headers);
        }

        private void OpenXMLFile(string fileName)
        {
            using (StreamReader reader = new StreamReader(fileName))
            {
                XDocument doc = XDocument.Load(reader);
                string resource = doc.Root.Element("gameDocs").Value;
                string data = doc.Root.Element("script").Value;
                string org = fileName.Substring(0, fileName.Length - 4);
                OpenEMEVDFile(org, new FileMetadata { GameDocs = resource }, jsText: data);
            }
        }

        private FileMetadata ChooseGame(bool showFancy)
        {
            GameChooser chooser = new GameChooser(showFancy);
            chooser.ShowDialog();
            if (chooser.GameDocs == null)
            {
                return null;
            }
            // Check oodle here and when saving
            SharedControls.CheckOodle(chooser.GameDocs);
            return new FileMetadata
            {
                GameDocs = chooser.GameDocs,
                Fancy = chooser.Fancy,
            };
        }

        private class FileMetadata
        {
            public string GameDocs { get; set; }
            public bool Fancy { get; set; }
        }

        internal void LockEditor()
        {
            // Locks everything which isn't registered with SharedControls (called there)
            MainMenuStrip.Enabled = false;
            Cursor = Cursors.WaitCursor;
        }

        internal void UnlockEditor()
        {
            MainMenuStrip.Enabled = true;
            Cursor = Cursors.Default;
        }

        #endregion

        #region Tabs

        // Basic tab management using what we can do with TabControl, which isn't much.
        // TODO: add tab drag and drop (reordering) and close buttons.

        private void closeTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabControl.TabCount > 0)
            {
                RemoveFile(tabControl.SelectedTab.Name);
            }
        }

        private void nextTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabControl.TabCount > 0)
            {
                ShowFile(tabControl.TabPages[(tabControl.SelectedIndex + 1) % tabControl.TabCount].Name);
            }
        }

        private void previousTabToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tabControl.TabCount > 0)
            {
                ShowFile(tabControl.TabPages[(tabControl.SelectedIndex - 1 + tabControl.TabCount) % tabControl.TabCount].Name);
            }
        }

        bool concurrentTabChange = false;
        private void tabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tabControl.TabCount > 0 && !concurrentTabChange)
            {
                ShowFile(tabControl.SelectedTab.Name);
            }
        }

        private bool CancelWithCloseAll()
        {
            while (CurrentEditor != null)
            {
                if (!RemoveFile(CurrentEditor.EMEVDPath))
                {
                    return true;
                }
            }
            return false;
        }

        private void AddAndShowFile(EditorGUI newEditor)
        {
            string filePath = newEditor.EMEVDPath;
            if (!AllEditors.ContainsKey(filePath))
            {
                AllEditors[filePath] = newEditor;
                SharedControls.AddEditor(newEditor);
                newEditor.TitleChanged += EditorGUI_TitleChanged;
            }
            ShowFile(filePath);
        }

        private bool RemoveFile(string filePath)
        {
            if (!AllEditors.TryGetValue(filePath, out EditorGUI editor))
            {
                return true;
            }
            if (editor.CancelWithUnsavedChanges())
            {
                return false;
            }
            int pageIndex = tabControl.TabPages.IndexOfKey(filePath);
            if (tabControl.SelectedIndex == pageIndex)
            {
                display.Panel2.Controls.Clear();
                CurrentEditor = null;
            }
            SharedControls.RemoveEditor(editor);
            AllEditors.Remove(filePath);
            editor.Dispose();

            concurrentTabChange = true;
            tabControl.TabPages.RemoveByKey(filePath);
            concurrentTabChange = false;
            if (tabControl.TabCount == 0)
            {
                tabControl.Visible = false;
                RefreshTitle();
                SharedControls.ResetStatus(true);
                UpdateDirectoryListing();
                display2.BackColor = Color.Transparent;
                display2.IsSplitterFixed = true;
                display.IsSplitterFixed = true;
            }
            else
            {
                int victim = Math.Min(pageIndex, tabControl.TabCount - 1);
                ShowFile(tabControl.TabPages[victim].Name);
            }
            return true;
        }

        internal void ShowFile(string filePath)
        {
            if (!AllEditors.TryGetValue(filePath, out EditorGUI editor))
            {
                return;
            }
            if (CurrentEditor?.EMEVDPath == filePath)
            {
                return;
            }
            display.Panel2.Controls.Clear();
            CurrentEditor = editor;
            SharedControls.SwitchEditor(CurrentEditor);
            display.Panel2.Controls.Add(CurrentEditor);
            // Do tab stuff, including creating one if needed.
            int pageIndex = tabControl.TabPages.IndexOfKey(filePath);
            TabPage page;
            if (pageIndex == -1)
            {
                page = new TabPage
                {
                    Name = filePath,
                    Text = CurrentEditor.DisplayTitle
                };
                tabControl.TabPages.Add(page);
            }
            else
            {
                page = tabControl.TabPages[pageIndex];
            }
            concurrentTabChange = true;
            tabControl.SelectTab(page);
            concurrentTabChange = false;
            tabControl.Visible = true;
            RefreshTitle();
            SharedControls.ResetStatus(true);
            UpdateDirectoryListing(requireDirectoryChange: true);
            display2.BackColor = Color.FromArgb(45, 45, 48);
            display2.IsSplitterFixed = false;
            display.IsSplitterFixed = false;
        }

        private void RefreshTitle()
        {
            Text = CurrentEditor == null ? "DARKSCRIPT 3" : $"DARKSCRIPT 3 - {CurrentEditor.DisplayTitle}";
        }

        private void EditorGUI_TitleChanged(object sender, EventArgs e)
        {
            if (sender is EditorGUI editor)
            {
                int pageIndex = tabControl.TabPages.IndexOfKey(editor.EMEVDPath);
                if (pageIndex != -1)
                {
                    tabControl.TabPages[pageIndex].Text = editor.DisplayTitle;
                }
            }
            RefreshTitle();
        }

        private Dictionary<string, string> LoadMapNames(string emedfName)
        {
            // Rough system to have named maps.
            // Null and empty dictionaries are meant to be interchangeable here.
            Dictionary<string, string> mapNames = null;
            if (emedfName.EndsWith(".emedf.json"))
            {
                string mapFile = Regex.Replace(emedfName, @"\.emedf\.json$", ".MapName.txt");
                AllMapNames.TryGetValue(mapFile, out mapNames);
                if (mapNames == null)
                {
                    AllMapNames[mapFile] = mapNames = new Dictionary<string, string>();
                    try
                    {
                        // Best-effort
                        string mapText = Resource.Text(mapFile);
                        foreach (string line in mapText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                        {
                            string[] parts = line.Split(new[] { ' ' }, 2);
                            if (parts.Length == 2)
                            {
                                mapNames[parts[0]] = parts[1];
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Resource not exist, usually
                    }
                }
            }
            return mapNames;
        }

        private void UpdateDirectoryListing(bool requireDirectoryChange = false)
        {
            string directory = null;
            Dictionary<string, string> mapNames = null;
            if (CurrentEditor != null)
            {
                directory = Path.GetDirectoryName(CurrentEditor.EMEVDPath);
                if (!Directory.Exists(directory))
                {
                    directory = null;
                }
                mapNames = LoadMapNames(CurrentEditor.ResourceString);
            }
            if (requireDirectoryChange && directory == CurrentDirectory)
            {
                return;
            }
            SortedSet<string> allFiles = new SortedSet<string>();
            HashSet<string> jsFiles = new HashSet<string>();
            string[] dirFiles = directory == null ? new string[] { } : Directory.GetFiles(directory);
            foreach (string path in dirFiles)
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith(".emevd") || name.EndsWith(".emevd.dcx"))
                {
                    allFiles.Add(name);
                }
                else if (name.EndsWith(".emevd.js") || name.EndsWith(".emevd.dcx.js"))
                {
                    name = name.Substring(0, name.Length - 3);
                    allFiles.Add(name);
                    jsFiles.Add(name);
                }
            }
            fileView.BeginUpdate();
            fileView.Nodes.Clear();
            fileView.BackColor = TextStyles.BackColor;
            fileView.ForeColor = TextStyles.ForeColor;
            foreach (string file in allFiles)
            {
                string fullText = file;
                if (mapNames != null && mapNames.Count > 0)
                {
                    string matchText = file.Split(new[] { '.' }, 2)[0];
                    if (mapNames.TryGetValue(matchText, out string mapName))
                    {
                        fullText = $"{file} <{mapName}>";
                    }
                }
                TreeNode node = new TreeNode(fullText);
                node.Tag = file;
                node.ForeColor = jsFiles.Contains(file) ? TextStyles.ForeColor : (TextStyles.Comment.ForeBrush as SolidBrush).Color;
                fileView.Nodes.Add(node);
            }
            fileView.Sort();
            fileView.EndUpdate();
            if (directory == null)
            {
                Watcher.EnableRaisingEvents = false;
            }
            else
            {
                Watcher.Path = directory;
                Watcher.EnableRaisingEvents = true;
            }
            CurrentDirectory = directory;
        }

        private void OnDirectoryContentsChanged(object sender, FileSystemEventArgs e)
        {
            string file = e.FullPath;
            if (file.EndsWith(".emevd") || file.EndsWith(".emevd.dcx") || file.EndsWith(".emevd.js") || file.EndsWith(".emevd.dcx.js"))
            {
                UpdateDirectoryListing();
            }
        }

        private void fileView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (CurrentDirectory == null)
            {
                return;
            }
            string text = e.Node.Text;
            if (e.Node.Tag is string tagText)
            {
                // Tag will contain filename when present, when visual text has extra annotations
                text = tagText;
            }
            string emevdPath = Path.Combine(CurrentDirectory, text);
            if (File.Exists(emevdPath))
            {
                DirectoryMetadata.TryGetValue(CurrentDirectory, out FileMetadata metadata);
                OpenFile(emevdPath, metadata);
            }
            else
            {
                UpdateDirectoryListing();
            }
        }

        private void Display_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized && display.SplitterDistance != 350)
                display.SplitterDistance = 350;
        }

        private void GUI_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                SharedControls.HideTip();
            }
        }

        #endregion

        #region Menus

        private void EmevdDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SharedControls.HideTip();
            CurrentEditor?.ShowEMEVDData();
        }

        private void RefreshGlobalStyles()
        {
            SharedControls.RefreshGlobalStyles();
            fileView.BackColor = TextStyles.BackColor;
            // Incidentally sets directory listing styles
            UpdateDirectoryListing();
        }

        private void customizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TextStyles.ShowStyleEditor())
            {
                RefreshGlobalStyles();
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CancelWithCloseAll())
            {
                return;
            }
            Close();
        }

        private void GUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CancelWithCloseAll())
            {
                e.Cancel = true;
            }
        }

        private void DocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SharedControls.HideTip();
            display.Panel1Collapsed = !display.Panel1Collapsed;
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.Cut();

        private void ReplaceToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowReplaceDialog();

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.SelectAll();

        private void FindToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowFindDialog(false);

        private void findInFilesToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowFindDialog(true);

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.Copy();

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"Version {ProgramVersion.VERSION}"
                + "\r\n-- Created by AinTunez\r\n-- MattScript and other features by thefifthmatt"
                + "\r\n-- Based on work by HotPocketRemix and TKGP\r\n-- Special thanks to Meowmaritus", "About DarkScript");
        }

        private void batchDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Title = "Open (note: skips existing JS files)";
            ofd.Multiselect = true;
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                FileMetadata metadata = ChooseGame(true);
                if (metadata == null) return;
                List<string> succeeded = new List<string>();
                List<string> failed = new List<string>();
                foreach (var fileName in ofd.FileNames)
                {
                    if (File.Exists(fileName + ".js"))
                    {
                        continue;
                    }
                    try
                    {
                        bool wasOpen = AllEditors.ContainsKey(fileName);
                        if (OpenEMEVDFile(fileName, metadata))
                        {
                            CurrentEditor.SaveJSFile();
                            succeeded.Add(fileName);
                            if (!wasOpen)
                            {
                                RemoveFile(CurrentEditor.EMEVDPath);
                            }
                            continue;
                        }
                    }
                    catch
                    {
                    }
                    failed.Add(fileName);
                }

                List<string> lines = new List<string>();
                if (failed.Count > 0)
                {
                    lines.Add("The following emevds failed to be dumped to JS:" + Environment.NewLine);
                    lines.AddRange(failed);
                    lines.Add("");
                }
                if (succeeded.Count > 0)
                {
                    lines.Add("The following emevds were dumped to JS:" + Environment.NewLine);
                    lines.AddRange(succeeded);
                }
                ScrollDialog.Show(this, string.Join(Environment.NewLine, lines));
            }
        }

        private void batchResaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "EMEVD Files|*.emevd.js; *.emevd.dcx.js";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                List<string> succeeded = new List<string>();
                List<string> failed = new List<string>();
                foreach (var fileName in ofd.FileNames)
                {
                    try
                    {
                        bool wasOpen = AllEditors.ContainsKey(fileName);
                        if (OpenJSFile(fileName) && CurrentEditor.SaveJSAndEMEVDFile())
                        {
                            succeeded.Add(fileName);
                            if (!wasOpen)
                            {
                                RemoveFile(CurrentEditor.EMEVDPath);
                            }
                            continue;
                        }
                    }
                    catch
                    {
                    }
                    failed.Add(fileName);
                }

                List<string> lines = new List<string>();
                if (failed.Count > 0)
                {
                    lines.Add("The following JS files failed to be saved:" + Environment.NewLine);
                    lines.AddRange(failed);
                    lines.Add("");
                }
                if (succeeded.Count > 0)
                {
                    lines.Add("The following JS files were saved:" + Environment.NewLine);
                    lines.AddRange(succeeded);
                }
                ScrollDialog.Show(this, string.Join(Environment.NewLine, lines));
            }
        }

        private void openAutoCompleteMenuToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowInstructionMenu();

        private void scriptCompilationSettingsToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowScriptSettings();

        private void decompileToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowPreviewDecompile();

        private void previewCompilationOutputToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowPreviewCompile();

        private void goToEventIDUnderCursorToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.JumpToEvent();

        private void replaceFloatUnderCursorToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ReplaceFloat();

        private void viewEMEDFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string name = CurrentEditor?.GetDocName();
            if (name == null)
            {
                MessageBox.Show("Open a file to view the EMEDF for that game\r\nor access it in the Resources directory.");
                return;
            }
            string path = $"{name}-emedf.html";
            if (File.Exists(path))
                System.Diagnostics.Process.Start(path);
            else if (File.Exists($@"Resources\{path}"))
                System.Diagnostics.Process.Start($@"Resources\{path}");
            else
                MessageBox.Show($"No EMEDF documentation found named {path}");
        }

        private void viewEMEVDTutorialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://soulsmodding.wikidot.com/tutorial:learning-how-to-use-emevd");
        }

        private void viewFancyDocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://soulsmodding.wikidot.com/tutorial:mattscript-documentation");
        }

        private void viewEldenRingEMEVDTutorialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("http://soulsmodding.wikidot.com/tutorial:intro-to-elden-ring-emevd");
        }

        private void checkForDarkScript3UpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/AinTunez/DarkScript3/releases");
        }

        #endregion

        #region Window Position

        #endregion

        // https://stackoverflow.com/questions/937298/restoring-window-size-position-with-multiple-monitors
        // However, don't remember maximized states. These are difficult to adapt to changing desktop
        // monitor configurations, and are quite easy to do manually.
        private void GUI_Load(object sender, EventArgs e)
        {
            InitializeWindow();
        }

        private void GUI_Move(object sender, EventArgs e)
        {
            TrackWindowState();
        }

        private void GUI_Resize(object sender, EventArgs e)
        {
            TrackWindowState();
        }

        private bool windowInitialized = false;

        private void InitializeWindow()
        {
            bool isVisible(Rectangle rect) => Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(rect));
            Rectangle stored = Settings.Default.WindowPosition;
            if (stored != Rectangle.Empty && isVisible(stored))
            {
                StartPosition = FormStartPosition.Manual;
                DesktopBounds = stored;
            }
            // Otherwise, don't try to apply the old settings.
            // We could use the size info but it may not fit if the monitor setup was changed.
            windowInitialized = true;
        }

        private void TrackWindowState()
        {
            if (!windowInitialized) { return; }

            if (WindowState == FormWindowState.Normal)
            {
                Settings.Default.WindowPosition = DesktopBounds;
                Settings.Default.Save();
            }
        }
    }
}
