using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.ClearScript;
using FastColoredTextBoxNS;
using SoulsFormats;
using System.Xml.Linq;
using System.Text;

namespace DarkScript3
{
    public partial class GUI : Form
    {
        public string EVD_Path;
        public EventScripter Scripter;
        public bool CodeChanged = false;
        public AutocompleteMenu InstructionMenu;
        public BetterFindForm BFF;

        Dictionary<string, (string title, string text)> ToolTips = new Dictionary<string, (string, string)>();

        public GUI()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            InitializeComponent();
            InfoTip.GotFocus += (object sender, EventArgs args) => editor.Focus();
            menuStrip.Renderer = new DarkToolStripRenderer();
            statusStrip.Renderer = new DarkToolStripRenderer();
            BFF = new BetterFindForm(editor);
            InfoTip = new ToolControl(editor, BFF);
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
            InstructionMenu.ImageList.Images.Add("instruction", MakeColorImage(Color.FromArgb(255, 255, 255)));
        }

        #region File Handling

        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null) return;
            InfoTip.Hide();

            editor.Enabled = false;
            docBox.Enabled = false;
            MainMenuStrip.Enabled = false;
            Cursor = Cursors.WaitCursor;

            try
            {
                Scripter.Pack(editor.Text).Write(EVD_Path);
                SaveJSFile();
                statusLabel.Text = "SAVE SUCCESSFUL";
                CodeChanged = false;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "SAVE FAILED";
                IScriptEngineException scriptException = ex as IScriptEngineException;
                if (scriptException != null)
                {
                    
                    string details = scriptException.ErrorDetails;
                    details = Regex.Replace(details, @"Script\s\[.*\]", "Script");
                    details = Regex.Replace(details, @"    at Script", "    at Editor");
                    details = Regex.Replace(details, @"->\s+", "-> ");
                    var lines = details.Split('\n');
                    var line = lines.FirstOrDefault((row) => row.Contains("at Editor"));

                    if (line != null)
                    {
                        MessageBox.Show(Regex.Replace(scriptException.Message, "^Error: ", "") + "\n" +  line.Trim());
                    }
                    else
                    {
                        MessageBox.Show(details);
                    }
                }
                else
                {
                    MessageBox.Show(ex.ToString().Trim());
                }
            }

            Cursor = Cursors.Default;
            MainMenuStrip.Enabled = true;
            editor.Enabled = true;
            docBox.Enabled = true;
            editor.Focus();
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InfoTip.Hide();
            if (Scripter != null && CodeChanged)
            {
                DialogResult result = MessageBox.Show("Save changes before opening a new file?", "Unsaved Changes", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    SaveToolStripMenuItem_Click(sender, e);
                }
                else if (result == DialogResult.Cancel)
                {
                    return;
                }
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx;";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
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
                    OpenEMEVDFile(ofd.FileName, null, ChooseGame());
                }
            }
            JSRegex.GlobalConstant = new Regex($@"[^.]\b(?<range>{string.Join("|", Scripter.GlobalConstants)})\b");
            IEnumerable<AutocompleteItem> instructions = Scripter.Functions.Keys.Select(s =>
            {
                var instr = Scripter.Functions[s];
                var doc = Scripter.DOC[instr.classIndex][instr.instrIndex];

                string menuText = s;
                string toolTipTitle = s;
                string toolTipText = $"{instr.classIndex}[{instr.instrIndex}] ({ArgString(s)})";

                return new AutocompleteItem(s + "(", InstructionMenu.ImageList.Images.IndexOfKey("instruction"), menuText, toolTipTitle, toolTipText);
            });

            foreach (var item in instructions)
            {
                ToolTips[item.MenuText] = (item.ToolTipTitle, item.ToolTipText);
                item.ToolTipText = null;
                item.ToolTipTitle = null;
            }
            instructions = instructions.OrderBy(i => i.Text);

            InstructionMenu.Items.SetAutocompleteItems(instructions);
            editor.ClearUndo();
            CodeChanged = false;
        }

        private void OpenEMEVDFile(string fileName, EMEVD evd, string gameDocs, string data = null)
        {
            if (evd == null)
                Scripter = new EventScripter(fileName, gameDocs, File.Exists(fileName.Replace(".emevd", ".emeld")));
            else
                Scripter = new EventScripter(evd, gameDocs);

            bool changed = CodeChanged;
            try
            {
                editor.Text = data ?? Scripter.Unpack();
                EVD_Path = fileName;
                Text = $"DARKSCRIPT 3 - {Path.GetFileName(fileName)}";
            } catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                CodeChanged = changed;
            }
        }

        private string GetHeaderValue(string fileText, string fieldName)
        {
            var headerText = Regex.Match(fileText, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==");
            if (headerText.Success)
            {
                string[] result = Regex.Split(headerText.Value, @"(\r\n|\r|\n)\s*");
                foreach (string headerLine in result.ToArray())
                {
                    string line = Regex.Replace(headerLine.Trim(), @"^//\s+", "// ");
                    string start = $"// @{fieldName} ";
                    if (line.StartsWith(start))
                    {
                        return line.Substring(start.Length).Trim();
                    }
                }
            }
            return null;
        }

        private void OpenJSFile(string fileName)
        {
            string org = fileName.Substring(0, fileName.Length - 3);
            SFUtil.Backup(org);
            string text = File.ReadAllText(fileName);
            string docs = GetHeaderValue(text, "docs");
            string[] fields = new string[]
            {
                GetHeaderValue(text, "compress"),
                GetHeaderValue(text, "format"),
                GetHeaderValue(text, "string"),
                GetHeaderValue(text, "linked")
            };

            EMEVD evd = null;
            if (!fields.Any(f => f == null))
            {
                evd = new EMEVD()
                {
                    Compression = (DCX.Type)Enum.Parse(typeof(DCX.Type), fields[0]),
                    Format = (EMEVD.Game)Enum.Parse(typeof(EMEVD.Game), fields[1]),
                    StringData = Encoding.Unicode.GetBytes(fields[2]),
                    LinkedFileOffsets = Regex.Split(fields[3], @"\s*,\s*").Select(o => long.Parse(o)).ToList()
                };
            }

            if (docs == null) docs = ChooseGame();

            text = Regex.Replace(text, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==", "");
            OpenEMEVDFile(org, evd, docs, text.Trim());
        }

        private string ChooseGame()
        {
            GameChooser chooser = new GameChooser();
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
                OpenEMEVDFile(org, null, resource, data);
            }
        }

        private bool SaveJSFile()
        {
            if (Scripter == null) return false;
            try
            {
                var sb = new StringBuilder(); ;
                sb.AppendLine("// ==EMEVD==");
                sb.AppendLine($"// @docs    {Scripter.ResourceString}");
                sb.AppendLine($"// @compress    {Scripter.EVD.Compression}");
                sb.AppendLine($"// @game    {Scripter.EVD.Format}");
                if (Scripter.ResourceString == "ds2scholar-common.emedf.json")
                    sb.AppendLine($"// @string    {Encoding.ASCII.GetString(Scripter.EVD.StringData)}");
                else
                    sb.AppendLine($"// @string    {Encoding.Unicode.GetString(Scripter.EVD.StringData)}");
                sb.AppendLine($"// @linked    [{string.Join(",", Scripter.EVD.LinkedFileOffsets)}]");
                sb.AppendLine("// ==/EMEVD==");
                sb.AppendLine("");
                sb.AppendLine(editor.Text);
                File.WriteAllText($"{EVD_Path}.js", sb.ToString());
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

        public static TextStyle MakeStyle(Color c, FontStyle f = FontStyle.Regular)
        {
            return MakeStyle(new SolidBrush(c), f);
        }

        public static TextStyle MakeStyle(int r, int g, int b, FontStyle f = FontStyle.Regular)
        {
            var color = Color.FromArgb(r, g, b);
            return MakeStyle(new SolidBrush(color), f);
        }

        private static TextStyle MakeStyle(Brush b, FontStyle f = FontStyle.Regular)
        {
            Styles.Add(new TextStyle(b, Brushes.Transparent, f));
            return Styles.Last();
        }

        public static List<TextStyle> Styles = new List<TextStyle>();

        public static class TextStyles
        {
            public static TextStyle Comment = MakeStyle(87, 166, 74);
            public static TextStyle String = MakeStyle(214, 157, 133);
            public static TextStyle Keyword = MakeStyle(86, 156, 214);
            public static TextStyle ToolTipKeyword = MakeStyle(106, 176, 234);
            public static TextStyle EnumProperty = MakeStyle(255, 150, 239);
            public static TextStyle EnumConstant = MakeStyle(78, 201, 176);
            public static TextStyle Number = MakeStyle(181, 206, 168);
            public static TextStyle EnumType = MakeStyle(180,180,180);
        }

        private void customizeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var colors = new StyleConfig(this);
            if (colors.ShowDialog() == DialogResult.OK)
            {

                var start = editor.Selection.Start;
                var end = editor.Selection.End;

                editor.ClearStylesBuffer();
                docBox.ClearStylesBuffer();

                TextStyles.Comment = MakeStyle(colors.commentSetting.Color);
                TextStyles.String = MakeStyle(colors.stringSetting.Color);
                TextStyles.Keyword = MakeStyle(colors.keywordSetting.Color);
                TextStyles.ToolTipKeyword = MakeStyle(colors.ttKeywordSetting.Color);
                TextStyles.EnumProperty = MakeStyle(colors.enumPropSetting.Color);
                TextStyles.EnumConstant = MakeStyle(colors.globalConstSetting.Color);
                TextStyles.Number = MakeStyle(colors.numberSetting.Color);
                TextStyles.EnumType = MakeStyle(colors.toolTipEnumType.Color);

                editor.BackColor = colors.backgroundSetting.Color;
                editor.SelectionColor = colors.highlightSetting.Color;
                editor.ForeColor = colors.plainSetting.Color;
                docBox.BackColor = colors.backgroundSetting.Color;
                docBox.SelectionColor = colors.highlightSetting.Color;
                docBox.ForeColor = colors.plainSetting.Color;

                SaveColors();

                editor.Selection.SelectAll();
                docBox.Selection.SelectAll();
                Editor_TextChanged(editor, new TextChangedEventArgs(editor.Selection));
                Editor_TextChanged(docBox, new TextChangedEventArgs(docBox.Selection));
                editor.Selection.Start = start;
                editor.Selection.End = end;
            }
        }

        string HexColor(TextStyle s)
        {
            return (s.ForeBrush as SolidBrush).Color.ToArgb().ToString("X");
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
            sb.AppendLine("Background=" + editor.BackColor.ToArgb().ToString("X"));
            sb.AppendLine("Highlight=" + editor.SelectionColor.ToArgb().ToString("X"));
            sb.AppendLine("Default=" + editor.ForeColor.ToArgb().ToString("X"));
            File.WriteAllText("colors.cfg", sb.ToString());
        }

        private void LoadColors()
        {
            if (!File.Exists("colors.cfg")) return;

            Dictionary<string, string> cfg = new Dictionary<string, string>();

            string[] lines = File.ReadAllLines("colors.cfg");
            foreach (string line in lines)
            {
                string[] split = line.Split('=');
                string prop = split[0];
                string val = split[1];
                cfg[prop] = val;
            }

            Color colorFromHex(string prop) => Color.FromArgb(Convert.ToInt32(cfg[prop], 16));

            TextStyles.Comment = MakeStyle(colorFromHex("Comment"));
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
        }
        
        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            statusLabel.Text = "";
            if (Scripter != null) CodeChanged = true;
            SetStyles(e);
        }

        public void SetStyles(TextChangedEventArgs e, Regex highlight = null)
        {
            e.ChangedRange.ClearStyle(Styles.ToArray());
            e.ChangedRange.SetStyle(TextStyles.Comment, JSRegex.Comment1);
            e.ChangedRange.SetStyle(TextStyles.Comment, JSRegex.Comment2);
            e.ChangedRange.SetStyle(TextStyles.Comment, JSRegex.Comment1);
            e.ChangedRange.SetStyle(TextStyles.String, JSRegex.String);
            e.ChangedRange.SetStyle(TextStyles.String, JSRegex.StringArg);
            e.ChangedRange.SetStyle(TextStyles.Keyword, JSRegex.Keyword);
            e.ChangedRange.SetStyle(TextStyles.Number, JSRegex.Number);
            e.ChangedRange.SetStyle(TextStyles.EnumConstant, JSRegex.GlobalConstant);
            e.ChangedRange.SetStyle(TextStyles.EnumProperty, JSRegex.Property);
            e.ChangedRange.SetFoldingMarkers("{", "}");
            e.ChangedRange.SetFoldingMarkers(@"/\*", @"\*/");
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
            public static Regex Number = new Regex(@"\b\d+[\.]?\d*([eE]\-?\d+)?[lLdDfF]?\b|\b0x[a-fA-F\d]+\b",
                                           RegexCompiledOption);
            public static Regex Keyword =
                new Regex(
                    @"\b(true|false|break|case|catch|const|continue|default|delete|do|else|export|for|function|if|in|instanceof|let|new|null|return|switch|this|throw|try|var|void|while|with|typeof)\b",
                    RegexCompiledOption);
            public static Regex DataType = new Regex(@"\b(byte|short|int|sbyte|ushort|uint|enum|bool)\b", RegexCompiledOption);
        }

        #endregion

        #region ToolTips

        public ToolControl InfoTip { get; set; }  = new ToolControl();
        public Range CurrentTipRange { get; set; }  = null;

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
            Range arguments = editor.Selection.GetFragment(@"[^)(\n]");
            if (CurrentTipRange == null || !arguments.Start.Equals(CurrentTipRange.Start))
            {
                CurrentTipRange = arguments;
                InfoTip.Hide();
            }

            if (arguments.CharBeforeStart == '(')
            {
                int argIndex = 0;
                Range arg = editor.Selection.GetFragment(@"[^)(\n,]");
                while (arg.CharBeforeStart == ',')
                {
                    argIndex++;
                    Place start = arg.Start;
                    start.iChar -= 2;
                    arg = editor.GetRange(start, start).GetFragment(@"[^)(\n,]");
                }
                string funcName = FuncName(arguments);
                LoadDocText(funcName);
                ShowArgToolTip(funcName, arguments, argIndex);
            }
            else
            {
                Range func = editor.Selection.GetFragment(@"\w");
                if (Scripter != null && Scripter.Functions.ContainsKey(func.Text))
                {
                    LoadDocText(func.Text);
                }
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
            if (Scripter != null && Scripter.Functions.ContainsKey(funcName))
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
            return pre.GetFragment(@"\w").Text;
        }

        private string TypeString(long type)
        {
            if (type == 0) return "byte";
            if (type == 1) return "ushort";
            if (type == 2) return "uint";
            if (type == 3) return "sbyte";
            if (type == 4) return "short";
            if (type == 5) return "int";
            if (type == 6) return "float";
            if (type == 8) return "uint";
            throw new Exception("Invalid type in argument definition.");
        }

        private string ArgString(string func, int index = -1)
        {
            (int classIndex, int instrIndex) = Scripter.Functions[func];
            EMEDF.InstrDoc insDoc = Scripter.DOC[classIndex][instrIndex];
            List<string> argStrings = new List<string>();
            for (int i = 0; i < insDoc.Arguments.Length; i++)
            {
                EMEDF.ArgDoc arg = insDoc.Arguments[i];
                if (arg.EnumName == "BOOL")
                {
                    argStrings.Add($"bool {arg.Name}");
                }
                else if (arg.EnumName != null)
                {
                    argStrings.Add($"enum<{arg.EnumName}> {arg.Name}");
                }
                else
                {
                    argStrings.Add($"{TypeString(arg.Type)} {arg.Name}");
                }
                if (i == index) return argStrings.Last();
            }
            return string.Join(", ", argStrings);
        }

        string currentFuncDoc = null;

        private void LoadDocText(string func)
        {
            if (Scripter == null) return;
            if (!Scripter.Functions.ContainsKey(func)) return;
            if (currentFuncDoc == func) return;
            currentFuncDoc = func;
            docBox.Clear();

            (int classIndex, int instrIndex) = Scripter.Functions[func];
            EMEDF.InstrDoc insDoc = Scripter.DOC[classIndex][instrIndex];

            for (int i = 0; i < insDoc.Arguments.Length; i++)
            {
                docBox.AppendText(Environment.NewLine);
                EMEDF.ArgDoc argDoc = insDoc.Arguments[i];

                if (argDoc.EnumName == null)
                {
                    docBox.AppendText($"  {TypeString(argDoc.Type)} ", TextStyles.Keyword);
                    docBox.AppendText(argDoc.Name);
                }
                else if (argDoc.EnumName == "BOOL")
                {
                    docBox.AppendText($"  bool ", TextStyles.Keyword);
                    docBox.AppendText(argDoc.Name);
                }
                else
                {
                    docBox.AppendText($"  enum ", TextStyles.Keyword);
                    docBox.AppendText(argDoc.Name);
                    EMEDF.EnumDoc enm = Scripter.DOC.Enums.First(e => e.Name == argDoc.EnumName);
                    foreach (var kv in enm.Values)
                    {
                        docBox.AppendText(Environment.NewLine);
                        docBox.AppendText($"    {kv.Key.PadLeft(5)}", TextStyles.String);
                        docBox.AppendText($": ");
                        if (Scripter.EnumNamesForGlobalization.Contains(enm.Name))
                        {
                            docBox.AppendText(kv.Value, TextStyles.EnumConstant);
                        }
                        else
                        {
                            docBox.AppendText($"{enm.Name}.");
                            docBox.AppendText(kv.Value, TextStyles.EnumProperty);
                        }
                    }
                }
            }
            
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
            InfoTip.tipBox.Font = editor.Font;
            docBox.Font = editor.Font;
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
            MessageBox.Show("-- Created by AinTunez\r\n-- Based on work by HotPocketRemix and TKGP\r\n-- Special thanks to Meowmaritus", "About DarkScript");
        }
        #endregion

        private void BatchResaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx; *.emevd.js; *.emevd.dcx.js";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                string gameDocs = ChooseGame();
                List<string> failed = new List<string>();
                foreach (var fileName in ofd.FileNames)
                {
                    try
                    {
                        if (File.Exists(fileName + ".js"))
                            OpenJSFile(ofd.FileName + ".js");
                        else
                            OpenEMEVDFile(fileName, null, gameDocs);
                        SaveJSFile();
                        editor.ClearUndo();
                    } catch
                    {
                        failed.Add(fileName);
                    }
                }

                if (failed.Count > 0)
                    MessageBox.Show("The following files failed to save:\r\n\r\n" + string.Join(Environment.NewLine, failed));
                else
                    MessageBox.Show("All files succesfully resaved.");
            }
        }

        private void openAutoCompleteMenuToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (InstructionMenu == null) return;
            InstructionMenu.Show(true);
        }
    }
}
