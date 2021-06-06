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

namespace DarkScript3
{
    public partial class GUI : Form
    {
        private readonly SharedControls SharedControls;
        private readonly Dictionary<string, InstructionDocs> AllDocs;
        private EditorGUI Editor;

        public GUI()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
            menuStrip.Renderer = new DarkToolStripRenderer();
            statusStrip.Renderer = new DarkToolStripRenderer();
            TextStyles.LoadColors();
            SharedControls = new SharedControls(this, statusLabel, docBox);
            SharedControls.RefreshGlobalStyles();
            SharedControls.ResetStatus(true);
            SharedControls.BFF.Owner = this;
            // Prevent fuzzy line from showing up. Tab is handled by the textbox in any case.
            display.TabStop = false;
            Controls.Add(SharedControls.InfoTip);
            AllDocs = new Dictionary<string, InstructionDocs>();
        }

        #region File Handling

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.SaveJSAndEMEVDFile();

        private bool CancelWithUnsavedChanges()
        {
            return Editor?.CancelWithUnsavedChanges() ?? false;
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SharedControls.HideTip();
            if (CancelWithUnsavedChanges())
            {
                return;
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx|EMEVD JS Files|*.emevd.js; *.emevd.dcx.js|All files|*.*";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            if (ofd.FileName.EndsWith(".js"))
            {
                OpenJSFile(ofd.FileName);
            }
            else if (File.Exists(ofd.FileName + ".js"))
            {
                OpenJSFile(ofd.FileName + ".js");
            }
            else if (ofd.FileName.EndsWith(".xml"))
            {
                OpenXMLFile(ofd.FileName);
            }
            else if (File.Exists(ofd.FileName + ".xml"))
            {
                OpenXMLFile(ofd.FileName + ".xml");
            }
            else
            {
                string game = ChooseGame(out bool fancy);
                if (game == null) return;
                OpenEMEVDFile(ofd.FileName, game, isFancy: fancy);
            }
        }

        private bool OpenEMEVDFile(
            string fileName,
            string gameDocs,
            EMEVD evd = null,
            string jsText = null,
            bool isFancy = false,
            Dictionary<string, string> extraFields = null)
        {
            // Can reuse docs if for the same game
            if (!AllDocs.TryGetValue(gameDocs, out InstructionDocs docs))
            {
                docs = AllDocs[gameDocs] = new InstructionDocs(gameDocs);
            }
            ScriptSettings settings = new ScriptSettings(docs, extraFields);
            EventScripter scripter = new EventScripter(fileName, docs, evd);

            string fileVersion = ProgramVersion.VERSION;
            if (jsText == null)
            {
                try
                {
                    if (isFancy && docs.Translator != null)
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
                        MessageBox.Show(ex.Message);
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
            if (Editor != null)
            {
                display.Panel2.Controls.Clear();
                SharedControls.RemoveEditor(Editor);
                Editor.Dispose();
            }
            Editor = new EditorGUI(SharedControls, scripter, docs, settings, fileVersion, jsText);
            SharedControls.AddEditor(Editor);
            SharedControls.RefreshGlobalStyles();
            display.Panel2.Controls.Add(Editor);
            // PerformLayout();
            Text = $"DARKSCRIPT 3 - {scripter.FileName}";
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

        private bool OpenJSFile(string fileName)
        {
            string org = fileName.Substring(0, fileName.Length - 3);
            string text = File.ReadAllText(fileName);
            Dictionary<string, string> headers = GetHeaderValues(text);
            List<string> emevdFileHeaders = new List<string> { "docs", "compress", "game", "string", "linked" };

            EMEVD evd;
            string docs;
            if (emevdFileHeaders.All(name => headers.ContainsKey(name)))
            {
                docs = headers["docs"];
                if (!Enum.TryParse(headers["compress"], out DCX.Type compression))
                {
                    // TODO look at SekiroDFLT
                    if (Enum.TryParse(headers["compress"], out DCX.DefaultType defaultComp))
                    {
                        compression = (DCX.Type)defaultComp;
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
                docs = ChooseGame();
                if (docs == null) return false;
            }

            text = Regex.Replace(text, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==", "");
            return OpenEMEVDFile(org, docs, evd: evd, jsText: text.Trim(), extraFields: headers);
        }

        private string ChooseGame(out bool fancy)
        {
            GameChooser chooser = new GameChooser(true);
            chooser.ShowDialog();
            fancy = chooser.Fancy;
            return chooser.GameDocs;
        }

        private string ChooseGame()
        {
            GameChooser chooser = new GameChooser(false);
            chooser.ShowDialog();
            return chooser.GameDocs;
        }

        private void OpenXMLFile(string fileName)
        {
            using (StreamReader reader = new StreamReader(fileName))
            {
                XDocument doc = XDocument.Load(reader);
                string resource = doc.Root.Element("gameDocs").Value;
                string data = doc.Root.Element("script").Value;
                string org = fileName.Substring(0, fileName.Length - 4);
                OpenEMEVDFile(org, resource, jsText: data);
            }
        }

        #endregion

        #region Misc GUI events

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
            Editor?.ShowEmevdData();
        }

        private void customizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TextStyles.ShowStyleEditor())
            {
                SharedControls.RefreshGlobalStyles();
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CancelWithUnsavedChanges())
            {
                return;
            }
            Close();
        }

        private void GUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (CancelWithUnsavedChanges())
            {
                e.Cancel = true;
            }
        }

        private void DocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SharedControls.HideTip();
            display.Panel1Collapsed = !display.Panel1Collapsed;
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.Cut();

        private void ReplaceToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.ShowReplaceDialog();

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.SelectAll();

        private void FindToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.ShowFindDialog();

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.Copy();

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"Version {ProgramVersion.VERSION}"
                + "\r\n-- Created by AinTunez\r\n-- MattScript and other features by thefifthmatt"
                + "\r\n-- Based on work by HotPocketRemix and TKGP\r\n-- Special thanks to Meowmaritus", "About DarkScript");
        }

        private void batchDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CancelWithUnsavedChanges())
            {
                return;
            }
            var ofd = new OpenFileDialog();
            ofd.Title = "Open (note: skips existing JS files)";
            ofd.Multiselect = true;
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string gameDocs = ChooseGame(out bool fancy);
                if (gameDocs == null) return;
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
                        if (OpenEMEVDFile(fileName, gameDocs, isFancy: fancy) && Editor.SaveJSFile())
                        {
                            succeeded.Add(fileName);
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
                MessageBox.Show(string.Join(Environment.NewLine, lines));
            }
        }

        private void batchResaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CancelWithUnsavedChanges())
            {
                return;
            }
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
                        if (OpenJSFile(fileName) && Editor.SaveJSAndEMEVDFile())
                        {
                            succeeded.Add(fileName);
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
                MessageBox.Show(string.Join(Environment.NewLine, lines));
            }
        }

        private void openAutoCompleteMenuToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.ShowInstructionMenu();

        private void scriptCompilationSettingsToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.ShowScriptSettings();

        private void decompileToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.ShowPreviewDecompile();

        private void previewCompilationOutputToolStripMenuItem_Click(object sender, EventArgs e) => Editor?.ShowPreviewCompile();

        private void viewEMEDFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string name = Editor?.GetDocName();
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

        #endregion
    }
}
