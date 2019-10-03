using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.ClearScript;
using Microsoft.ClearScript.V8;
using FastColoredTextBoxNS;
using SoulsFormats;


namespace DarkScript3
{
    public partial class GUI : Form
    {
        public EventScripter Scripter;

        public string EVD_Path;

        public CustomToolTip InfoTip = new CustomToolTip();

        public Range CurrentTipRange = null;

        public AutocompleteMenu InstructionMenu;

        Dictionary<string, (string title, string text)> ToolTips = new Dictionary<string, (string, string)>();

        public GUI()
        {
            InitializeComponent();
            InfoTip.GotFocus += (object sender, EventArgs args) => editor.Focus();
            menuStrip.Renderer = new DarkToolStripRenderer();
            statusStrip.Renderer = new DarkToolStripRenderer();
            InfoTip = new CustomToolTip(editor);
            InfoTip.Show();
            InfoTip.Hide();
            docBox.Font = editor.Font;
            editor.Focus();
        }

        private void TipBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.SetStyle(TextStyles.ToolTipKeyword, JSRegex.DataType);
        }

        
        private void ShowTip(string s, Point p, int argIndex = -1)
        {
            if (argIndex > -1)
            {
                var args = Regex.Split(s, @"\s*,\s");
                if (argIndex > args.Length - 1)
                    args[args.Length - 1] = args[args.Length - 1].Replace(" ", " *");
                args[argIndex] = args[argIndex].Replace(" ", " *");
                InfoTip.SetText(string.Join(", ", args));
            } else
            {
                InfoTip.SetText(s);
            }
            p.Offset(0, -InfoTip.Height - 5);
            InfoTip.Location = editor.PointToScreen(p);
            if (!InfoTip.Visible) InfoTip.Show();
            editor.Focus();
        }
        
