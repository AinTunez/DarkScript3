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
using System.Text;
using System.ComponentModel;
using Range = FastColoredTextBoxNS.Range;

namespace DarkScript3
{
    public partial class EditorGUI : UserControl
    {
        private string FileVersion;
        private EventScripter Scripter;
        private InstructionDocs Docs;
        private ScriptSettings Settings;
        private bool codeChanged;
        private bool savedAfterChanges;
        private List<FancyJSCompiler.CompileError> LatestWarnings;

        private SharedControls SharedControls;
        private AutocompleteMenu InstructionMenu;
        private Regex globalConstantRegex;

        private Dictionary<string, (string title, string text)> ToolTips = new Dictionary<string, (string, string)>();

        public EditorGUI(SharedControls controls, EventScripter scripter, InstructionDocs docs, ScriptSettings settings, string fileVersion, string text)
        {
            SharedControls = controls;
            Scripter = scripter;
            Docs = docs;
            Settings = settings;
            FileVersion = fileVersion;
            globalConstantRegex = JSRegex.GetGlobalConstantRegex(InstructionDocs.GlobalConstants.Concat(Docs.GlobalEnumConstants.Keys));
            Dock = DockStyle.Fill;
            BackColor = TextStyles.BackColor;
            InitializeComponent();
            editor.Focus();
            editor.SelectionColor = Color.White;
            editor.HotkeysMapping.Add(Keys.Control | Keys.Enter, FCTBAction.CustomAction1);
            editor.HotkeysMapping.Add(Keys.Control | Keys.J, FCTBAction.CustomAction2);
            editor.HotkeysMapping.Add(Keys.Control | Keys.D1, FCTBAction.CustomAction3);
            editor.HotkeysMapping.Add(Keys.Control | Keys.D, FCTBAction.CustomAction4);
            editor.HotkeysMapping.Add(Keys.Control | Keys.Y, FCTBAction.Redo);
            editor.HotkeysMapping.Add(Keys.Control | Keys.Shift | Keys.Z, FCTBAction.Redo);
            editor.Text = text;
            editor.ClearUndo();
            CodeChanged = false;
            InitUI();
            // Colors and font are set after this so that they also apply to the doc box as appropriate.
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

            List<AutocompleteItem> instructions = new List<AutocompleteItem>();
            List<AutocompleteItem> conditions = new List<AutocompleteItem>();
            Dictionary<string, List<string>> instrAliases = Docs.AllAliases
                .GroupBy(e => e.Value)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Key).ToList());
            foreach (string s in Docs.AllArgs.Keys)
            {
                string menuText = s;
                string toolTipTitle = s;
                string toolTipText;
                bool isInstr = false;
                if (Docs.Functions.TryGetValue(s, out (int, int) indices))
                {
                    isInstr = true;
                    toolTipText = $"{indices.Item1}[{indices.Item2:d2}] ({ArgString(s)})";
                }
                // TODO: It shouldn't be necessary to delve into Translator in this case
                else if (Docs.Translator != null && Docs.Translator.ShortDocs.TryGetValue(s, out InstructionTranslator.ShortVariant sv))
                {
                    isInstr = true;
                    toolTipText = $"{sv.Cmd} ({ArgString(s)})";
                }
                else
                {
                    toolTipText = $"({ArgString(s)})";
                }
                ToolTips[menuText] = (toolTipTitle, toolTipText);
                if (instrAliases.TryGetValue(menuText, out List<string> aliases))
                {
                    foreach (string alias in aliases)
                    {
                        ToolTips[alias] = ToolTips[menuText];
                    }
                }

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
        }

        // Info for GUI
        public string DisplayTitle => Scripter.EmevdFileName + (CodeChanged ? "*" : "");
        public string EMEVDPath => Scripter.EMEVDPath;
        public string ResourceString => Docs.ResourceString;

