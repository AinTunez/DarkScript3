using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using FastColoredTextBoxNS;
using SoulsFormats;
using static DarkScript3.DocAutocomplete;
using Range = FastColoredTextBoxNS.Range;

namespace DarkScript3
{
    public partial class EditorGUI : UserControl
    {
        private string FileVersion;
        private readonly EventScripter Scripter;
        private readonly InstructionDocs Docs;
        private readonly ScriptSettings Settings;
        private readonly InitData.Links Links;
        private readonly AutocompleteContext AutoContext;
        private bool codeChanged;
        private bool savedAfterChanges;
        private List<Exception> LatestWarnings;

        private readonly SharedControls SharedControls;
        private readonly AutocompleteMenu InstructionMenu;
        private readonly Regex globalConstantRegex;

        private readonly Dictionary<string, (string title, string text)> ToolTips = new();

        public EditorGUI(
            SharedControls controls,
            EventScripter scripter,
            InstructionDocs docs,
            ScriptSettings settings,
            string fileVersion,
            string text,
            InitData.Links links)
        {
            SharedControls = controls;
            Scripter = scripter;
            Docs = docs;
            Settings = settings;
            Links = links;
            AutoContext = new AutocompleteContext(docs.ResourceString, scripter.EmevdFileName);
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
            InstructionMenu = new AutocompleteMenu(editor);
            InitUI();
            // Colors and font are set after this so that they also apply to the doc box as appropriate.
        }