        private void SaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null) return;
            InfoTip.Hide();
            try
            {
                Scripter.Pack(editor.Text).Write(EVD_Path);
                File.WriteAllText($"{EVD_Path}.js", editor.Text);
                statusLabel.Text = "SAVE SUCCESSFUL";
            }
            catch (Exception ex)
            {
                var scriptException = ex as IScriptEngineException;
                if (scriptException != null)
                {
                    string details = scriptException.ErrorDetails;
                    MessageBox.Show(details);
                }
                else
                {
                    MessageBox.Show(ex.ToString());
                }
                statusLabel.Text = "SAVE FAILED";
            }
        }

        private void OpenToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InfoTip.Hide();
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "EMEVD Files|*.emevd; *.emevd.dcx; *.emevd.js; *";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                GameChooser chooser = new GameChooser();
                chooser.ShowDialog();
                if (ofd.FileName.EndsWith(".js"))
                {
                    OpenJSFile(ofd.FileName, chooser.GameDocs);
                }
                else if (File.Exists(ofd.FileName + ".js"))
                {
                    OpenJSFile(ofd.FileName + ".js", chooser.GameDocs);
                }
                else
                {
                    OpenEMEVDFile(ofd.FileName, chooser.GameDocs);
                }
            }
        }


        private void OpenEMEVDFile(string fileName, string gameDocs)
        {
            Scripter = new EventScripter(fileName, gameDocs);
            EVD_Path = fileName;
            InitUI();
            InfoTip.tipBox.TextChanged += (object sender, TextChangedEventArgs e) => TipBox_TextChanged(sender, e);
            editor.Text = Scripter.Unpack();
            Text = $"DARKSCRIPT 3 - {Path.GetFileName(fileName)}";
        }

        private void OpenJSFile(string fileName, string gameDocs)
        {
            editor.Text = File.ReadAllText(fileName);
            string org = fileName.Substring(0, fileName.Length - 3);
            SFUtil.Backup(org);
            OpenEMEVDFile(org, gameDocs);
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

            Image img = MakeColorImage(Color.FromArgb(255, 255, 255));
            InstructionMenu.ImageList = new ImageList();
            InstructionMenu.ImageList.Images.Add("instruction", img);

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

            InstructionMenu.Items.SetAutocompleteItems(instructions);
            JSRegex.GlobalConstant = new Regex($@"[^.]\b(?<range>{string.Join("|", Scripter.GlobalConstants)})\b");
        }


        private void EmevdDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Scripter == null) return;
            InfoTip.Hide();
            (new InfoViewer(Scripter)).ShowDialog();
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

        private static List<TextStyle> Styles = new List<TextStyle>();

        private static class TextStyles
        {
            public static TextStyle Comment = MakeStyle(87, 166, 74);
            public static TextStyle String = MakeStyle(214, 157, 133);
            public static TextStyle Keyword = MakeStyle(86, 156, 214);
            public static TextStyle ToolTipKeyword = MakeStyle(106, 176, 234);
            public static TextStyle Property = MakeStyle(255, 150, 239);
            public static TextStyle EnumConstant = MakeStyle(78, 201, 176);
            public static TextStyle Number = MakeStyle(181, 206, 168);
            public static TextStyle BoldToolTipKeyword = MakeStyle(106, 176, 234, FontStyle.Bold);
            public static TextStyle BoldNormal = MakeStyle(220, 220, 220, FontStyle.Bold);
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            statusLabel.Text = "";
            SetStyles(e);
        }

        public void SetStyles(TextChangedEventArgs e, bool isToolTip = false)
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
            e.ChangedRange.SetStyle(TextStyles.Property, JSRegex.Property);
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
                    @"\b(true|false|break|case|catch|const|continue|default|delete|do|else|export|for|function|if|in|instanceof|new|null|return|switch|this|throw|try|var|void|while|with|typeof)\b",
                    RegexCompiledOption);
            public static Regex DataType = new Regex(@"\b(byte|short|int|sbyte|ushort|uint|enum)\b", RegexCompiledOption);
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void Editor_ToolTipNeeded(object sender, ToolTipNeededEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.HoveredWord))
            {
                if (ToolTips.ContainsKey(e.HoveredWord))
                {
                    (string title, string text) = ToolTips[e.HoveredWord];
                    Point p = editor.PlaceToPoint(e.Place);
                    string s = title + "\n" + text;
                    Console.WriteLine(s);
                    ShowTip(s, p);
                }
            }
        }   

        private void DocumentationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            InfoTip.Hide();
            display.Panel1Collapsed = !display.Panel1Collapsed;
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
                Range arg = editor.Selection.GetFragment("[^)(\n,]");
                while (arg.CharBeforeStart == ',')
                {
                    argIndex++;
                    var start = arg.Start;
                    start.iChar -= 2;
                    arg = editor.GetRange(start, start).GetFragment("[^)(\n,]");
                }
                string funcName = FuncName(arguments);
                LoadDocText(funcName);
                ShowArgToolTip(arguments, argIndex);
            } else
            {
                Range func = editor.Selection.GetFragment("\\w");
                if (Scripter != null && Scripter.Functions.ContainsKey(func.Text))
                {
                    LoadDocText(func.Text);
                }
            }
        }

        private string FuncName(Range arguments)
        {
            int start = arguments.Start.iChar - 2;
            int line = arguments.Start.iLine;

            Range pre = new Range(editor, start, line, start, line);
            return pre.GetFragment("\\w").Text;
        }

        private void ShowArgToolTip(Range arguments, int argument = -1)
        {
            string funcName = FuncName(arguments);

            if (Scripter != null && Scripter.Functions.ContainsKey(funcName))
            {
                Point point = editor.PlaceToPoint(arguments.Start);
                ShowTip(ArgString(funcName), point, argument);
            }
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
                } else
                {
                    argStrings.Add($"{TypeString(arg.Type)} {arg.Name}");
                }
                if (i == index) return argStrings.Last();
            }


            return string.Join(", ", argStrings);
        }

        private void LoadDocText(string func)
        {
            if (Scripter == null || !Scripter.Functions.ContainsKey(func)) return;

            docBox.Clear();
            docBox.AppendText(func);

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
                            docBox.AppendText(kv.Value, TextStyles.Property);
                        }
                    }
                }
            }
            
        }

        private void Display_Resize(object sender, EventArgs e)
        {
            if (display.SplitterDistance != 350) display.SplitterDistance = 350;
        }

        private void Editor_ZoomChanged(object sender, EventArgs e)
        {
            InfoTip.Hide();
            InfoTip.tipBox.Font = editor.Font;
            docBox.Font = editor.Font;

        }

        private void Editor_FontChanged(object sender, EventArgs e)
        {

        }

        private void Editor_KeyPress(object sender, KeyPressEventArgs e)
        {
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
            }
        }
    }

}
