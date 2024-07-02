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
using System.ComponentModel;
using System.Diagnostics;

namespace DarkScript3
{
    public partial class GUI : Form
    {
        private readonly SharedControls SharedControls;
        private readonly ContextMenuStrip FileBrowserContextMenu;
        private readonly Dictionary<string, InstructionDocs> AllDocs = new Dictionary<string, InstructionDocs>();
        private readonly Dictionary<string, EditorGUI> AllEditors = new Dictionary<string, EditorGUI>();
        private readonly Dictionary<string, FileMetadata> DirectoryMetadata = new Dictionary<string, FileMetadata>();
        private readonly Dictionary<string, ProjectSettingsFile> DirectoryProjects = new Dictionary<string, ProjectSettingsFile>();
        private readonly NameMetadata NameMetadata = new NameMetadata();
        private readonly FileSystemWatcher Watcher;
        private readonly FileSystemWatcher GameWatcher;
        private ProjectSettingsFile CurrentDefaultProject;
        private EditorGUI CurrentEditor;
        private string CurrentDirectory;
        private bool ManuallySelectedProject;

        public GUI()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
            // Various rendering and data setup
            menuStrip.Renderer = new DarkToolStripRenderer();
            statusStrip.Renderer = new DarkToolStripRenderer();
            TextStyles.LoadColors();
            SharedControls = new SharedControls(this, statusLabel, docBox, NameMetadata);
            SharedControls.ResetStatus(true);
            SharedControls.BFF.Owner = this;
            // Set up right-click menu
            FileBrowserContextMenu = new ContextMenuStrip();
            FileBrowserContextMenu.Renderer = new DarkToolStripRenderer();
            FileBrowserContextMenu.Opening += new CancelEventHandler(FileBrowserContextMenu_Opening);
            fileView.ContextMenuStrip = FileBrowserContextMenu;
            // Prevent fuzzy line from showing up. Tab key is handled by the textbox in any case.
            display.TabStop = false;
            display2.TabStop = false;
            Controls.Add(SharedControls.InfoTip);
            tabControl.Visible = false;
            Watcher = MakeFileSystemWatcher();
            GameWatcher = MakeFileSystemWatcher();
            // This updates the directory listing, which uses Watchers
            RefreshGlobalStyles();
        }