        private bool CodeChanged
        {
            get => codeChanged;
            set
            {
                bool changeChanged = codeChanged != value;
                codeChanged = value;
                if (changeChanged)
                {
                    TitleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Description("Occurs when the tab's title has changed due to unsaved changes")]
        public event EventHandler TitleChanged;

        #region File Handling

        public bool SaveJSAndEMEVDFile()
        {
            // Before anything else, make sure Oodle exists. This is also done when opening new files.
            SharedControls.CheckOodle(Docs.ResourceString);
            Range originalSelect = editor.Selection.Clone();
            Range errorSelect = null;
            bool success = false;
            try
            {
                SharedControls.LockEditor();
                success = SaveJSAndEMEVDFileOperation(out errorSelect);
            }
            finally
            {
                SharedControls.UnlockEditor();
            }

            editor.Focus();
            if (errorSelect == null)
            {
                editor.Selection = originalSelect;
            }
            else
            {
                PreventHoverMousePosition = MousePosition;
                editor.Selection = errorSelect;
                editor.DoSelectionVisible();
            }
            return success;
        }

        public bool MayBeFancy()
        {
            return editor.GetRanges(@"\$Event\(").Count() > 0 && Settings.AllowPreprocess;
        }

        private bool SaveJSAndEMEVDFileOperation(out Range errorSelect)
        {
            string text = editor.Text;
            bool fancyHint = text.Contains("$Event(");
            errorSelect = null;
            try
            {
                EMEVD result;
                FancyJSCompiler.CompileOutput output = null;
                if (fancyHint && Settings.AllowPreprocess && Docs.Translator != null)
                {
                    result = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).Pack(text, Scripter.JsFileName, out output);
                }
                else
                {
                    result = Scripter.Pack(text, Scripter.JsFileName);
                }
                if (File.Exists(Scripter.EMEVDPath))
                {
                    SFUtil.Backup(Scripter.EMEVDPath);
                }
                result.Write(Scripter.EMEVDPath);
                SaveJSFile();
                string saveInfo = "";
                if (output != null && output.Warnings != null && output.Warnings.Count > 0)
                {
                    LatestWarnings = output.Warnings;
                    saveInfo += ". Use Ctrl+J to view warnings";
                }
                SharedControls.SetStatus("SAVE SUCCESSFUL" + saveInfo);
                if (editor.UndoEnabled)
                {
                    savedAfterChanges = true;
                }
                CodeChanged = false;
                return true;
            }
            catch (Exception ex)
            {
                // Mainly these exceptions will be JSScriptException, from V8, and FancyCompilerException, from JS parsing/compilation.
                SharedControls.SetStatus("SAVE FAILED");
                StringBuilder extra = new StringBuilder();
                if (ex is JSScriptException && fancyHint && !Settings.AllowPreprocess)
                {
                    extra.AppendLine(Environment.NewLine);
                    extra.Append("($Event is used but preprocessing is disabled. Enable it in compilation settings if desired.)");
                }
                if (ProgramVersion.CompareVersions(ProgramVersion.VERSION, FileVersion) != 0)
                {
                    extra.AppendLine(Environment.NewLine);
                    extra.Append(ProgramVersion.GetCompatibilityMessage(Scripter.JsFileName, Docs.ResourceString, FileVersion));
                }
                errorSelect = ShowCompileError(Scripter.JsFileName, ex, extra.ToString());
                return false;
            }
        }

        public bool CancelWithUnsavedChanges()
        {
            if (CodeChanged)
            {
                DialogResult result = MessageBox.Show($"Save {Scripter.EmevdFileName}?", "Unsaved Changes", MessageBoxButtons.YesNoCancel);
                if (result == DialogResult.Yes)
                {
                    SaveJSAndEMEVDFile();
                }
                else if (result == DialogResult.Cancel)
                {
                    return true;
                }
            }
            return false;
        }

        public void SaveJSFile()
        {
            var sb = new StringBuilder();
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
            File.WriteAllText($"{Scripter.EMEVDPath}.js", sb.ToString());
            FileVersion = ProgramVersion.VERSION;
        }

        #endregion

        #region Highlighting

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            SharedControls.ResetStatus(true);
            CodeChanged = true;
            LatestWarnings = null;
            if (SharedControls.Preview != null)
            {
                SharedControls.Preview.DisableConversion();
            }
            SetStyles(e.ChangedRange);
            SetHighlightRange(null, null);
            RecalculateHighlightTokenDelayed();
        }

