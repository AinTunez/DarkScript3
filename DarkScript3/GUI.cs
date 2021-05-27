using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using SoulsFormats;
using System.Xml.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace DarkScript3
{
    public partial class GUI : Form
    {
        private string EVD_Path;
        private string FileVersion;
        private bool FromJSFile;
        private EventScripter Scripter;
        private InstructionDocs Docs;
        private ScriptSettings Settings;
        private bool CodeChanged = false;
        private AutocompleteMenu InstructionMenu;
        private BetterFindForm BFF;
        private PreviewCompilationForm Preview;

        private Dictionary<string, (string title, string text)> ToolTips = new Dictionary<string, (string, string)>();

        public GUI()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
            menuStrip.Renderer = new DarkToolStripRenderer();
            statusStrip.Renderer = new DarkToolStripRenderer();
            BFF = new BetterFindForm(editor);
            InfoTip = new ToolControl(editor, BFF);
            InfoTip.GotFocus += (object sender, EventArgs args) => editor.Focus();
            BFF.infoTip = InfoTip;
            display.Panel2.Controls.Add(InfoTip);
            InfoTip.Show();
            InfoTip.Hide();
            docBox.Font = editor.Font;
            editor.Focus();
            editor.SelectionColor = Color.White;
            docBox.SelectionColor = Color.White;
            LoadColors();
            InitUI();
            InfoTip.tipBox.TextChanged += (object sender, TextChangedEventArgs e) => TipBox_TextChanged(sender, e);
            editor.HotkeysMapping.Add(Keys.Control | Keys.Enter, FCTBAction.CustomAction1);
        }

        private void InitUI()
        {
            InstructionMenu = new AutocompleteMenu(editor);
            InstructionMenu.BackColor = Color.FromArgb(37, 37, 38);
            InstructionMenu.ForeColor = Color.FromArgb(240, 240, 240);
            InstructionMenu.SelectedColor = Color.FromArgb(0, 122, 204);
            InstructionMenu.Items.MaximumSize = new Size(400, 300);
            InstructionMenu.Items.Width = 250;
            InstructionMenu.AllowTabKey = true;
            InstructionMenu.AlwaysShowTooltip = false;
            InstructionMenu.ToolTipDuration = 1;
            InstructionMenu.AppearInterval = 250;

            InstructionMenu.ImageList = new ImageList();
            // Colors match the HTML emedf documentation
            InstructionMenu.ImageList.Images.Add("instruction", MakeColorImage(Color.FromArgb(0xFF, 0xFF, 0xB3)));
            InstructionMenu.ImageList.Images.Add("condition", MakeColorImage(Color.FromArgb(0xFF, 0xFF, 0xFF)));
            InstructionMenu.ImageList.Images.Add("enum", MakeColorImage(Color.FromArgb(0xE0, 0xB3, 0xFF)));
        }

        #region File Handling

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveJSAndEMEVDFile();
        }

        private bool SaveJSAndEMEVDFile()
        {
            if (Scripter == null) return false;
            InfoTip.Hide();

            Range originalSelect = editor.Selection.Clone();
            editor.Enabled = false;
            docBox.Enabled = false;
            MainMenuStrip.Enabled = false;
            Cursor = Cursors.WaitCursor;

            string text = editor.Text;
            bool fancyHint = text.Contains("$Event(");
            bool success = true;
            Range errorSelect = null;
            try
            {
                EMEVD result;
                if (fancyHint && Settings.AllowPreprocess && Docs.Translator != null)
                {
                    result = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).Pack(text, Scripter.FileName);
                }
                else
                {
                    result = Scripter.Pack(text, Scripter.FileName);
                }
                result.Write(EVD_Path);
                SaveJSFile();
                statusLabel.Text = "SAVE SUCCESSFUL";
                CodeChanged = false;
            }
            catch (Exception ex)
            {
                // Mainly these exceptions will be JSScriptException, from V8, and FancyCompilerException, from JS parsing/compilation.
                statusLabel.Text = "SAVE FAILED";
                StringBuilder extra = new StringBuilder();
                if (ex is JSScriptException && fancyHint && !Settings.AllowPreprocess)
                {
                    extra.AppendLine(Environment.NewLine);
                    extra.Append("($Event is used but preprocessing is disabled. Enable it in compilation settings if desired.)");
                }
                if (FromJSFile && ProgramVersion.CompareVersions(ProgramVersion.VERSION, FileVersion) != 0)
                {
                    extra.AppendLine(Environment.NewLine);
                    extra.Append(ProgramVersion.GetCompatibilityMessage(Scripter.FileName, Docs.ResourceString, FileVersion));
                }
                errorSelect = ShowCompileError(Scripter.FileName, ex, extra.ToString());
                success = false;
            }

            Cursor = Cursors.Default;
            MainMenuStrip.Enabled = true;
            editor.Enabled = true;
            editor.Focus();
            if (errorSelect == null)
            {
                editor.Selection = originalSelect;
            }
            else
            {
                editor.Selection = errorSelect;
                editor.DoSelectionVisible();
            }
            docBox.Enabled = true;
            return success;
        }

        private bool CancelWithUnsavedChanges(object sender, EventArgs e)
        {
            if (Scripter != null && CodeChanged)
            {
                DialogResult result = MessageBox.Show("Save changes before opening a new file?", "Unsaved Changes", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    SaveToolStripMenuItem_Click(sender, e);
                }
                else if (result == DialogResult.Cancel)
                {
                    return true;
                }
            }
            return false;
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InfoTip.Hide();
            if (CancelWithUnsavedChanges(sender, e))
            {
                return;
            }
            InstructionDocs oldDocs = Docs;
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
            if (Docs == null)
            {
                return;
            }

            if (oldDocs?.ResourceString != Docs.ResourceString)
            {
                JSRegex.GlobalConstant = new Regex($@"[^.]\b(?<range>{string.Join("|", InstructionDocs.GlobalConstants.Concat(Docs.GlobalEnumConstants.Keys))})\b");
                SetGlobalStyles();
            }

            List<AutocompleteItem> instructions = new List<AutocompleteItem>();
            List<AutocompleteItem> conditions = new List<AutocompleteItem>();
            foreach (string s in Docs.AllArgs.Keys)
            {
                string menuText = s;

                string toolTipTitle = s;
                string toolTipText;
                bool isInstr = Docs.Functions.TryGetValue(s, out (int, int) indices);
                if (isInstr)
                {
                    toolTipText = $"{indices.Item1}[{indices.Item2}] ({ArgString(s)})";
                }
                else
                {
                    toolTipText = $"({ArgString(s)})";
                }
                ToolTips[menuText] = (toolTipTitle, toolTipText);

                if (isInstr)
                {
                    instructions.Add(new AutocompleteItem(s + "(", InstructionMenu.ImageList.Images.IndexOfKey("instruction"), menuText));
                }
                else
                {
                    conditions.Add(new AutocompleteItem(s + "(", InstructionMenu.ImageList.Images.IndexOfKey("condition"), menuText));
                }
            }
            IEnumerable<AutocompleteItem> enums = Docs.EnumValues.Keys.Select(s => new AutocompleteItem(s, InstructionMenu.ImageList.Images.IndexOfKey("enum"), s));

            InstructionMenu.Items.SetAutocompleteItems(
                instructions.OrderBy(i => i.MenuText)
                    .Concat(conditions.OrderBy(i => i.MenuText))
                    .Concat(enums.OrderBy(i => i.MenuText)));
            editor.ClearUndo();
            CodeChanged = false;
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
            if (Docs == null || Docs.ResourceString != gameDocs)
            {
                Docs = new InstructionDocs(gameDocs);
            }
            Settings = new ScriptSettings(Docs, extraFields);
            Scripter = new EventScripter(fileName, Docs, evd);

            bool changed = CodeChanged;
            FromJSFile = true;
            if (jsText == null)
            {
                try
                {
                    FromJSFile = false;
                    if (isFancy && Docs.Translator != null)
                    {
                        jsText = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).Unpack();
                    }
                    else
                    {
                        jsText = Scripter.Unpack();
                    }
                }
                catch (Exception ex)
                {
                    // Also try to do it in compatibility mode, for emevd files which are no longer allowed, such as changing EMEDFs.
                    try
                    {
                        jsText = Scripter.Unpack(compatibilityMode: true);
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
                        Scripter = null;
                        CodeChanged = changed;
                        return false;
                    }
                }
            }
            editor.Text = jsText;
            EVD_Path = fileName;
            Text = $"DARKSCRIPT 3 - {Scripter.FileName}";
            FileVersion = extraFields != null && extraFields.TryGetValue("version", out string version) ? version : null;
            if (FromJSFile)
            {
                // Notify about possible compatibility issues
                int versionCmp = ProgramVersion.CompareVersions(ProgramVersion.VERSION, FileVersion);
                if (versionCmp > 0)
                {
                    statusLabel.Text = "Note: File was previously saved using an earlier version of DarkScript3";
                }
                else if (versionCmp < 0)
                {
                    statusLabel.Text = "Note: File was previously saved using an newer version of DarkScript3. Please update!";
                }
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

            if (File.Exists(org))
            {
                SFUtil.Backup(org);
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

        private bool SaveJSFile()
        {
            if (Scripter == null) return false;
            try
            {
                var sb = new StringBuilder(); ;
                sb.AppendLine("// ==EMEVD==");
                sb.AppendLine($"// @docs    {Docs.ResourceString}");
                sb.AppendLine($"// @compress    {Scripter.EVD.Compression}");
                sb.AppendLine($"// @game    {Scripter.EVD.Format}");
                if (Docs.IsASCIIStringData)
                    sb.AppendLine($"// @string    {Encoding.ASCII.GetString(Scripter.EVD.StringData)}");
                else
                    sb.AppendLine($"// @string    {Encoding.Unicode.GetString(Scripter.EVD.StringData)}");
                sb.AppendLine($"// @linked    [{string.Join(",", Scripter.EVD.LinkedFileOffsets)}]");
                foreach (KeyValuePair<string, string> extra in Settings.SettingsDict)
                {
                    sb.AppendLine($"// @{extra.Key}    {extra.Value}");
                }
                sb.AppendLine($"// @version    {ProgramVersion.VERSION}");
                sb.AppendLine("// ==/EMEVD==");
                sb.AppendLine("");
                sb.AppendLine(editor.Text);
                File.WriteAllText($"{EVD_Path}.js", sb.ToString());
                FileVersion = ProgramVersion.VERSION;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return false;
            }
            return true;
        }

        #endregion

        #region Highlighting

        public static TextStyle MakeStyle(int r, int g, int b, FontStyle f = FontStyle.Regular, bool renderCJK = false)
        {
            var color = Color.FromArgb(r, g, b);
            return MakeStyle(color, f, renderCJK);
        }

        public static TextStyle MakeStyle(Color c, FontStyle f = FontStyle.Regular, bool renderCJK = false)
        {
            Brush b = new SolidBrush(c);
            TextStyle style;
            if (renderCJK)
            {
                style = new CJKCompatibleTextStyle(b, null, f);
            }
            else
            {
                style = new TextStyle(b, null, f);
            }
            Styles.Add(style);
            return style;
        }

        // A hack to make CJK text display not overlap, since default char width is based on the with of M.
        // If needed we can also set the DefaultStyle to use this, but at the moment it's only used in comments.
        public class CJKCompatibleTextStyle : TextStyle
        {
            public CJKCompatibleTextStyle(Brush foreBrush, Brush backgroundBrush, FontStyle fontStyle) : base(foreBrush, backgroundBrush, fontStyle) { }

            // This is taken from TextStyle but without the incredibly slow IME mode.
            public override void Draw(Graphics gr, Point position, Range range)
            {
                if (BackgroundBrush != null)
                    gr.FillRectangle(BackgroundBrush, position.X, position.Y, (range.End.iChar - range.Start.iChar) * range.tb.CharWidth, range.tb.CharHeight);

                using (var f = new Font(range.tb.Font, FontStyle))
                {
                    Line line = range.tb[range.Start.iLine];
                    float dx = range.tb.CharWidth;
                    // The calculation used in TextStyle.
                    float y = position.Y + range.tb.LineInterval / 2;
                    float x = position.X - range.tb.CharWidth / 3;

                    if (ForeBrush == null)
                        ForeBrush = new SolidBrush(range.tb.ForeColor);

                    GraphicsState savedState = null;
                    float widthRatio = 0;
                    for (int i = range.Start.iChar; i < range.End.iChar; i++)
                    {
                        char c = line[i].c;
                        if (FastColoredTextBox.IsCJKLetter(c))
                        {
                            if (savedState == null)
                            {
                                if (widthRatio == 0)
                                {
                                    // To be more performant and readable, use a single width for everything.
                                    widthRatio = range.tb.CharWidth / FastColoredTextBox.GetCharSize(f, '間').Width;
                                }
                                savedState = gr.Save();
                                // Even though this is appended at the end, the x/y transform is still applied after that by DrawString.
                                // It's compensated for below.
                                gr.ScaleTransform(widthRatio, 1, MatrixOrder.Append);
                            }
                        }
                        else
                        {
                            if (savedState != null)
                            {
                                gr.Restore(savedState);
                                savedState = null;
                            }
                        }
                        float relX = savedState == null ? x : x / widthRatio + 2;
                        gr.DrawString(c.ToString(), f, ForeBrush, relX, y, stringFormat);
                        x += dx;
                    }
                    if (savedState != null)
                    {
                        gr.Restore(savedState);
                    }
                }
            }
        }

        public static List<TextStyle> Styles = new List<TextStyle>();

        public static class TextStyles
        {
            public static TextStyle Comment = MakeStyle(87, 166, 74, renderCJK: true);
            public static TextStyle String = MakeStyle(214, 157, 133);
            public static TextStyle Keyword = MakeStyle(86, 156, 214);
            public static TextStyle ToolTipKeyword = MakeStyle(106, 176, 234);
            public static TextStyle EnumProperty = MakeStyle(255, 150, 239);
            public static TextStyle EnumConstant = MakeStyle(78, 201, 176);
            public static TextStyle Number = MakeStyle(181, 206, 168);
            public static TextStyle EnumType = MakeStyle(180, 180, 180);
            public static BoxStyle HighlightToken = BoxStyle.Make(Color.FromArgb(127, 255, 255, 255));
        }

        public class BoxStyle : Style
        {
            public Pen BorderPen { get; set; }

            public static BoxStyle Make(Color color)
            {
                return new BoxStyle { BorderPen = new Pen(color) };
            }

            public override void Draw(Graphics gr, Point position, Range range)
            {
                gr.DrawRectangle(BorderPen, position.X, position.Y, (range.End.iChar - range.Start.iChar) * range.tb.CharWidth, range.tb.CharHeight);
            }
        }

        private void customizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var colors = new StyleConfig(this, editor.Font);
            if (colors.ShowDialog() == DialogResult.OK)
            {
                editor.ClearStylesBuffer();
                docBox.ClearStylesBuffer();

                TextStyles.Comment = MakeStyle(colors.commentSetting.Color, renderCJK: true);
                TextStyles.String = MakeStyle(colors.stringSetting.Color);
                TextStyles.Keyword = MakeStyle(colors.keywordSetting.Color);
                TextStyles.ToolTipKeyword = MakeStyle(colors.ttKeywordSetting.Color);
                TextStyles.EnumProperty = MakeStyle(colors.enumPropSetting.Color);
                TextStyles.EnumConstant = MakeStyle(colors.globalConstSetting.Color);
                TextStyles.Number = MakeStyle(colors.numberSetting.Color);
                TextStyles.EnumType = MakeStyle(colors.toolTipEnumType.Color);
                Color box = colors.highlightBoxSetting.Color;
                box = Color.FromArgb(127, box.R, box.G, box.B);
                TextStyles.HighlightToken = BoxStyle.Make(box);

                editor.BackColor = colors.backgroundSetting.Color;
                editor.SelectionColor = colors.highlightSetting.Color;
                editor.ForeColor = colors.plainSetting.Color;

                docBox.BackColor = colors.backgroundSetting.Color;
                docBox.SelectionColor = colors.highlightSetting.Color;
                docBox.ForeColor = colors.plainSetting.Color;

                SetGlobalFont(colors.FontSetting, checkAllowed: true);

                SaveColors();
                SetGlobalStyles();
            }
        }

        private string HexColor(TextStyle s)
        {
            return HexColor(s.ForeBrush);
        }

        private string HexColor(Brush brush)
        {
            return HexColor((brush as SolidBrush).Color);
        }

        private string HexColor(Color c)
        {
            return c.ToArgb().ToString("X");
        }

        private void SaveColors()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Comment=" + HexColor(TextStyles.Comment));
            sb.AppendLine("String=" + HexColor(TextStyles.String));
            sb.AppendLine("Keyword=" + HexColor(TextStyles.Keyword));
            sb.AppendLine("ToolTipKeyword=" + HexColor(TextStyles.ToolTipKeyword));
            sb.AppendLine("EnumProperty=" + HexColor(TextStyles.EnumProperty));
            sb.AppendLine("EnumConstant=" + HexColor(TextStyles.EnumConstant));
            sb.AppendLine("Number=" + HexColor(TextStyles.Number));
            sb.AppendLine("EnumType=" + HexColor(TextStyles.EnumType));
            sb.AppendLine("Background=" + HexColor(editor.BackColor));
            sb.AppendLine("Highlight=" + HexColor(editor.SelectionColor));
            sb.AppendLine("HighlightBox=" + HexColor(TextStyles.HighlightToken.BorderPen.Brush));
            sb.AppendLine("Default=" + HexColor(editor.ForeColor));
            sb.AppendLine("Font=" + new FontConverter().ConvertToInvariantString(editor.Font));
            File.WriteAllText("colors.cfg", sb.ToString());
        }

        private void LoadColors()
        {
            if (!File.Exists("colors.cfg")) return;

            Dictionary<string, string> cfg = new Dictionary<string, string>();

            string[] lines = File.ReadAllLines("colors.cfg");
            foreach (string line in lines)
            {
                string[] split = line.Split(new[] { '=' }, 2);
                string prop = split[0].Trim();
                string val = split[1].Trim();
                cfg[prop] = val;
            }

            Color colorFromHex(string prop) => Color.FromArgb(Convert.ToInt32(cfg[prop], 16));

            TextStyles.Comment = MakeStyle(colorFromHex("Comment"), renderCJK: true);
            TextStyles.String = MakeStyle(colorFromHex("String"));
            TextStyles.Keyword = MakeStyle(colorFromHex("Keyword"));
            TextStyles.ToolTipKeyword = MakeStyle(colorFromHex("ToolTipKeyword"));
            TextStyles.EnumProperty = MakeStyle(colorFromHex("EnumProperty"));
            TextStyles.EnumConstant = MakeStyle(colorFromHex("EnumConstant"));
            TextStyles.Number = MakeStyle(colorFromHex("Number"));
            TextStyles.EnumType = MakeStyle(colorFromHex("EnumType"));

            editor.SelectionColor = colorFromHex("Highlight");
            editor.BackColor = colorFromHex("Background");
            editor.ForeColor = colorFromHex("Default");

            docBox.SelectionColor = editor.SelectionColor;
            docBox.BackColor = editor.BackColor;
            docBox.ForeColor = editor.ForeColor;

            // Added in later version
            if (cfg.ContainsKey("HighlightBox"))
            {
                TextStyles.HighlightToken = BoxStyle.Make(colorFromHex("HighlightBox"));
            }
            if (cfg.ContainsKey("Font"))
            {
                if (new FontConverter().ConvertFromInvariantString(cfg["Font"]) is Font font)
                {
                    if (new InstalledFontCollection().Families.Any(family => family.Name == font.Name))
                    {
                        SetGlobalFont(font, checkAllowed: true);
                    }
                }
            }
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            statusLabel.Text = "";
            if (Scripter != null)
            {
                CodeChanged = true;
            }
            if (Preview != null)
            {
                Preview.DisableConversion();
            }
            SetStyles(e.ChangedRange);
            SetHighlightRange(null, null);
            RecalculateHighlightTokenDelayed();
        }

        private void SetGlobalFont(Font font, bool checkAllowed = false)
        {
            if (checkAllowed)
            {
                // FastColoredTextBox will refuse to set a monospace font (if . and M aren't the same width), so revert in that case.
                // Use InfoTip as a guinea pig, but any of them will work.
                Font oldFont = editor.Font;
                InfoTip.tipBox.Font = font;
                if (InfoTip.tipBox.Font.Name != font.Name)
                {
                    FontConverter converter = new FontConverter();
                    MessageBox.Show(
                        $"Font \"{converter.ConvertToInvariantString(font)}\" detected as not monospace."
                            + $" Reverting back to \"{converter.ConvertToInvariantString(oldFont)}\".");
                    font = oldFont;
                }
            }
            editor.Font = font;
            docBox.Font = font;
            InfoTip.tipBox.Font = font;
        }

        private List<Style> HighlightStyles;

        public void SetGlobalStyles()
        {
            SetStyles(editor.Range);
            SetStyles(docBox.Range);
            HighlightStyles = new List<Style> { TextStyles.Keyword, TextStyles.Number, TextStyles.EnumConstant, TextStyles.EnumProperty };
        }

        public void SetStyles(Range range, Regex highlight = null)
        {
            range.ClearStyle(Styles.ToArray());
            range.SetStyle(TextStyles.Comment, JSRegex.Comment1);
            range.SetStyle(TextStyles.Comment, JSRegex.Comment2);
            range.SetStyle(TextStyles.Comment, JSRegex.Comment3);
            range.SetStyle(TextStyles.String, JSRegex.String);
            range.SetStyle(TextStyles.String, JSRegex.StringArg);
            range.SetStyle(TextStyles.Keyword, JSRegex.Keyword);
            range.SetStyle(TextStyles.Number, JSRegex.Number);
            range.SetStyle(TextStyles.EnumConstant, JSRegex.GlobalConstant);
            range.SetStyle(TextStyles.EnumProperty, JSRegex.Property);
            range.SetFoldingMarkers("{", "}");
            range.SetFoldingMarkers(@"/\*", @"\*/");
        }

        public static RegexOptions RegexCompiledOption
        {
            get
            {
                if (PlatformType.GetOperationSystemPlatform() == Platform.X86)
                    return RegexOptions.Compiled;
                else
                    return RegexOptions.None;
            }
        }

        public static class JSRegex
        {
            public static Regex Property = new Regex(@"(\w|\$)+\.(?<range>(\w|\$)+)");
            public static Regex GlobalConstant = new Regex(@"\bX\d+_\d+\b");
            public static Regex StringArg = new Regex(@"\bX\d+_\d+\b");
            public static Regex String = new Regex(@"""""|''|"".*?[^\\]""|'.*?[^\\]'", RegexCompiledOption);
            public static Regex Comment1 = new Regex(@"//.*$", RegexOptions.Multiline | RegexCompiledOption);
            public static Regex Comment2 = new Regex(@"(/\*.*?\*/)|(/\*.*)", RegexOptions.Singleline | RegexCompiledOption);
            public static Regex Comment3 = new Regex(@"(/\*.*?\*/)|(.*\*/)",
                                             RegexOptions.Singleline | RegexOptions.RightToLeft | RegexCompiledOption);
            // Allow negative sign here so that negative numbers can be highlighted as tokens.
            public static Regex Number = new Regex(@"(\b|-)\d+[\.]?\d*([eE]\-?\d+)?[lLdDfF]?\b|\b0x[a-fA-F\d]+\b",
                                           RegexCompiledOption);
            public static Regex Keyword =
                new Regex(
                    @"\b("
                        // Reserved in ECMAScript 2015
                        + "break|case|catch|class|const|continue|debugger|default|delete|do|else|export|extends|finally|for|function"
                        + "|if|import|in|instanceof|new|return|super|switch|this|throw|try|typeof|var|void|while|with|yield"
                        // Reserved in strict mode/modules
                        + "|await|implements|interface|let|package|private|protected|public|static|yield"
                        // Custom
                        + "|true|false"
                    + @")\b|\$Event\b",
                    RegexCompiledOption);
            public static Regex DataType = new Regex(@"\b(byte|short|int|sbyte|ushort|uint|enum|bool)\b", RegexCompiledOption);
        }

        #endregion

        #region Selection

        // Reinitialized in the constructor
        private ToolControl InfoTip = new ToolControl();
        // Selection state
        private Range CurrentTipRange;
        private Range CurrentTokenRange;
        private string CurrentToken;
        private Action<string> loadDocTextDebounce;
        private Action<int> highlightTokenDebounce;
        private bool statusActionInfo = false;

        private void Editor_ToolTipNeeded(object sender, ToolTipNeededEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.HoveredWord))
            {
                if (ToolTips.ContainsKey(e.HoveredWord))
                {
                    (string title, string text) = ToolTips[e.HoveredWord];
                    Point p = editor.PlaceToPoint(e.Place);
                    string s = title + "\n" + text;
                    ShowTip(s, p);
                }
            }
        }

        private void TipBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.SetStyle(TextStyles.ToolTipKeyword, JSRegex.DataType);
            e.ChangedRange.SetStyle(TextStyles.EnumType, new Regex(@"[<>]"));
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            // Find text around cursor in the current line, up until hitting parentheses
            Range arguments = editor.Selection.GetFragment(@"[^)(\n]");

            // Preemptively remove tooltip if changing the range; it may potentially get immediately added back.
            // The tooltip may also change even within the same range.
            if (CurrentTipRange == null || !arguments.Start.Equals(CurrentTipRange.Start))
            {
                CurrentTipRange = arguments;
                InfoTip.Hide();
            }

            if (statusActionInfo)
            {
                statusLabel.Text = "";
                statusActionInfo = false;
            }

            // Immediately remove/update token highlight range if we've stepped outside of it
            if (CurrentTokenRange != null && !CurrentTokenRange.Contains(editor.Selection.Start))
            {
                Range alreadyHighlighted = editor.Selection.GetFragment(TextStyles.HighlightToken, true);
                if (!alreadyHighlighted.IsEmpty && alreadyHighlighted.Text == CurrentToken)
                {
                    CurrentTokenRange = alreadyHighlighted;
                }
                else
                {
                    SetHighlightRange(null, null);
                    RecalculateHighlightTokenDelayed();
                }
            }
            // Now if there is no current highlight range, try to calculate it.
            // With a delay, so it doesn't pop up in an annoying way, and because it requires a whole-screen update.
            if (CurrentTokenRange == null)
            {
                RecalculateHighlightTokenDelayed();
            }

            // For being within function args rather than matching the exact string, check if inside parens, but not nested ones.
            // Matching IfThing(0^,1) but not WaitFor(Thi^ng(0,1))
            string funcName;
            if (arguments.CharBeforeStart == '(' && editor.GetRange(arguments.End, arguments.End).CharAfterStart != '(')
            {
                // Scan leftward through arguments until no commas remain.
                // This does not work with nested calls, like IfThing(getMyCustomConstant(), ^1)
                int argIndex = 0;
                Range arg = editor.Selection.GetFragment(@"[^)(\n,]");
                while (arg.CharBeforeStart == ',')
                {
                    argIndex++;
                    Place start = arg.Start;
                    start.iChar -= 2;
                    arg = editor.GetRange(start, start).GetFragment(@"[^)(\n,]");
                }
                funcName = FuncName(arguments);
                ShowArgToolTip(funcName, arguments, argIndex);
            }
            else
            {
                // Get the word immediately under the cursor. No tooltip in this case.
                Range func = editor.Selection.GetFragment(@"[\w\$]");
                funcName = func.Text;
            }
            if (loadDocTextDebounce == null)
            {
                loadDocTextDebounce = Debounce((Action<string>)LoadDocText, 50);
            }
            loadDocTextDebounce(funcName);
        }

        private void Editor_VisibleRangeChangedDelayed(object sender, EventArgs e)
        {
            // Can be used to optimize temporary display ranges if needed
        }

        private void Editor_MouseUp(object sender, MouseEventArgs e)
        {
            // Use this to implement Ctrl+Click, which tries to find an event definition or initialization.
            if ((ModifierKeys & Keys.Control) == 0)
            {
                return;
            }
            Place place = editor.PointToPlace(e.Location);
            JumpToEvent(place);
        }

        private void Editor_CustomAction(object sender, CustomActionEventArgs e)
        {
            if (e.Action == FCTBAction.CustomAction1)
            {
                if (Scripter != null)
                {
                    JumpToEvent(editor.Selection.Start);
                }
            }
        }

        private void JumpToEvent(Place place)
        {
            Range numRange = editor.GetRange(place, place).GetFragment(TextStyles.Number, false);
            if (!int.TryParse(numRange.Text, out int id))
            {
                return;
            }
            // This isn't a comprehensive regex and doesn't support a lot of code on the same line.
            Regex regex = new Regex($@"^\s*\$?Event\(\s*{id}\s*,", RegexOptions.Singleline);
            List<Range> ranges = editor.Range.GetRangesByLines(regex).ToList();
            string searchType = "";
            if (ranges.Any(r => r.Start.iLine == place.iLine))
            {
                // At the moment, only support regular event initialization. Common events would need to be cross-file.
                regex = new Regex($@"^\s*InitializeEvent\(\s*\d+\s*,\s*{id}\b", RegexOptions.Singleline);
                ranges = editor.Range.GetRangesByLines(regex).ToList();
                searchType = " initialization";
            }
            if (ranges.Count > 0)
            {
                Range idRange = ranges[0].GetRanges($"{id}").LastOrDefault() ?? ranges[0];
                editor.Selection = editor.GetRange(idRange.Start, idRange.Start);
                // Update the last visited line; can't be done directly so call where it's done.
                editor.OnSelectionChangedDelayed();
                editor.DoSelectionVisible();
                string extraText = ranges.Count == 1 ? "" : $" (1 of {ranges.Count})";
                statusLabel.Text = $"Event {id}{searchType} found{extraText}. Use Ctrl+- to navigate back";
            }
            else
            {
                statusLabel.Text = $"Event {id}{searchType} not found";
            }
            statusActionInfo = true;
        }

        private void RecalculateHighlightTokenDelayed()
        {
            if (highlightTokenDebounce == null)
            {
                highlightTokenDebounce = Debounce((Action<int>)CalculateHighlightToken, 300);
            }
            highlightTokenDebounce(0);
        }

        private void CalculateHighlightToken(int dummy)
        {
            // We want to highlight floating point numbers, integers (including negative sign), and variable names.
            // Try to avoid inside of strings and comments and exclude a few very common constants.
            // We can do some light tokenization to achieve this, but try to rely on existing styles instead where possible.
            HashSet<string> excludeHighlightTokens = new HashSet<string> { "0", "true", "false" };
            List<Style> styles = editor.GetStylesOfChar(editor.Selection.Start);
            Range range;
            if (styles.Contains(TextStyles.Comment))
            {
                range = null;
            }
            else if (styles.Contains(TextStyles.String))
            {
                // Normally exclude strings, but don't exclude param args
                range = editor.Selection.GetFragment(TextStyles.String, false);
                char start = range.CharAfterStart;
                if (start == '"' || start == '\'')
                {
                    range = null;
                }
            }
            else
            {
                Style candidate = null;
                if (styles.Count > 0 && HighlightStyles != null)
                {
                    candidate = HighlightStyles.Find(style => styles.Contains(style));
                }
                if (candidate == null)
                {
                    // Roughly match JavaScript-variable-like string
                    range = editor.Selection.GetFragment(@"[\w\$_]");
                    // This may be a comment, if the end of range isn't a comment because it's EOL, so specially check that.
                    if (editor.GetStylesOfChar(range.Start).Contains(TextStyles.Comment))
                    {
                        range = null;
                    }
                }
                else
                {
                    range = editor.Selection.GetFragment(candidate, false);
                }
            }
            string text = range?.Text;
            if (excludeHighlightTokens.Contains(text))
            {
                range = null;
                text = null;
            }
            SetHighlightRange(range, text);
        }

        private void SetHighlightRange(Range range, string text)
        {
            if (range == null || range.IsEmpty)
            {
                CurrentToken = null;
                CurrentTokenRange = null;
                // If needed, instead of doing this, we can use editor.VisibleRange and update later on.
                // This doesn't seem to be a bit issue so far, however.
                editor.Range.ClearStyle(TextStyles.HighlightToken);
            }
            else
            {
                CurrentToken = text;
                CurrentTokenRange = range;
                // Punctuation doesn't work with \b
                bool noPrefix = text.StartsWith("-");
                // Regex regex = new Regex((noPrefix ? "" : @"(\b|^|\n)") + Regex.Escape(text) + @"(\b|$|\r|\n)");
                Regex regex = new Regex((noPrefix ? "" : @"\b") + Regex.Escape(text) + @"\b");

                editor.Range.SetStyle(TextStyles.HighlightToken, regex);
            }
        }

        private void ShowTip(string s, Point p, int argIndex = -1)
        {
            if (argIndex > -1)
            {
                string[] args = Regex.Split(s, @"\s*,\s");
                if (argIndex > args.Length - 1)
                    args[args.Length - 1] = "\u2b9a " + args[args.Length - 1];
                else
                    args[argIndex] = "\u2b9a " + args[argIndex];
                InfoTip.SetText(string.Join(", ", args));
            }
            else
            {
                InfoTip.SetText(s);
            }

            p.Offset(0, -InfoTip.Height - 5);
            if (!InfoTip.Location.Equals(p))
                InfoTip.Location = p;
            if (!InfoTip.Visible)
                InfoTip.Show();

            InfoTip.BringToFront();
            editor.Focus();
        }

        private void ShowArgToolTip(string funcName, Range arguments, int argument = -1)
        {
            if (Docs != null && Docs.AllArgs.ContainsKey(funcName))
            {
                Point point = editor.PlaceToPoint(arguments.Start);
                ShowTip(ArgString(funcName), point, argument);
            }
        }

        private Bitmap MakeColorImage(Color color)
        {
            var map = new Bitmap(16, 16);
            using (Graphics gfx = Graphics.FromImage(map))
            using (SolidBrush brush = new SolidBrush(color))
            {
                gfx.FillRectangle(brush, 1, 1, 14, 14);
            }
            return map;
        }

        #endregion

        #region Text Handling

        private string FuncName(Range arguments)
        {
            int start = arguments.Start.iChar - 2;
            int line = arguments.Start.iLine;
            Range pre = new Range(editor, start, line, start, line);
            return pre.GetFragment(@"[\w\$]").Text;
        }

        private string ArgString(string func, int index = -1)
        {
            List<EMEDF.ArgDoc> args = Docs.AllArgs[func];
            List<string> argStrings = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                EMEDF.ArgDoc arg = args[i];
                if (arg.EnumName == "BOOL")
                {
                    argStrings.Add($"bool {arg.DisplayName}");
                }
                else if (arg.EnumDoc != null)
                {
                    argStrings.Add($"enum<{arg.EnumDoc.DisplayName}> {arg.DisplayName}");
                }
                else
                {
                    argStrings.Add($"{InstructionDocs.TypeString(arg.Type)} {arg.DisplayName}");
                }
                if (i == index) return argStrings.Last();
            }
            return string.Join(", ", argStrings);
        }

        private string currentFuncDoc = null;

        private void LoadDocText(string func)
        {
            if (Docs == null) return;
            List<EMEDF.ArgDoc> args = null;
            ScriptAst.BuiltIn builtin = null;
            if (!Docs.AllArgs.TryGetValue(func, out args) && !ScriptAst.ReservedWords.TryGetValue(func, out builtin)) return;
            if (builtin != null && builtin.Doc == null) return;

            if (currentFuncDoc == func) return;
            currentFuncDoc = func;
            docBox.Clear();

            if (builtin != null)
            {
                docBox.AppendText($"{func}");
                if (builtin.Args != null)
                {
                    if (builtin.Args.Count == 0)
                    {
                        docBox.AppendText(Environment.NewLine);
                        docBox.AppendText("  (no arguments)", TextStyles.Comment);
                    }
                    foreach (string arg in builtin.Args)
                    {
                        docBox.AppendText(Environment.NewLine);
                        if (arg == "COND")
                        {
                            docBox.AppendText("  Condition ", TextStyles.Keyword);
                            docBox.AppendText("cond");
                        }
                        else if (arg == "LABEL")
                        {
                            docBox.AppendText("  Label ", TextStyles.Keyword);
                            docBox.AppendText("label");
                        }
                        else if (arg == "LAYERS")
                        {
                            docBox.AppendText("  uint ", TextStyles.Keyword);
                            docBox.AppendText("layer");
                            docBox.AppendText(" (vararg)", TextStyles.Comment);
                        }
                    }
                }
                if (builtin.Doc != null)
                {
                    docBox.AppendText(Environment.NewLine + Environment.NewLine + builtin.Doc);
                }
                return;
            }

            docBox.AppendText($"{func}");

            // TODO: Make ArgDoc include optional info instead of counting arguments? This requires making a shallow copy for cond functions.
            int optCount = 0;
            if (Docs.Translator != null && Docs.Translator.CondDocs.TryGetValue(func, out InstructionTranslator.FunctionDoc funcDoc))
            {
                optCount = funcDoc.OptionalArgs;
            }

            if (args.Count == 0)
            {
                docBox.AppendText(Environment.NewLine);
                docBox.AppendText("  (no arguments)", TextStyles.Comment);
            }
            for (int i = 0; i < args.Count; i++)
            {
                docBox.AppendText(Environment.NewLine);
                EMEDF.ArgDoc argDoc = args[i];
                bool optional = i >= args.Count - optCount;

                bool displayEnum = false;
                if (argDoc.EnumName == null)
                {
                    docBox.AppendText($"  {InstructionDocs.TypeString(argDoc.Type)} ", TextStyles.Keyword);
                }
                else if (argDoc.EnumName == "BOOL")
                {
                    docBox.AppendText($"  bool ", TextStyles.Keyword);
                }
                else if (argDoc.EnumDoc != null)
                {
                    docBox.AppendText($"  enum ", TextStyles.Keyword);
                    displayEnum = true;
                }

                docBox.AppendText(argDoc.DisplayName);
                if (optional) docBox.AppendText(" (optional)", TextStyles.Comment);
                if (argDoc.Vararg) docBox.AppendText(" (vararg)", TextStyles.Comment);

                if (displayEnum)
                {
                    EMEDF.EnumDoc enm = argDoc.EnumDoc;
                    foreach (var kv in enm.DisplayValues)
                    {
                        docBox.AppendText(Environment.NewLine);
                        docBox.AppendText($"    {kv.Key.PadLeft(5)}", TextStyles.String);
                        docBox.AppendText($": ");
                        string val = kv.Value;
                        if (val.Contains("."))
                        {
                            string[] parts = val.Split(new[] { '.' }, 2);
                            docBox.AppendText($"{parts[0]}.");
                            docBox.AppendText(parts[1], TextStyles.EnumProperty);
                        }
                        else
                        {
                            docBox.AppendText(val, TextStyles.EnumConstant);
                        }
                    }
                }
            }
            List<string> altNames = Docs.GetAltFunctionNames(func);
            if (altNames != null && altNames.Count > 0)
            {
                docBox.AppendText(Environment.NewLine + Environment.NewLine + $"(alt: {string.Join(", ", altNames)})");
            }
        }

        // https://stackoverflow.com/questions/28472205/c-sharp-event-debounce
        // Can only be called from the UI thread, and runs the given action in the UI thread.
        private static Action<T> Debounce<T>(Action<T> func, int ms)
        {
            CancellationTokenSource cancelTokenSource = null;

            return arg =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(ms, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            func(arg);
                        }
                    }, TaskScheduler.FromCurrentSynchronizationContext());
            };
        }

        #endregion

        #region Misc GUI Events

        private void EmevdDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null) return;
            InfoTip.Hide();
            (new InfoViewer(Scripter)).ShowDialog();
        }

        private void Display_Resize(object sender, EventArgs e)
        {
            if (WindowState != FormWindowState.Minimized && display.SplitterDistance != 350)
                display.SplitterDistance = 350;
        }

        private void Editor_ZoomChanged(object sender, EventArgs e)
        {
            InfoTip.Hide();
            SetGlobalFont(editor.Font);
        }

        private void docBox_ZoomChanged(object sender, EventArgs e)
        {
            InfoTip.Hide();
            SetGlobalFont(docBox.Font);
        }

        private void GUI_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                InfoTip.Hide();
            }
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                InfoTip.Hide();
            } else if (e.KeyCode == Keys.F && e.Control)
            {
                ShowFindDialog(editor.Selection.Text);
                e.Handled = true;
            }
        }

        public void ShowFindDialog(string findText)
        {
            if (findText != null)
                BFF.tbFind.Text = findText;
            else if (!editor.Selection.IsEmpty && editor.Selection.Start.iLine == editor.Selection.End.iLine)
                BFF.tbFind.Text = editor.Selection.Text;

            BFF.tbFind.SelectAll();
            BFF.Show();
            BFF.Focus();
            BFF.BringToFront();
        }

        private void Editor_Scroll(object sender, ScrollEventArgs e) => InfoTip.Hide();

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EVD_Path)|| !CodeChanged) Close();
            else
            {
                DialogResult result = MessageBox.Show("Save changes before exiting?", "Unsaved Changes", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    SaveToolStripMenuItem_Click(sender, e);
                    Close();
                }
                else if (result == DialogResult.No)
                {
                    Close();
                }
            }
        }

        private void DocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InfoTip.Hide();
            display.Panel1Collapsed = !display.Panel1Collapsed;
        }

        private void CutToolStripMenuItem_Click(object sender, EventArgs e) => editor.Cut();

        private void ReplaceToolStripMenuItem_Click(object sender, EventArgs e) => editor.ShowReplaceDialog();

        private void SelectAllToolStripMenuItem_Click(object sender, EventArgs e) => editor.SelectAll();

        private void FindToolStripMenuItem_Click(object sender, EventArgs e) => editor.ShowFindDialog();

        private void CopyToolStripMenuItem_Click(object sender, EventArgs e) => editor.Copy();

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show($"Version {ProgramVersion.VERSION}"
                + "\r\n-- Created by AinTunez\r\n-- MattScript and other features by thefifthmatt"
                + "\r\n-- Based on work by HotPocketRemix and TKGP\r\n-- Special thanks to Meowmaritus", "About DarkScript");
        }

        private void batchDumpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CancelWithUnsavedChanges(sender, e))
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
                        if (OpenEMEVDFile(fileName, gameDocs, isFancy: fancy) && SaveJSFile())
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
                editor.ClearUndo();

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
            if (CancelWithUnsavedChanges(sender, e))
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
                        if (OpenJSFile(fileName) && SaveJSAndEMEVDFile())
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
                editor.ClearUndo();

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


        private void openAutoCompleteMenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (InstructionMenu == null) return;
            InstructionMenu.Show(true);
        }

        private void scriptCompilationSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Settings == null) return;
            ScriptSettingsForm form = new ScriptSettingsForm(Settings);
            form.ShowDialog();
        }

        private void decompileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null || Docs.Translator == null) return;
            try
            {
                FancyJSCompiler.CompileOutput output = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).RepackFull(editor.Text);
                PreviewCompilationForm preview = RefreshPreviewForm();
                preview.SetSegments(output.GetDiffSegments(), output.Code);
                preview.Show();
                preview.Focus();
            }
            catch (FancyJSCompiler.FancyCompilerException ex)
            {
                if (ShowCompileError(Scripter.FileName, ex, "") is Range errorSelect)
                {
                    editor.Selection = errorSelect;
                    editor.DoSelectionVisible();
                }
            }
        }

        private void previewCompilationOutputToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null || Docs.Translator == null) return;
            try
            {
                List<FancyJSCompiler.DiffSegment> segments = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).PreviewPack(editor.Text);
                PreviewCompilationForm preview = RefreshPreviewForm();
                preview.SetSegments(segments);
                preview.Show();
                preview.Focus();
            }
            catch (FancyJSCompiler.FancyCompilerException ex)
            {
                if (ShowCompileError(Scripter.FileName, ex, "") is Range errorSelect)
                {
                    editor.Selection = errorSelect;
                    editor.DoSelectionVisible();
                }
            }
        }

        private PreviewCompilationForm RefreshPreviewForm()
        {
            if (Preview == null || Preview.IsDisposed)
            {
                Preview = new PreviewCompilationForm(editor.Font);
                Preview.FormClosed += (sender, ev) =>
                {
                    if (Preview.Confirmed && Preview.PendingCode != null)
                    {
                        editor.Text = Preview.PendingCode;
                        if (Settings != null && !Settings.AllowPreprocess)
                        {
                            Settings.AllowPreprocess = true;
                        }
                    }
                };
            }
            return Preview;
        }

        private Range ShowCompileError(string file, Exception ex, string extra)
        {
            ErrorMessageForm error = new ErrorMessageForm(editor.Font);
            error.SetMessage(file, ex, extra);
            error.ShowDialog();
            if (error.Place is Place p)
            {
                Range select = editor.GetRange(p, p);
                try
                {
                    // Quick validity check
                    string text = select.Text;
                    return select;
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
            return null;
        }

        private void viewEMEDFToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Docs == null)
            {
                MessageBox.Show("Open a file to view the EMEDF for that game\r\nor access it in the Resources directory.");
                return;
            }
            string inferredName = Docs.ResourceString.Split('-')[0];
            string path = $"{inferredName}-emedf.html";
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