        private FileSystemWatcher MakeFileSystemWatcher()
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Created += OnDirectoryContentsChanged;
            watcher.Deleted += OnDirectoryContentsChanged;
            watcher.Renamed += OnDirectoryContentsChanged;
            watcher.EnableRaisingEvents = false;
            watcher.SynchronizingObject = this;
            return watcher;
        }

        private void GUI_Load(object sender, EventArgs e)
        {
            InitializeWindow();
            RefreshTitle();
            // Normalize all fonts at this point
            SharedControls.SetGlobalFont(TextStyles.Font);
            // Ad-hoc way of doing settings, as tool menus (TODO: find something more permanent?)
            // Note this may call CheckChanged, which could have side effects
            showTooltipsToolStripMenuItem.Checked = Settings.Default.DisplayTooltips;
            showArgumentsInTooltipToolStripMenuItem.Checked = Settings.Default.ArgTooltip;
            showArgumentsInPanelToolStripMenuItem.Checked = Settings.Default.ArgDocbox;

            connectToolStripMenuItem.Checked = Settings.Default.UseSoapstoneDSMS;
            useSmithboxForMetadataToolStripMenuItem.Checked = Settings.Default.UseSoapstoneSmithbox;

            // Update versions
            string previousVersion = Settings.Default.Version;
            if (!string.IsNullOrEmpty(previousVersion))
            {
                // Can check for default values here, using ProgramVersion.CompareVersions("3.x.x", previousVersion) > 0
            }
            if (previousVersion != ProgramVersion.VERSION)
            {
                Settings.Default.Version = ProgramVersion.VERSION;
                Settings.Default.Save();
            }
            // Load projects
            string projectJson = Settings.Default.ProjectJson;
            if (!string.IsNullOrEmpty(projectJson)
                && File.Exists(projectJson)
                && Path.GetFileName(projectJson) == "project.json")
            {
                try
                {
                    LoadProject(projectJson);
                }
                catch (Exception ex)
                {
                    SharedControls.SetStatus(ex.Message);
                }
            }
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
            else if (CurrentDefaultProject != null && ManuallySelectedProject)
            {
                // Allow using manually seiected project, if one is displayed.
                // Only do this if manually selected, as currently loaded projects are sticky (stored in settings).
                string projectDir = CurrentDefaultProject.ProjectEventDirectory;
                if (Directory.Exists(projectDir))
                {
                    ofd.InitialDirectory = projectDir;
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

        private void openProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!openProjectToolStripMenuItem.Enabled)
            {
                return;
            }
            SharedControls.HideTip();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "DSMapStudio Project File|project.json|All files|*.*";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string projectJson = ofd.FileName;
            if (Path.GetFileName(projectJson) != "project.json")
            {
                return;
            }
            try
            {
                LoadProject(projectJson);
                ManuallySelectedProject = true;
                Settings.Default.ProjectJson = projectJson;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                ScrollDialog.Show(this, ex.ToString());
            }
        }

        private void clearProjectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CurrentDefaultProject = null;
            Settings.Default.ProjectJson = "";
            Settings.Default.Save();
            UpdateDirectoryListing();
            RefreshTitle();
            SharedControls.ResetStatus(true);
            clearProjectToolStripMenuItem.Enabled = false;
        }

        private void LoadProject(string projectJson)
        {
            // projectJson should be a valid project file. Callers should handle exceptions.
            CurrentDefaultProject = ProjectSettingsFile.LoadProjectFile(projectJson);
            if (CurrentDefaultProject.ProjectEventDirectory != null)
            {
                DirectoryProjects[CurrentDefaultProject.ProjectEventDirectory] = CurrentDefaultProject;
            }
            UpdateDirectoryListing();
            RefreshTitle();
            SharedControls.SetStatus($"Loaded {projectJson}");
            clearProjectToolStripMenuItem.Enabled = true;
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
            HeaderData headerData = null,
            string loadPath = null)
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
            ScriptSettings settings = new ScriptSettings(docs, headerData?.ExtraSettings);
            EventScripter scripter;
            try
            {
                scripter = new EventScripter(fileName, docs, evd, loadPath);
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
                fileVersion = headerData?.Version;
            }
            // If properly decompiled, the metadata is reused by the directory sidebar
            string fileDir = Path.GetDirectoryName(fileName);
            if (decompiled)
            {
                DirectoryMetadata[fileDir] = metadata;
            }
            if (ProjectSettingsFile.TryGetEmevdFileProject(fileName, out ProjectSettingsFile projectFile))
            {
                DirectoryProjects[fileDir] = projectFile;
            }
            else
            {
                DirectoryProjects.Remove(fileDir);
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

        // Marked internal for Find in Files functionality. This access can be revoked or modified if necessary.
        internal bool OpenJSFile(string fileName)
        {
            string org = fileName.Substring(0, fileName.Length - 3);
            string text = File.ReadAllText(fileName);

            EMEVD evd;
            FileMetadata metadata;
            if (HeaderData.Read(text, out HeaderData headerData))
            {
                metadata = new FileMetadata { GameDocs = headerData.GameDocs };
                evd = new EMEVD()
                {
                    Compression = headerData.Compression,
                    Format = headerData.Game,
                    StringData = headerData.StringData,
                    LinkedFileOffsets = headerData.LinkedFileOffsets,
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

            text = HeaderData.Trim(text);
            return OpenEMEVDFile(org, metadata, evd: evd, jsText: text.Trim(), headerData: headerData);
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
                // For now, project only makes sense when no tabs are open
                openProjectToolStripMenuItem.Enabled = true;
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
            // For now, project only makes sense when no tabs are open
            openProjectToolStripMenuItem.Enabled = false;
        }

        private void RefreshTitle()
        {
            string name = $"DARKSCRIPT {ProgramVersion.VERSION}";
            if (CurrentEditor != null)
            {
                Text = $"{name} - {CurrentEditor.DisplayTitleWithDir}";
            }
            else if (CurrentDefaultProject?.ProjectEventDirectory != null)
            {
                Text = $"{name} - {CurrentDefaultProject?.ProjectEventDirectory}";
            }
            else
            {
                Text = name;
            }
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

        private void UpdateDirectoryListing(bool requireDirectoryChange = false)
        {
            string directory = null;
            ProjectSettingsFile project = null;
            Dictionary<string, string> mapNames = null;
            if (CurrentEditor != null)
            {
                directory = Path.GetDirectoryName(CurrentEditor.EMEVDPath);
                if (!Directory.Exists(directory))
                {
                    directory = null;
                }
                if (directory != null)
                {
                    DirectoryProjects.TryGetValue(directory, out project);
                }
                mapNames = NameMetadata.GetMapNames(CurrentEditor.ResourceString);
            }
            else if (CurrentDefaultProject != null)
            {
                project = CurrentDefaultProject;
                directory = project.ProjectEventDirectory;
                mapNames = NameMetadata.GetMapNames(project.ResourcePrefix);
            }
            // Optionally, don't change if just switching tabs, to avoid losing place
            if (requireDirectoryChange && directory == CurrentDirectory)
            {
                return;
            }
            string gameDir = project?.GameEventDirectory;
            SortedSet<string> allFiles = new SortedSet<string>();
            HashSet<string> baseFiles = new HashSet<string>();
            HashSet<string> jsFiles = new HashSet<string>();
            if (directory != null)
            {
                // directory need not exist, as gameDir might exist
                string[] dirFiles = Directory.Exists(directory) ? Directory.GetFiles(directory) : Array.Empty<string>();
                foreach (string path in dirFiles)
                {
                    string name = Path.GetFileName(path);
                    if (name.EndsWith(".emevd") || name.EndsWith(".emevd.dcx"))
                    {
                        allFiles.Add(name);
                        baseFiles.Add(name);
                    }
                    else if (name.EndsWith(".emevd.js") || name.EndsWith(".emevd.dcx.js"))
                    {
                        name = name.Substring(0, name.Length - 3);
                        allFiles.Add(name);
                        jsFiles.Add(name);
                    }
                }
                string[] gameFiles = gameDir != null && Directory.Exists(gameDir) ? Directory.GetFiles(gameDir) : Array.Empty<string>();
                foreach (string path in gameFiles)
                {
                    string name = Path.GetFileName(path);
                    if (name.EndsWith(".emevd") || name.EndsWith(".emevd.dcx"))
                    {
                        allFiles.Add(name);
                    }
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
                FileBrowserTag tag = new FileBrowserTag
                {
                    BaseName = file,
                    LocalPath = Path.Combine(directory, file),
                };
                if (gameDir != null)
                {
                    tag.GamePath = Path.Combine(gameDir, file);
                }
                node.Tag = tag;
                if (jsFiles.Contains(file))
                {
                    node.ForeColor = TextStyles.ForeColor;
                }
                else if (baseFiles.Contains(file))
                {
                    node.ForeColor = (TextStyles.Comment.ForeBrush as SolidBrush).Color;
                }
                else
                {
                    node.ForeColor = (TextStyles.String.ForeBrush as SolidBrush).Color;
                }
                fileView.Nodes.Add(node);
            }
            fileView.Sort();
            fileView.EndUpdate();
            SetWatcherDirectory(Watcher, directory);
            SetWatcherDirectory(GameWatcher, gameDir);
            CurrentDirectory = directory;
        }

        private class FileBrowserTag
        {
            // map.emevd.dcx
            public string BaseName { get; set; }
            // currentdir/map.emevd.dcx
            public string LocalPath { get; set; }
            // gamedir/map.emevd.dcx
            public string GamePath { get; set; }
        }

        private void SetWatcherDirectory(FileSystemWatcher watcher, string directory)
        {
            if (directory != null && Directory.Exists(directory))
            {
                watcher.Path = directory;
                watcher.EnableRaisingEvents = true;
            }
            else
            {
                watcher.EnableRaisingEvents = false;
            }
        }

        private void OnDirectoryContentsChanged(object sender, FileSystemEventArgs e)
        {
            string file = e.FullPath;
            if (file.EndsWith(".emevd") || file.EndsWith(".emevd.dcx") || file.EndsWith(".emevd.js") || file.EndsWith(".emevd.dcx.js"))
            {
                UpdateDirectoryListing();
            }
        }

        private void fileView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            fileView.SelectedNode = e.Node;
            // Update this whenever possible. The Opening event should follow this on right-click.
            FileBrowserContextMenu.Tag = e.Node;
        }

        private void fileView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            if (e.Node.Tag is not FileBrowserTag tag)
            {
                return;
            }
            OpenFileBrowserTag(tag);
        }

        private void OpenFileBrowserTag(FileBrowserTag tag)
        {
            if (CurrentDirectory == null)
            {
                return;
            }
            DirectoryMetadata.TryGetValue(CurrentDirectory, out FileMetadata metadata);
            string jsPath = $"{tag.LocalPath}.js";
            if (File.Exists(jsPath))
            {
                OpenFile(jsPath, metadata);
            }
            else if (File.Exists(tag.LocalPath))
            {
                OpenFile(tag.LocalPath, metadata);
            }
            else if (tag.GamePath != null && File.Exists(tag.GamePath))
            {
                string gameFile = tag.GamePath;
                // Try to select vanilla versions by selecting bak file
                if (File.Exists(gameFile + ".bak"))
                {
                    gameFile = gameFile + ".bak";
                }
                metadata = metadata ?? ChooseGame(true);
                if (metadata == null)
                {
                    return;
                }
                // At least make sure the directory exists, so opening files works, and saving later on
                string localDir = Path.GetDirectoryName(tag.LocalPath);
                if (!Directory.Exists(localDir))
                {
                    Directory.CreateDirectory(localDir);
                    // Could also unconditionally refresh listing, but this should do it indirectly
                    SetWatcherDirectory(Watcher, localDir);
                }
                OpenEMEVDFile(tag.LocalPath, metadata, loadPath: gameFile);
            }
            else
            {
                // Make it disappear
                UpdateDirectoryListing();
            }
        }

        private async Task OpenFileBrowserMap(string game, string map)
        {
            try
            {
                await SharedControls.Metadata.OpenMap(game, map);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, $"Could not open {map}", MessageBoxButtons.OK);
            }
        }

        private void FileBrowserContextMenu_Opening(object sender, CancelEventArgs e)
        {
            if (FileBrowserContextMenu.Tag is not TreeNode treeNode || treeNode.Tag is not FileBrowserTag tag)
            {
                return;
            }
            FileBrowserContextMenu.Tag = null;
            FileBrowserContextMenu.Items.Clear();
            ToolStripMenuItem eventItem = new ToolStripMenuItem($"Load {tag.BaseName}");
            // It may be worth noting existing shortcuts if more things are added to this menu.
            // eventItem.ShortcutKeyDisplayString = "Double-click in menu";
            eventItem.Click += (sender, e) => OpenFileBrowserTag(tag);
            FileBrowserContextMenu.Items.Add(eventItem);
            if (tag.BaseName.StartsWith("m") && CurrentDirectory != null)
            {
                // Try to freshly infer game name here. Use project first, as DSMapStudio also uses it.
                // If there is a mismatch between files in this directory, or between this project.json and the open one,
                // there is not much we can do.
                string gameStr = null;
                if (DirectoryProjects.TryGetValue(CurrentDirectory, out ProjectSettingsFile project) && project.ResourcePrefix != null)
                {
                    gameStr = project.ResourcePrefix;
                }
                else if (DirectoryMetadata.TryGetValue(CurrentDirectory, out FileMetadata metadata) && metadata.GameDocs != null)
                {
                    gameStr = InstructionDocs.GameNameFromResourceName(Path.GetFileName(metadata.GameDocs));
                }
                if (gameStr != null)
                {
                    string mapName = tag.BaseName.Split('.')[0];

                    var name = "DSMapStudio";

                    if (DarkScript3.Properties.Settings.Default.UseSoapstoneSmithbox)
                    {
                        name = "Smithbox";
                    }

                    ToolStripMenuItem mapItem = new ToolStripMenuItem($"Load {mapName} in {name}");
                    mapItem.Click += async (sender, e) => await OpenFileBrowserMap(gameStr, mapName);
                    FileBrowserContextMenu.Items.Add(mapItem);
                }
            }
            e.Cancel = false;
        }

        private void ContextEmevdToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void ContextMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }

        private void Display_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized && display.SplitterDistance != 350)
            {
                display.SplitterDistance = 350;
            }
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

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.Copy();

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.Paste();

        private void ReplaceToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowReplaceDialog();

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.SelectAll();

        private void FindToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowFindDialog(false);

        private void findInFilesToolStripMenuItem_Click(object sender, EventArgs e) => CurrentEditor?.ShowFindDialog(true);

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
            string suffix = "";
            // Unfortunately this doesn't work with Process.Start, since the file needs to exist
            // if (CurrentEditor != null && CurrentEditor.MayBeFancy()) suffix = "?hidecond=1";
            string getFullPath(string p)
            {
                // return "file://" + Path.GetFullPath(p) + suffix;
                return p + suffix;
            }
            if (File.Exists(path))
                OpenURL(getFullPath(path));
            else if (File.Exists($@"Resources\{path}"))
                OpenURL(getFullPath($@"Resources\{path}"));
            else
                MessageBox.Show($"No EMEDF documentation found named {path}");
        }

        private void OpenURL(string url)
        {
            Console.WriteLine(url);
            // https://stackoverflow.com/questions/21835891/process-starturl-fails
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private void viewEMEVDTutorialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenURL("http://soulsmodding.wikidot.com/tutorial:learning-how-to-use-emevd");
        }

        private void viewFancyDocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenURL("http://soulsmodding.wikidot.com/tutorial:mattscript-documentation");
        }

        private void viewEldenRingEMEVDTutorialToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenURL("http://soulsmodding.wikidot.com/tutorial:intro-to-elden-ring-emevd");
        }

        private void checkForDarkScript3UpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenURL("https://github.com/AinTunez/DarkScript3/releases");
        }
        private void showTooltipsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.DisplayTooltips = showTooltipsToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private void showArgumentTooltipsToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ArgTooltip = showArgumentsInTooltipToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        private void showArgumentsInPanelToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            Settings.Default.ArgDocbox = showArgumentsInPanelToolStripMenuItem.Checked;
            Settings.Default.Save();
        }

        // DSMapStudio
        private void connectToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if(connectToolStripMenuItem.Checked)
            {
                useSmithboxForMetadataToolStripMenuItem.Checked = false;
                Settings.Default.UseSoapstoneDSMS = true;
                Settings.Default.UseSoapstoneSmithbox = false;
            }
            else
            {
                useSmithboxForMetadataToolStripMenuItem.Checked = true;
                Settings.Default.UseSoapstoneDSMS = false;
                Settings.Default.UseSoapstoneSmithbox = true;
            }

            Settings.Default.Save();
            SoapstoneMetadata metadata = SharedControls?.Metadata;
            if (metadata != null && metadata.IsOpenable())
            {
                if (Settings.Default.UseSoapstoneDSMS)
                {
                    metadata.Open();
                }
                else
                {
                    metadata.Close();
                }
            }
        }

        // Smithbox
        private void useSmithboxForMetadataToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            if (useSmithboxForMetadataToolStripMenuItem.Checked)
            {
                connectToolStripMenuItem.Checked = false;
                Settings.Default.UseSoapstoneDSMS = false;
                Settings.Default.UseSoapstoneSmithbox = true;
            }
            else
            {
                connectToolStripMenuItem.Checked = true;
                Settings.Default.UseSoapstoneDSMS = true;
                Settings.Default.UseSoapstoneSmithbox = false;
            }

            Settings.Default.Save();
            SoapstoneMetadata metadata = SharedControls?.Metadata;
            if (metadata != null && metadata.IsOpenable())
            {
                if (Settings.Default.UseSoapstoneSmithbox)
                {
                    metadata.Open();
                }
                else
                {
                    metadata.Close();
                }
            }
        }

        private void showConnectionInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StringBuilder sb = new StringBuilder();

            var name = "DSMapStudio";
            if (Settings.Default.UseSoapstoneSmithbox)
            {
                name = "Smithbox";
            }

            if (Settings.Default.UseSoapstoneDSMS || Settings.Default.UseSoapstoneSmithbox)
            {
                sb.AppendLine($"{name} connectivity is enabled.");
                sb.AppendLine();
                sb.AppendLine($"When {name} is open and Settings > Soapstone Server is enabled, "
                    + $"data from {name} will be used to autocomplete values from params, FMGs, and loaded maps. "
                    + "You can also hover on numbers in DarkScript3 to get tooltip info, "
                    + $"and right-click on the tooltip to open it in {name}.");
            }
            else
            {
                sb.AppendLine("Cross-reference connectivity is disabled.");
                sb.AppendLine();
                if (connectToolStripMenuItem.Enabled)
                {
                    sb.AppendLine($"Select \"{connectToolStripMenuItem.Text}\" to enable it.");
                }
                else
                {
                    sb.AppendLine($"Restart DarkScript3 and select \"{connectToolStripMenuItem.Text}\" to enable it.");
                }
            }

            sb.AppendLine();
            SoapstoneMetadata metadata = SharedControls.Metadata;
            string portStr = metadata.LastPort is int port ? $"{port}" : "None";
            sb.AppendLine($"Server port: {portStr}");
            sb.AppendLine($"Detected game: {metadata.CurrentGameString ?? "None"}");
            sb.AppendLine($"Client state: {metadata.State}");
            sb.AppendLine();
            sb.AppendLine(metadata.LastLoopResult ?? "No requests sent");
            ScrollDialog.Show(this, sb.ToString(), "Soapstone Server Info");
        }

        private void clearMetadataCacheToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SharedControls.Metadata.ResetData();

            var name = "DSMapStudio";
            if (Settings.Default.UseSoapstoneSmithbox)
            {
                name = "Smithbox";
            }

            ScrollDialog.Show(this,
                $"Metadata cache cleared. Names and autocomplete items will be refetched from {name} when connected."
                    + "\n\n(This may be supported automatically in the future, if that would be helpful.)",
                "Cleared cached metadata");
        }

        #endregion

        #region Window Position

        #endregion

        // https://stackoverflow.com/questions/937298/restoring-window-size-position-with-multiple-monitors
        // However, don't remember maximized states. These are difficult to adapt to changing desktop
        // monitor configurations, and are quite easy to do manually.

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