        private void Editor_UndoRedoStateChanged(object sender, EventArgs e)
        {
            // We can allow exit without confirmation if the undo stack is empty and there's been no save since then.
            // The undo stack is internal to the editor so it's not possible to see if any arbitrary position is the last-saved state.
            if (!editor.UndoEnabled && !savedAfterChanges)
            {
                CodeChanged = false;
            }
        }

        public void RefreshGlobalStyles(FastColoredTextBox tb = null)
        {
            tb = tb ?? editor;
            tb.ClearStylesBuffer();
            tb.Font = TextStyles.Font;
            tb.SelectionColor = TextStyles.SelectionColor;
            tb.BackColor = TextStyles.BackColor;
            tb.ForeColor = TextStyles.ForeColor;
            SetStyles(tb.Range);
        }

        public void SetStyles(Range range)
        {
            range.ClearStyle(TextStyles.SyntaxStyles.ToArray());
            range.SetStyle(TextStyles.Comment, JSRegex.Comment1);
            range.SetStyle(TextStyles.Comment, JSRegex.Comment2);
            range.SetStyle(TextStyles.Comment, JSRegex.Comment3);
            range.SetStyle(TextStyles.String, JSRegex.String);
            range.SetStyle(TextStyles.String, JSRegex.StringArg);
            range.SetStyle(TextStyles.Keyword, JSRegex.Keyword);
            range.SetStyle(TextStyles.Number, JSRegex.Number);
            range.SetStyle(TextStyles.EnumConstant, globalConstantRegex);
            range.SetStyle(TextStyles.EnumProperty, JSRegex.Property);
            range.SetFoldingMarkers("{", "}");
            range.SetFoldingMarkers(@"/\*", @"\*/");
        }

        #endregion

        #region Selection

        // Selection state
        private Range CursorTipRange;
        private Range HoverTipRange;
        private Point? PreventHoverMousePosition;
        private Point CheckMouseMovedPosition;
        private Range CurrentTokenRange;
        private string CurrentToken;