        private void InitUI()
        {
            InstructionMenu.BackColor = Color.FromArgb(37, 37, 38);
            InstructionMenu.ForeColor = Color.FromArgb(240, 240, 240);
            InstructionMenu.SelectedColor = Color.FromArgb(0, 122, 204);
            // Width previously 250.
            // Both of these seem to be needed for changing the width, but it is not dynamic.
            InstructionMenu.Items.MaximumSize = new Size(500, 300);
            InstructionMenu.Items.Width = 500;
            InstructionMenu.AllowTabKey = true;
            InstructionMenu.AlwaysShowTooltip = false;
            InstructionMenu.ToolTipDuration = 1;
            InstructionMenu.AppearInterval = 250;
            // Override for dynamic system, it does its own filtering
            InstructionMenu.MinFragmentLength = 0;
            InstructionMenu.Selected += (e, args) =>
            {
                if (args.Item is DocAutocompleteItem item)
                {
                    item.MarkSelected(AutoContext);
                    if (item.IsFunction)
                    {
                        // Auto-complete immediately after selection if a ( was inserted
                        InstructionMenu.Show(false);
                        FetchAutocomplete();
                    }
                }
            };
            InstructionMenu.Opened += (e, args) =>
            {
                // Arg tooltip may come up immediately in argument context.
                // Argument autocomplete may come up very shortly after, so cancel tooltip.
                SharedControls.HideTip();
            };

            InstructionMenu.ImageList = new ImageList();
            // Colors match the HTML emedf documentation, and also indices match AutocompleteCategory enum
            InstructionMenu.ImageList.Images.Add("instruction", MakeColorImage(Color.FromArgb(0xFF, 0xFF, 0xB3)));
            InstructionMenu.ImageList.Images.Add("condition", MakeColorImage(Color.FromArgb(0xFF, 0xFF, 0xFF)));
            InstructionMenu.ImageList.Images.Add("enum", MakeColorImage(Color.FromArgb(0xE0, 0xB3, 0xFF)));
            InstructionMenu.ImageList.Images.Add("object", MakeColorImage(Color.FromArgb(0xFD, 0xCC, 0xD5)));

            // Create items for EMEDF. This can probably be shared between different tabs if done carefully.
            List <DocAutocompleteItem> items = new List<DocAutocompleteItem>();
            Dictionary<string, List<string>> instrAliases = Docs.AllAliases
                .GroupBy(e => e.Value)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Key).ToList());
            bool isCondInstr(string id)
            {
                if (Docs.Translator == null) return false;
                return Docs.Translator.Selectors.ContainsKey(id) || Docs.Translator.LabelDocs.ContainsKey(id);
            }
            foreach (string s in Docs.CallArgs.Keys)
            {
                string menuText = s;
                string toolTipTitle = s;
                string toolTipText;
                string instrId = null;
                bool lowQuality = false;
                InitData.DocID docId = new(s);
                InstructionTranslator.ShortVariant sv = null;
                // Perhaps some of the logic here should be hidden within Translator
                if (Docs.Functions.TryGetValue(s, out (int, int) indices))
                {
                    instrId = $"{indices.Item1}[{indices.Item2:d2}]";
                    toolTipText = $"{instrId} ({ArgString(docId)})";
                    if (Docs.Translator != null && Docs.Translator.ShortSelectors.ContainsKey(instrId))
                    {
                        // If there are short selectors, prefer them
                        lowQuality = true;
                    }
                }
                else if (Docs.Translator != null && Docs.Translator.ShortDocs.TryGetValue(s, out sv))
                {
                    instrId = sv.Cmd;
                    toolTipText = $"{instrId} ({ArgString(docId)})";
                }
                else if (Docs.Translator != null && Docs.Translator.CondDocs.TryGetValue(s, out InstructionTranslator.FunctionDoc funcDoc))
                {
                    toolTipText = $"({ArgString(docId)})";
                }
                else
                {
#if DEBUG
                    // This should cover all AllArgs keys, but duck out in release just in case.
                    throw new Exception($"Unknown {s} in AllArgs");
#else
                    continue;
#endif
                }
                ToolTips[menuText] = (toolTipTitle, toolTipText);
                if (instrAliases.TryGetValue(menuText, out List<string> aliases))
                {
                    foreach (string alias in aliases)
                    {
                        ToolTips[alias] = ToolTips[menuText];
                    }
                }
                string initName = "$" + menuText;
                if (Docs.TypedInitIndex.ContainsKey(initName))
                {
                    ToolTips[initName] = ToolTips[menuText];
                }

                if (instrId != null)
                {
                    FancyContextType fancy = FancyContextType.Any;
                    if (isCondInstr(instrId))
                    {
                        fancy = FancyContextType.RegularOnly;
                    }
                    else if (sv != null)
                    {
                        // Short versions currently only available with translator
                        fancy = FancyContextType.FancyOnly;
                    }
                    items.Add(new DocAutocompleteItem(s, menuText, AutocompleteCategory.Instruction, fancy, lowQuality, null));
                }
                else
                {
                    // Detection if this is the main condition whilst variants exist
                    if (Docs.Translator != null
                        && Docs.Translator.CondDocs.TryGetValue(s, out InstructionTranslator.FunctionDoc funcDoc)
                        && funcDoc.ConditionDoc.Name == s
                        && funcDoc.ConditionDoc.HasVariants)
                    {
                        lowQuality = true;
                    }
                    items.Add(new DocAutocompleteItem(s, menuText, AutocompleteCategory.Condition, FancyContextType.FancyOnly, lowQuality, null));
                }
            }
            items.AddRange(Docs.Enums.Values
                // Don't autocomplete booleans, to allow showing the argument name inline
                .Where(e => e.Name != "BOOL")
                .SelectMany(e =>
                    e.DisplayValues.Values.Select(s =>
                        new DocAutocompleteItem(s, s, AutocompleteCategory.Enum, FancyContextType.Any, false, e.Name))));

            // Finally, builtins. Only fancy ones for now.
            items.AddRange(ScriptAst.ReservedWords
                .Where(e => e.Value.ControlStatement)
                .Select(e => new DocAutocompleteItem(e.Key, e.Key, AutocompleteCategory.Instruction, FancyContextType.FancyOnly, false, null)));