        private Action<Point> moveOutOfBounds;
        private void Editor_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Location == CheckMouseMovedPosition)
            {
                // For some reason, MouseMove events unnecessarily activate while typing,
                // so ignore these to avoid unnecessarily dismissing the tooltip.
                return;
            }
            CheckMouseMovedPosition = e.Location;
            if (moveOutOfBounds == null)
            {
                moveOutOfBounds = SharedControls.Debounce((Action<Point>)OnOutOfBoundsToolTip, editor.ToolTipDelay + 10);
            }
            moveOutOfBounds(e.Location);
        }

        private void OnOutOfBoundsToolTip(Point p)
        {
            // For some reason, FCTB does not cancel a tooltip when hovering past the end of a line, so do it ourselves.
            Place place = editor.PointToPlace(p);
            if (place.iChar == editor.GetLineLength(place.iLine))
            {
                Editor_ToolTipNeeded(editor, new ToolTipNeededEventArgs(place, ""));
            }
        }

        private void Editor_ToolTipNeeded(object sender, ToolTipNeededEventArgs e)
        {
            if (PreventHoverMousePosition != null)
            {
                if (MousePosition.Equals(PreventHoverMousePosition))
                {
                    return;
                }
                PreventHoverMousePosition = null;
            }
            if (!string.IsNullOrEmpty(e.HoveredWord) && ToolTips.ContainsKey(e.HoveredWord))
            {
                // Don't reposition if it's the same range. Use the same logic FCTB does for generating HoveredWord.
                Range tipRange = editor.GetRange(e.Place, e.Place).GetFragment("[a-zA-Z]");
                if (HoverTipRange != null && HoverTipRange.Start.Equals(tipRange.Start))
                {
                    return;
                }
                (string title, string text) = ToolTips[e.HoveredWord];
                Point p = editor.PlaceToPoint(e.Place);
                // Translate a bit rightward to make vertical mouse movement change hover.
                p.Offset(2, 0);
                string s = title + "\n" + text;
                ShowTip(s, p);
                HoverTipRange = tipRange;
            }
            else
            {
                if (SharedControls.InfoTip.Visible && CursorTipRange != null && CursorTipRange.Contains(e.Place))
                {
                    return;
                }
                SharedControls.HideTip();
                HoverTipRange = null;
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
            if (CursorTipRange == null || !arguments.Start.Equals(CursorTipRange.Start))
            {
                CursorTipRange = arguments;
                SharedControls.HideTip();
            }

            SharedControls.ResetStatus(false);

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
            int argIndex = -1;
            if (arguments.CharBeforeStart == '(' && editor.GetRange(arguments.End, arguments.End).CharAfterStart != '(')
            {
                // Scan leftward through arguments until no commas remain.
                // This does not work with nested calls, like IfThing(getMyCustomConstant(), ^1)
                argIndex = 0;
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
            SharedControls.LoadDocText(funcName, argIndex, Docs, false);
        }

        private void ShowTip(string s, Point p, int argIndex = -1)
        {
            ToolControl InfoTip = SharedControls.InfoTip;
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

            InfoTip.ShowAtPosition(editor, p, editor.CharHeight);
            editor.Focus();
        }

        private void ShowArgToolTip(string funcName, Range arguments, int argument = -1)
        {
            if (!Properties.Settings.Default.ArgTooltip)
            {
                return;
            }
            if (Docs.AllAliases.TryGetValue(funcName, out string realName))
            {
                funcName = realName;
            }
            if (Docs.AllArgs.ContainsKey(funcName))
            {
                Point point = editor.PlaceToPoint(arguments.Start);
                ShowTip(ArgString(funcName), point, argument);
            }
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
            // Require Ctrl to be pressed when mouse down started, otherwise Ctrl+Mousedown+Select+Mouseup+C, to copy text, will jump
            // If this gets complicated enough, probably just copy Visual Studio
            if (!ctrlMouseDown)
            {
                return;
            }
            Place place = editor.PointToPlace(e.Location);
            JumpToEvent(place);
        }

        private bool ctrlMouseDown;
        private void editor_MouseDown(object sender, MouseEventArgs e)
        {
            ctrlMouseDown = (ModifierKeys & Keys.Control) != 0;
        }

        public void ReplaceFloat()
        {
            Place place = editor.Selection.Start;
            Range numRange = editor.GetRange(place, place).GetFragment(TextStyles.Number, false);
            string text = numRange.Text;
            int val;
            if (int.TryParse(text, out int id))
            {
                val = id;
            }
            else if (uint.TryParse(text, out uint uid))
            {
                val = (int)uid;
            }
            else
            {
                MessageBox.Show($"Integer value not found at text cursor (found \"{text}\")", "Parse float failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            float f = BitConverter.ToSingle(BitConverter.GetBytes(val), 0);
            // Temporarily disable this until it's actually supported in initializations
            string fval = f.ToString("R");
            int val2 = BitConverter.ToInt32(BitConverter.GetBytes(float.Parse(fval)), 0);
            if (val != val2)
            {
                MessageBox.Show($"Integer value {text} does not correspond to a unique float ({fval} loses precision)", "Parse float failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult res = ScrollDialog.Show(ParentForm, $"Integer {text} is equivalent to float {fval}\n\nReplace it for event initialization? (Don't do this unless you're sure it's a float. Typed initializations are coming in a future version.)", "Confirm replacement");
            if (res == DialogResult.OK)
            {
                editor.Selection = numRange;
                editor.InsertText($"floatArg({fval})");
            }
        }

        public void JumpToEvent()
        {
            JumpToEvent(editor.Selection.Start);
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
                PreventHoverMousePosition = MousePosition;
                editor.Selection = editor.GetRange(idRange.Start, idRange.Start);
                // Update the last visited line; can't be done directly so call where it's done.
                editor.OnSelectionChangedDelayed();
                editor.DoSelectionVisible();
                string extraText = ranges.Count == 1 ? "" : $" (1 of {ranges.Count})";
                SharedControls.SetStatus($"Event {id}{searchType} found{extraText}. Use Ctrl+- to navigate back", false);
            }
            else
            {
                // Fallback to common_func, if it's open and in the same directory.
                // This requires communicating with other tabs which should be done carefully to avoid circular dependencies.
                string commonSuggest = "";
                if (searchType == "")
                {
                    if (SharedControls.JumpToCommonFunc(id, this))
                    {
                        return;
                    }
                    if (Docs != null && Docs.LooksLikeCommonFunc(id))
                    {
                        commonSuggest = ". Open common_func in another tab to enable searching there";
                    }
                }
                SharedControls.SetStatus($"Event {id}{searchType} not found{commonSuggest}", false);
            }
        }

        public void JumpToTextNearLine(string lineText, int lineChar, int lineHint)
        {
            Regex regex = new Regex($"^{Regex.Escape(lineText)}$", RegexOptions.Singleline);
            List<Range> ranges = editor.Range.GetRangesByLines(regex).ToList();
            Range target = null;
            if (ranges.Count > 0)
            {
                int closest = -1;
                foreach (Range range in ranges)
                {
                    int dist = Math.Abs(range.FromLine - lineHint);
                    if (closest == -1 || dist < closest)
                    {
                        Range lineRange = range;
                        Place linePlace = range.Start;
                        int lineLen = editor.GetLineLength(linePlace.iLine);
                        if (lineChar < lineLen)
                        {
                            linePlace = new Place(lineChar, linePlace.iLine);
                            lineRange = editor.GetRange(linePlace, linePlace);
                        }
                        target = lineRange;
                        closest = dist;
                    }
                }
            }
            if (target == null)
            {
                try
                {
                    target = editor.GetLine(lineHint);
                }
                catch (ArgumentException) { }
            }
            if (target == null)
            {
                target = editor.GetLine(0);
            }
            PreventHoverMousePosition = MousePosition;
            editor.Selection = editor.GetRange(target.End, target.End);
            editor.OnSelectionChangedDelayed();
            editor.DoSelectionVisible();
        }

        public Action GetJumpToEventAction(int id)
        {
            // Used for common_func only. Some stuff copied from the above. The Action should be run in a UI thread.
            Regex regex = new Regex($@"^\s*\$?Event\(\s*{id}\s*,", RegexOptions.Singleline);
            List<Range> ranges = editor.Range.GetRangesByLines(regex).ToList();
            if (ranges.Count > 0)
            {
                Range idRange = ranges[0].GetRanges($"{id}").LastOrDefault() ?? ranges[0];
                return () =>
                {
                    try
                    {
                        // Quick validity check
                        string text = idRange.Text;
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        return;
                    }
                    PreventHoverMousePosition = MousePosition;
                    editor.Selection = editor.GetRange(idRange.Start, idRange.Start);
                    editor.OnSelectionChangedDelayed();
                    editor.DoSelectionVisible();
                    SharedControls.SetStatus($"Event {id} found", false);
                };
            }
            return null;
        }

        private Action<int> highlightTokenDebounce;
        private void RecalculateHighlightTokenDelayed()
        {
            if (highlightTokenDebounce == null)
            {
                highlightTokenDebounce = SharedControls.Debounce((Action<int>)CalculateHighlightToken, 300);
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
                if (styles.Count > 0)
                {
                    candidate = TextStyles.HighlightStyles.Find(style => styles.Contains(style));
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

        private void DuplicateSelection()
        {
            Range sel = editor.Selection.Clone();
            string text;
            Place insertPlace;
            if (sel.Start == sel.End)
            {
                // Insert new line
                Range fullLine = editor.GetLine(sel.Start.iLine);
                insertPlace = fullLine.End;
                text = Environment.NewLine + fullLine.Text;
            }
            else
            {
                // Insert after current selection
                insertPlace = sel.Start > sel.End ? sel.Start : sel.End;
                text = sel.Text;
            }
            Range replaceRange = editor.GetRange(insertPlace, insertPlace);
            Range resultRange = editor.InsertTextAndRestoreSelection(replaceRange, text, editor.Styles[0]);
            SetStyles(resultRange);
            // The selection itself is doubled in the second case, which is largely against the point of this functionality
            // So restore the original selection, even if it's not saved on undo/redo
            editor.Selection = sel;
        }

        #endregion

        #region Text Handling

        private void editor_AutoIndentNeeded(object sender, AutoIndentEventArgs args)
        {
            JSAutoIndentNeeded(args);
        }

        private void JSAutoIndentNeeded(AutoIndentEventArgs args)
        {
            // From https://github.com/PavelTorgashov/FastColoredTextBox/blob/master/FastColoredTextBox/SyntaxHighlighter.cs
            // CSharpAutoIndentNeeded as of Oct 2021.
            // block {}
            if (Regex.IsMatch(args.LineText, @"^[^""']*\{.*\}[^""']*$"))
            {
                return;
            }
            // start of block {}
            bool startBlock = Regex.IsMatch(args.LineText, @"^[^""']*\{");
            // end of block {}
            bool endBlock = Regex.IsMatch(args.LineText, @"}[^""']*$");
            if (startBlock && endBlock)
            {
                // Unlike C#, JS can have both on one line, like "} else {"
                args.Shift = -args.TabLength;
                return;
            }
            if (startBlock)
            {
                args.ShiftNextLines = args.TabLength;
                return;
            }
            if (endBlock)
            {
                args.Shift = -args.TabLength;
                args.ShiftNextLines = -args.TabLength;
                return;
            }
            // label
            if (Regex.IsMatch(args.LineText, @"^\s*\w+\s*:\s*($|//)") &&
                !Regex.IsMatch(args.LineText, @"^\s*default\s*:"))
            {
                args.Shift = -args.AbsoluteIndentation; // -args.TabLength;
                return;
            }
            // some statements: case, default
            if (Regex.IsMatch(args.LineText, @"^\s*(case|default)\b.*:\s*($|//)"))
            {
                args.Shift = -args.TabLength / 2;
                return;
            }
            // is unclosed operator in previous line ?
            if (Regex.IsMatch(args.PrevLineText, @"^\s*(if|for|foreach|while|[\}\s]*else)\b[^{]*$"))
            {
                if (!Regex.IsMatch(args.PrevLineText, @"(;\s*$)|(;\s*//)")) //operator is unclosed
                {
                    args.Shift = args.TabLength;
                    return;
                }
            }
        }

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

        #endregion

        #region Misc GUI Events

        private void Editor_CustomAction(object sender, CustomActionEventArgs e)
        {
            if (e.Action == FCTBAction.CustomAction1)
            {
                JumpToEvent();
            }
            else if (e.Action == FCTBAction.CustomAction2)
            {
                ShowCompileWarnings();
            }
            else if (e.Action == FCTBAction.CustomAction3)
            {
                ReplaceFloat();
            }
            else if (e.Action == FCTBAction.CustomAction4)
            {
                DuplicateSelection();
            }
        }

        public string GetDocName()
        {
            return Docs.ResourceString.Split('-')[0];
        }

        public void Cut() => editor.Cut();

        public void Copy() => editor.Copy();

        public void Paste() => editor.Paste();

        public void ShowReplaceDialog() => editor.ShowReplaceDialog();

        public void SelectAll() => editor.SelectAll();

        // TODO: There are a few improvements we can make here, like: multi-file search form,
        // highlighting search terms, non-dialog search (Google Chrome style Ctrl+F).
        public void ShowFindDialog(bool complex = false)
        {
            string text = null;
            if (!editor.Selection.IsEmpty && editor.Selection.Start.iLine == editor.Selection.End.iLine)
            {
                text = editor.Selection.Text;
            }
            if (complex)
            {
                SharedControls.ShowComplexFindDialog(text);
            }
            else
            {
                SharedControls.ShowFindDialog(text);
            }
        }

        public void ShowEMEVDData()
        {
            new InfoViewer(Scripter).ShowDialog();
        }

        public void ShowInstructionMenu()
        {
            if (InstructionMenu == null) return;
            InstructionMenu.Show(true);
        }

        public void ShowScriptSettings()
        {
            if (Settings == null) return;
            ScriptSettingsForm form = new ScriptSettingsForm(Settings);
            form.ShowDialog();
        }

        public void ShowCompileWarnings()
        {
            if (LatestWarnings == null)
            {
                return;
            }
            if (ShowCompileError(Scripter.JsFileName, new FancyJSCompiler.FancyCompilerException { Errors = LatestWarnings, Warning = true }, "") is Range errorSelect)
            {
                PreventHoverMousePosition = MousePosition;
                editor.Selection = errorSelect;
                editor.DoSelectionVisible();
            }
        }

        public void ShowPreviewDecompile()
        {
            if (Docs.Translator == null) return;
            try
            {
                string text = editor.Text;
                new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).Repack(text, out FancyJSCompiler.CompileOutput output);
                PreviewCompilationForm preview = RefreshPreviewCompilationForm();
                preview.SetSegments(output.GetDiffSegments(), text, output.Code);
                preview.Show();
                preview.Focus();
            }
            catch (FancyJSCompiler.FancyCompilerException ex)
            {
                if (ShowCompileError(Scripter.JsFileName, ex, "") is Range errorSelect)
                {
                    PreventHoverMousePosition = MousePosition;
                    editor.Selection = errorSelect;
                    editor.DoSelectionVisible();
                }
            }
        }

        public void ShowPreviewCompile()
        {
            if (Docs.Translator == null) return;
            try
            {
                List<FancyJSCompiler.DiffSegment> segments = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).PreviewPack(editor.Text);
                PreviewCompilationForm preview = RefreshPreviewCompilationForm();
                preview.SetSegments(segments);
                preview.Show();
                preview.Focus();
            }
            catch (FancyJSCompiler.FancyCompilerException ex)
            {
                if (ShowCompileError(Scripter.JsFileName, ex, "") is Range errorSelect)
                {
                    PreventHoverMousePosition = MousePosition;
                    editor.Selection = errorSelect;
                    editor.DoSelectionVisible();
                }
            }
        }

        public PreviewCompilationForm RefreshPreviewCompilationForm()
        {
            PreviewCompilationForm preview = SharedControls.RefreshPreviewCompilationForm(editor.Font);
            preview.FormClosed += (sender, ev) =>
            {
                if (preview.Confirmed && preview.PendingCode != null && editor.Text == preview.StartingCode)
                {
                    editor.Text = preview.PendingCode;
                    if (Settings != null && !Settings.AllowPreprocess)
                    {
                        Settings.AllowPreprocess = true;
                    }
                }
            };
            return preview;
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                // Is this necessary?
                SharedControls.HideTip();
            }
            else if (e.KeyCode == Keys.F && e.Control)
            {
                // Can this be done with actions?
                ShowFindDialog();
                e.Handled = true;
            }
        }

        private void Editor_Scroll(object sender, ScrollEventArgs e) => SharedControls.HideTip();

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

        #endregion
    }
}