            // Default sort for speedier future OrderBy sort
            items.Sort();
            InstructionMenu.Items.SetAutocompleteItems(new DocAutocompleteItemList(items, editor, Docs, Links, SharedControls.Metadata, AutoContext));
        }

        // Info for GUI
        public string DisplayTitle => Scripter.EmevdFileName + (CodeChanged ? "*" : "");
        public string DisplayTitleWithDir => Scripter.EmevdFileName + (CodeChanged ? "*" : "") + " - " + Scripter.EmevdFileDir;
        public string EMEVDPath => Scripter.EmevdPath;
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

        private bool SaveJSAndEMEVDFileOperation(out Range errorSelect)
        {
            string text = editor.Text;
            bool fancyHint = text.Contains("$Event(");
            errorSelect = null;
            try
            {
                EMEVD result;
                FancyJSCompiler.CompileOutput output = null;
                // TODO: Could prompt for links again
                if (fancyHint && Settings.AllowPreprocess && Docs.Translator != null)
                {
                    result = new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).Pack(text, Links, out output);
                }
                else
                {
                    result = Scripter.Pack(text, Links);
                }
                if (File.Exists(Scripter.EmevdPath))
                {
                    SFUtil.Backup(Scripter.EmevdPath);
                }
                result.Write(Scripter.EmevdPath);
                SaveJSFile();
                Scripter.UpdateLinksAfterPack(Links);
                if ((output != null && output.Warnings.Count > 0) || Scripter.PackWarnings.Count > 0)
                {
                    LatestWarnings = new();
                    if (output != null)
                    {
                        LatestWarnings.Add(new FancyJSCompiler.FancyCompilerException { Errors = output.Warnings });
                    }
                    LatestWarnings.AddRange(Scripter.PackWarnings);
                    SharedControls.SetStatus("SAVED WITH WARNINGS. Use Ctrl+J to view warnings");
                }
                else
                {
                    SharedControls.SetStatus("SAVE SUCCESSFUL");
                }
                // Clear state
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
                errorSelect = ShowCompileError(Scripter.JsFileName, new List<Exception>() { ex }, extra.ToString());
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
            HeaderData header = HeaderData.Create(Scripter, Docs, Settings.SettingsDict);
            header.Write(sb, Docs);
            sb.AppendLine(editor.Text);
            File.WriteAllText($"{Scripter.EmevdPath}.js", sb.ToString());
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
            tb.Font = TextStyles.FontFor(tb);
            tb.SelectionColor = TextStyles.SelectionColor;
            tb.BackColor = TextStyles.BackColor;
            tb.ForeColor = TextStyles.ForeColor;
            SetStyles(tb.Range);
        }

        private void SetStyles(Range range)
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

        // Range used for determining text-cursor-based tooltip, mainly to avoid it getting repositioned
        // unnecessarily, and to avoid hover-based logic from removing it as well.
        private Range CursorTipRange;
        // Range used for avoiding recomputing hover-based tooltip, when the word is the same.
        // Also used for asynchronous fetches, to make sure the result is still applicable.
        private Range HoverTipRange;
        // Indication to not recompute the hover tooltip yet, on editor jump or on click text.
        private Point? PreventHoverMousePosition;
        // Used for detecting if mouse has actually moved when MouseMove is called, for out-of-bounds hover tooltip removal.
        private Point CheckMouseMovedPosition;
        // Highlight token, and a range to prevent unnecessarily recalculating it
        private Range CurrentTokenRange;
        private string CurrentToken;
        // Signal that autocomplete logic will run shortly, so prefetch any possible results
        // as soon as we recalculate the current func arg name.
        private bool PrecomputeAutocomplete;

        // Metadata support. This is integrated in the UI in 3 places:
        // 1. Hover on integer
        // 2. Precompute when typing
        // 3. Autocomplete after precompute
        private static readonly List<string> metadataTypes =
            new List<string> { "param", "fmg", "entity", "mapint", "mapparts", "eventflag", "eventid" };

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
                // Normally HoveredWord is not null, so be careful.
                Editor_ToolTipNeeded(editor, new ToolTipNeededEventArgs(place, null));
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
            // e.HoveredWord is an [a-zA-Z] (hardcoded in FCTB), though we want to make the same word either apply to
            // ToolTips (function names) or to various data we can look up and cache.
            Range tipRange = editor.GetRange(e.Place, e.Place).GetFragment("[a-zA-Z0-9_.$]");
            string hoveredWord = e.HoveredWord == null ? "" : tipRange.Text;
            Point p = editor.PlaceToPoint(e.Place);
            if (ToolTips.ContainsKey(hoveredWord))
            {
                // Don't reposition/recalculate if it's the same range. Use the same logic FCTB does for generating HoveredWord.
                // This does mean that if a tooltip was hidden by some other means (e.g. selection change), it won't come back
                // until moving away and then back.
                if (HoverTipRange != null && HoverTipRange.Start.Equals(tipRange.Start))
                {
                    // Don't change the tooltip if it matches the last hovered tooltip location.
                    // This has a weird relationship with the argument tooltip, because it can "remember" the last hover
                    // tooltip from before the arg tooltip was shown, and keep the arg tooltip visible as a result.
                    // TODO: Can this be simplified to be more consistent?
                    return;
                }
                string s = null;
                if (Docs.TypedInitIndex.ContainsKey(hoveredWord))
                {
                    ParseFuncAtRange(tipRange, Docs, out InitData.DocID docId, out _);
                    if (docId.Event >= 0 && Docs.LookupArgDocs(docId, Links, out _, out InitData.EventInit eventInit) && eventInit != null)
                    {
                        s = $"{hoveredWord}({eventInit.ID})\n{ArgString(docId)}";
                        if (eventInit.Name != null)
                        {
                            s += "\n" + eventInit.Name;
                        }
                    }
                }
                if (s == null)
                {
                    (string title, string text) = ToolTips[hoveredWord];
                    s = title + "\n" + text;
                }
                ShowTip(s, p);
                HoverTipRange = tipRange;
                return;
            }
            // Can probably add variables at some point, but for now only respect int constants.
            // TODO: Some things are uints, like entity ids and event ids
            if (int.TryParse(hoveredWord, out int value) && SharedControls.Metadata.IsOpen())
            {
                List<string> argList = new List<string>();
                ParseFuncAtRange(tipRange, Docs, out InitData.DocID docId, out int argIndex, argList);
                EMEDF.ArgDoc argDoc = Docs.GetHeuristicArgDoc(docId, argIndex, Links);
                string dataType = argDoc?.MetaType?.DataType;
                if (metadataTypes.Contains(dataType))
                {
                    // At this point, we're definitely going to try to display things, so count it as the current hover range
                    if (HoverTipRange != null && HoverTipRange.Start.Equals(tipRange.Start))
                    {
                        return;
                    }
                    HoverTipRange = tipRange;
                    void showData(SoapstoneMetadata.DisplayData data)
                    {
                        // Cancel if moved away in the intervening time
                        if (HoverTipRange == null || !HoverTipRange.Start.Equals(tipRange.Start)) return;
                        if (data != null)
                        {
                            string text = data.Desc;
                            if ((data is SoapstoneMetadata.EntityData ent && ent.Type != "Self") || data is SoapstoneMetadata.EntryData)
                            {
                                text += $"\nRight-click tooltip to open in {SharedControls.Metadata.ServerName}";
                            }
                            ShowTip(text, p, data: data);
                        }
                    }
                    if (dataType == "entity")
                    {
                        // Exact entity subtype should not matter, since it's all one namespace.
                        // TODO: Don't clear tooltip when hovering over it, in this particular case, if there's a button (done?)
                        SharedControls.Metadata.GetEntityData(AutoContext.Game, value)
                            .ContinueWith(result => showData(result.Result), TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else if (dataType == "param" && argDoc.MetaType.Type != null)
                    {
                        SharedControls.Metadata.GetParamRow(AutoContext.Game, argDoc.MetaType.Type, value)
                            .ContinueWith(result => showData(result.Result), TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else if (dataType == "param" && argDoc.MetaType.MultiNames != null
                        && argDoc.MetaType.MultiNames.Count == 2 && argDoc.MetaType.OverrideTypes != null)
                    {
                        List<int> multiArgs = Docs.GetArgsAsInts(argList, docId.Func, argDoc.MetaType.MultiNames);
                        // Param multi-type is an enum and int. Use both of these together.
                        // Currently this will only pop up for the int values, due to the int filter above.
                        if (multiArgs != null && multiArgs.Count == 2)
                        {
                            // Only support the first matched override here (would require GetParamRow to support a list otherwise)
                            string overrideType = argDoc.MetaType.OverrideTypes
                                .Where(e => e.Value.Value == multiArgs[0])
                                .Select(e => e.Key)
                                .FirstOrDefault();
                            if (overrideType != null)
                            {
                                SharedControls.Metadata.GetParamRow(AutoContext.Game, overrideType, value)
                                    .ContinueWith(result => showData(result.Result), TaskScheduler.FromCurrentSynchronizationContext());
                            }
                        }
                    }
                    else if (dataType == "fmg" && argDoc.MetaType.Type != null)
                    {
                        SharedControls.Metadata.GetFmgEntry(AutoContext.Game, argDoc.MetaType.Type, value)
                            .ContinueWith(result => showData(result.Result), TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else if (dataType == "mapint")
                    {
                        showData(SharedControls.Metadata.GetMapNameData(AutoContext.Game, value));
                    }
                    else if (dataType == "mapparts" && argDoc.MetaType.MultiNames != null && argDoc.MetaType.MultiNames.Count == 4)
                    {
                        List<int> multiArgs = Docs.GetArgsAsInts(argList, docId.Func, argDoc.MetaType.MultiNames);
                        if (multiArgs != null && multiArgs.Count > 0)
                        {
                            // Existing mapparts should be have either 2 or 4 args
                            while (multiArgs.Count < 4)
                            {
                                multiArgs.Add(0);
                            }
                            showData(SharedControls.Metadata.GetMapNameData(AutoContext.Game, multiArgs));
                        }
                    }
                    else if (dataType == "eventflag")
                    {
                        showData(SharedControls.Metadata.GetEventFlagData(AutoContext.Game, value));
                    }
                    return;
                }
            }
            // No possible applicable hover tooltip: hide it, unless there's a text cursor one to keep.
            if (SharedControls.InfoTip.Visible && CursorTipRange != null && CursorTipRange.Contains(e.Place))
            {
                // This case is specifically for the argument tooltip: if it is present, there is no hovered keyword,
                // but we want to keep showing the arguments as long as the mouse is over them.
                return;
            }
            // This may also get called by OnOutOfBoundsToolTip, when moving mouse from text -> out-of-bounds -> tooltip,
            // so don't hide the tooltip if we've landed on it in the intervening time. This could also be changed to not depend
            // on the delay amount and have an area of leniency above the tooltip rectangle.
            if (SharedControls.InfoTip.Visible
                && SharedControls.InfoTip.ClientRectangle.Contains(SharedControls.InfoTip.PointToClient(Cursor.Position)))
            {
                return;
            }
            SharedControls.HideTip();
            HoverTipRange = null;
        }

        private void Editor_SelectionChanged(object sender, EventArgs e)
        {
            Range arguments = ParseFuncAtRange(editor.Selection, Docs, out InitData.DocID docId, out int argIndex);

            // Preemptively remove tooltip if changing the text-cursor-based tooltip range.
            // It may potentially get immediately added back here.
            // This will prevent he text-cursor-based tooltip from getting removed if there is no applicable hover tooltip.
            // If there is an applicable hover tooltip *and* the mouse moves, the hover tooltip will appear.
            if (CursorTipRange == null || !arguments.Start.Equals(CursorTipRange.Start))
            {
                // Console.WriteLine($"Setting cursor range: [{arguments.Text}]");
                CursorTipRange = arguments;
                // For hover tooltips: remove it when the selection range changes, and clear its cached range.
                // If the user clicks on text or types, we don't want to show a hover tip until their mouse moves.
                HoverTipRange = null;
                PreventHoverMousePosition = MousePosition;
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

            // Process funcName/argIndex stuff
            if (argIndex >= 0)
            {
                ShowArgToolTip(docId, arguments, argIndex);
            }
            SharedControls.LoadDocText(docId, argIndex, Docs, Links, false);
            AutoContext.FuncName = docId.Func;
            AutoContext.FuncArg = argIndex;
            // Hopefully this isn't too expensive? It only strictly needs to be done on PrecomputeAutocomplete
            // and also during autocomplete itself.
            AutoContext.ArgDoc = Docs.GetHeuristicArgDoc(docId, AutoContext.FuncArg, Links);
            if (PrecomputeAutocomplete)
            {
                FetchAutocomplete();
                PrecomputeAutocomplete = false;
            }
        }

        private void FetchAutocomplete()
        {
            // This method is used to fetch data before autocomplete menu pops up.
            // Also, used in the case of function autocomplete. This latter case could also be handled
            // if DocAutocompleteItems fetches some data and notifies the editor to re-show the menu.
            void maybeReshow(bool result)
            {
                if (result)
                {
                    // Force reshow with new results
                    // Can be tested with auto-complete dialog instruction
                    ShowInstructionMenu();
                }
            }
            EMEDF.DarkScriptType metaType = AutoContext.ArgDoc?.MetaType;
            if (metadataTypes.Contains(metaType?.DataType) && SharedControls.Metadata.IsOpen())
            {
                if (metaType.DataType == "param" && metaType.Type != null)
                {
                    SharedControls.Metadata.FetchParamRowNames(AutoContext.Game, metaType.Type)
                        .ContinueWith(result => maybeReshow(result.Result), TaskScheduler.FromCurrentSynchronizationContext());
                }
                if (metaType.DataType == "param" && metaType.OverrideTypes != null)
                {
                    List<Task<bool>> multiFetch = new List<Task<bool>>();
                    foreach (string type in metaType.OverrideTypes.Keys)
                    {
                        multiFetch.Add(SharedControls.Metadata.FetchParamRowNames(AutoContext.Game, type));
                    }
                    Task.WhenAll(multiFetch)
                        .ContinueWith(result => maybeReshow(result.Result.Any()), TaskScheduler.FromCurrentSynchronizationContext());
                }
                else if (metaType.DataType == "fmg" && metaType.Type != null)
                {
                    SharedControls.Metadata.FetchFmgEntryNames(AutoContext.Game, metaType.Type)
                        .ContinueWith(result => maybeReshow(result.Result), TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        public static Range ParseFuncAtRange(
            Range start,
            InstructionDocs docs,
            out InitData.DocID docId,
            out int argIndex,
            List<string> extractArgs = null)
        {
            // Try to find the innermost function at this place. This does not handle nested parens well at all.
            // This needs to be cheap. It's called any time the text cursor changes, for instance.

            // Find text around cursor in the current line, up until hitting parentheses
            Range arguments = start.GetFragment(@"[^)(\n]");

            // CharAfterEnd does not exist so do this based on CharAfterStart
            static char charAfterEnd(Range arg) => arg.End.iChar >= arg.tb[arg.End.iLine].Count ? '\n' : arg.tb[arg.End.iLine][arg.End.iChar].c;
            // Can be used if charAfterEnd is ',' or '('
            static Range getNextArg(Range arg)
            {
                Place argStart = arg.End;
                argStart.iChar += 1;
                return arg.tb.GetRange(argStart, argStart).GetFragment(@"[^)(\n,]");
            }

            // For being within function args rather than matching the exact string, check if inside parens, but not nested ones.
            // Matching IfThing(0^,1) but not WaitFor(Thi^ng(0,1))
            Range nameRange;
            long eventId = -1;
            argIndex = -1;
            if (arguments.CharBeforeStart == '(' && start.tb.GetRange(arguments.End, arguments.End).CharAfterStart != '(')
            {
                // Scan leftward through arguments until no commas remain.
                // This does not work with nested calls, like IfThing(getMyCustomConstant(), ^1)
                Range arg = start.GetFragment(@"[^)(\n,]");
                argIndex = 0;
                if (extractArgs != null)
                {
                    extractArgs.Add(arg.Text);
                }
                while (arg.CharBeforeStart == ',')
                {
                    argIndex++;
                    Place argStart = arg.Start;
                    argStart.iChar -= 2;
                    arg = start.tb.GetRange(argStart, argStart).GetFragment(@"[^)(\n,]");
                    if (extractArgs != null)
                    {
                        extractArgs.Add(arg.Text);
                    }
                }
                nameRange = FuncNameRange(arguments);
                // For extractArgs mode in particular, go in the reverse direction
                // In case it all gets rewritten to be not terrible, this should be improved too.
                if (extractArgs != null)
                {
                    extractArgs.Reverse();
                    arg = start.GetFragment(@"[^)(\n,]");
                    while (true)
                    {
                        if (charAfterEnd(arg) != ',')
                        {
                            break;
                        }
                        arg = getNextArg(arg);
                        extractArgs.Add(arg.Text);
                    }
                }
            }
            else
            {
                // Get the word immediately under the cursor. No tooltip in this case.
                nameRange = start.GetFragment(@"[\w\$]");

                // Special handling for Event and $Event, usually excluded from above because of the following (
                // This could be generalized to the entire line
                if (nameRange.CharBeforeStart == '(')
                {
                    Range funcRange = FuncNameRange(nameRange);
                    string funcText = funcRange.Text;
                    if (funcText == "Event" || funcText == "$Event")
                    {
                        nameRange = funcRange;
                    }
                }
            }
            string funcName = nameRange.Text;
            if ((funcName == "Event" || funcName == "$Event") && charAfterEnd(nameRange) == '(')
            {
                Range idRange = getNextArg(nameRange);
                if (long.TryParse(idRange.Text, out long id))
                {
                    eventId = id;
                }
            }
            else if (docs.TypedInitIndex.TryGetValue(funcName, out int idIndex))
            {
                string eventArg = null;
                if (extractArgs != null && idIndex < extractArgs.Count)
                {
                    eventArg = extractArgs[idIndex];
                }
                else
                {
                    // Separate lookup for the id, rather than adding further tracking to the above logic
                    Range fullCall = nameRange.GetFragment(@"[\w\$\s]");
                    if (charAfterEnd(fullCall) == '(')
                    {
                        Range arg = getNextArg(fullCall);
                        // This could be a loop, but assume that idIndex can only be 0 or 1
                        if (idIndex == 0)
                        {
                            eventArg = arg.Text;
                        }
                        else if (idIndex == 1 && charAfterEnd(arg) == ',')
                        {
                            arg = getNextArg(arg);
                            eventArg = arg.Text;
                        }
                    }
                }
                // Parse ignores whitespace
                if (eventArg != null && long.TryParse(eventArg, out long id))
                {
                    eventId = id;
                }
            }
            docId = new InitData.DocID(funcName, eventId);
            return arguments;
        }

        private static Range FuncNameRange(Range arguments)
        {
            int start = arguments.Start.iChar - 2;
            int line = arguments.Start.iLine;
            Range pre = new Range(arguments.tb, start, line, start, line);
            return pre.GetFragment(@"[\w\$]");
        }

        private void ShowTip(string s, Point p, int argIndex = -1, object data = null)
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
                InfoTip.SetText(s, AutoContext.Game, data);
            }
            // Translate a bit rightward to make directly vertical mouse movement change hover.
            p.Offset(3, 0);
            InfoTip.ShowAtPosition(editor, p, editor.CharHeight);
            editor.Focus();
        }

        private void ShowArgToolTip(InitData.DocID docId, Range arguments, int argument = -1)
        {
            if (!Properties.Settings.Default.ArgTooltip)
            {
                return;
            }
            if (Docs.AllAliases.TryGetValue(docId.Func, out string realName))
            {
                docId = new InitData.DocID(realName, docId.Event);
            }
            // TODO: Use LookupArgDocs instead for init data?
            if (Docs.CallArgs.ContainsKey(docId.Func))
            {
                Point point = editor.PlaceToPoint(arguments.Start);
                ShowTip(ArgString(docId), point, argument);
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
            DialogResult res = ScrollDialog.Show(ParentForm, $"Integer {text} is equivalent to float {fval}\n\nReplace it for regular event initialization? (Don't do this unless you're sure it's a float, and don't use it for typed initialization like $InitializeEvent.)", "Confirm replacement");
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
            if (!long.TryParse(numRange.Text, out long id))
            {
                return;
            }
            // This isn't a comprehensive regex and doesn't support code on very different lines.
            // Singleline is needed for ^ to work. But GetRangesByLines may be unnecessarily expensive vs GetRanges.
            Regex regex = new Regex($@"^\s*\$?Event\(\s*{id}\s*,", RegexOptions.Singleline);
            List<Range> ranges = editor.Range.GetRangesByLines(regex).ToList();
            string searchType = "";
            if (ranges.Any(r => r.Start.iLine == place.iLine))
            {
                // Used clicked on an event definition, so go to initialization in the same file, which has a slot id
                // Note that Elden Ring incorrectly uses common init in the same file sometimes
                regex = new Regex($@"^\s*\$?Initialize\w+\(\s*\d+\s*,\s*{id}\b", RegexOptions.Singleline);
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
                    // Not sure where this logic should go. It is all heuristic, doesn't use actual linked files.
                    string checkFile = "common_func";
                    if (Docs.ResourceGame == "bb")
                    {
                        checkFile = Scripter.EmevdFileName.StartsWith("m29_") ? "m29" : "common";
                    }
                    if (SharedControls.JumpToCommonFunc(id, checkFile, this))
                    {
                        return;
                    }
                    if (Docs != null && Docs.LooksLikeCommonFunc(id))
                    {
                        commonSuggest = $". Open {checkFile} in another tab to enable searching there";
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

        public Action GetJumpToEventAction(long id)
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

        private string ArgString(InitData.DocID docId)
        {
            if (!Docs.LookupArgDocs(docId, Links, out List<EMEDF.ArgDoc> args))
            {
                return "";
            }
            List<string> argStrings = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                EMEDF.ArgDoc arg = args[i];
                // TODO: Display whether the arg is optional here.
                // It may depend on func name. This logic is currently implemented in SharedControls.
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

        public string GetDocName() => Docs.ResourceGame;

        public void Cut() => editor.Cut();

        public void Copy() => editor.Copy();

        public void Paste() => editor.Paste();

        public void ShowReplaceDialog() => editor.ShowReplaceDialog();

        public void SelectAll() => editor.SelectAll();

        // TODO: There are a few improvements we can make here.
        // Highlighting search terms, non-dialog search (Google Chrome style Ctrl+F).
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
            if (ShowCompileError(Scripter.JsFileName, LatestWarnings, "", true) is Range errorSelect)
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
                new FancyEventScripter(Scripter, Docs, Settings.CFGOptions).Repack(text, Links, out FancyJSCompiler.CompileOutput output);
                PreviewCompilationForm preview = RefreshPreviewCompilationForm();
                preview.SetSegments(output.GetDiffSegments(), text, output.Code);
                preview.Show();
                preview.Focus();
            }
            catch (FancyJSCompiler.FancyCompilerException ex)
            {
                if (ShowCompileError(Scripter.JsFileName, new List<Exception>() { ex }, "") is Range errorSelect)
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
                if (ShowCompileError(Scripter.JsFileName, new List<Exception>() { ex }, "") is Range errorSelect)
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
                // Is this necessary? (it's also in GUI)
                if (SharedControls.InfoTip.Visible)
                {
                    SharedControls.HideTip();
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.F && e.Control)
            {
                // Can this be done with actions?
                ShowFindDialog();
                e.Handled = true;
            }
            // This happens on a similar trigger to autocomplete itself (KeyPressed) before SelectionChanged
            PrecomputeAutocomplete = true;
        }

        private void Editor_Scroll(object sender, ScrollEventArgs e) => SharedControls.HideTip();

        private Range ShowCompileError(string file, List<Exception> ex, string extra, bool warnings = false)
        {
            ErrorMessageForm error = new ErrorMessageForm(editor.Font);
            error.SetMessage(file, ex, extra, warnings);
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
