using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;
using System.Threading.Tasks;
using FastColoredTextBoxNS;
using System.IO;

namespace DarkScript3
{
    public class SharedControls
    {
        public BetterFindForm BFF;
        public ComplexFindForm CFF;
        public PreviewCompilationForm Preview;
        public ToolControl InfoTip;

        // Misc GUI-owned controls.
        private readonly ToolStripStatusLabel statusLabel;
        private readonly FastColoredTextBox docBox;

        // References to everything. These should be kept internal to this class, to avoid explicit reverse dependencies.
        private readonly GUI gui;
        private List<EditorGUI> Editors = new List<EditorGUI>();
        private List<FastColoredTextBox> AllTextBoxes => new[] { InfoTip.tipBox, docBox }.Concat(Editors.Select(editor => editor.editor)).ToList();

        private bool stickyStatusMessage = false;

        public SharedControls(GUI gui, ToolStripStatusLabel statusLabel, FastColoredTextBox docBox)
        {
            this.gui = gui;
            this.statusLabel = statusLabel;
            this.docBox = docBox;
            BFF = new BetterFindForm(null);
            CFF = new ComplexFindForm(this);
            InfoTip = new ToolControl();

            BFF.infoTip = InfoTip;
            InfoTip.Show();
            InfoTip.Hide();
            InfoTip.tipBox.TextChanged += (object sender, TextChangedEventArgs e) => TipBox_TextChanged(sender, e);
            // InfoTip.GotFocus += (object sender, EventArgs args) => editor.Focus(); // needed?

            foreach (FastColoredTextBox tb in AllTextBoxes)
            {
                tb.ZoomChanged += (sender, e) => { HideTip(); SetGlobalFont(((FastColoredTextBox)sender).Font); };
            }
        }

        public void LockEditor()
        {
            HideTip();
            foreach (FastColoredTextBox tb in AllTextBoxes)
            {
                tb.Enabled = false;
            }
            gui.LockEditor();
        }

        public void UnlockEditor()
        {
            foreach (FastColoredTextBox tb in AllTextBoxes)
            {
                tb.Enabled = true;
            }
            gui.UnlockEditor();
        }

        public void AddEditor(EditorGUI editor)
        {
            SwitchEditor(editor);
            editor.RefreshGlobalStyles();
            editor.editor.ZoomChanged += (sender, e) => { HideTip(); SetGlobalFont(((FastColoredTextBox)sender).Font); };
        }

        public void RemoveEditor(EditorGUI editor)
        {
            HideTip();
            Editors.Remove(editor);
            docBox.Clear();
            currentDoc = (null, -1);
        }

        public void SwitchEditor(EditorGUI editor)
        {
            HideTip();
            BFF.tb = editor.editor;
            docBox.Clear();
            currentDoc = (null, -1);
            editor.RefreshGlobalStyles(docBox);
            // Add last as a hint for which global styles to use
            Editors.Remove(editor);
            Editors.Add(editor);
        }

        public void HideTip()
        {
            InfoTip.Hide();
        }

        public void JumpToLineInFile(string path, string lineText, int lineChar, int lineHint)
        {
            // lineHint is 0-indexed here
            FileInfo file = new FileInfo(path);
            path = file.FullName;
            if (!file.Exists || !path.EndsWith(".js")) return;
            if (gui.OpenJSFile(file.FullName))
            {
                string org = path.Substring(0, path.Length - 3);
                EditorGUI destEditor = Editors.Find(e => e.EMEVDPath == org);
                if (destEditor != null)
                {
                    // Reverse dependency. Keep this limited in scope
                    gui.ShowFile(destEditor.EMEVDPath);
                    // gui.BringToFront();
                    if (InfoTip.Visible) InfoTip.Hide();
                    destEditor.editor.Focus();
                    destEditor.JumpToTextNearLine(lineText, lineChar, lineHint);
                }
            }
        }

        public bool JumpToCommonFunc(int eventId, EditorGUI sourceEditor)
        {
            // If a common_func is open in the same directory, jump to it.
            // This is just based on the contents of the editor - initialization types would require something more robust.
            string path = sourceEditor.EMEVDPath;
            string[] parts = Path.GetFileName(path).Split(new[] { '.' }, 2);
            if (parts.Length <= 1 || parts[0] == "common_func") return false;
            string commonFuncPath = Path.Combine(Path.GetDirectoryName(path), "common_func." + parts[1]);
            EditorGUI destEditor = Editors.Find(e => e.EMEVDPath == commonFuncPath);
            if (destEditor == null) return false;
            Action gotoEvent = destEditor.GetJumpToEventAction(eventId);
            if (gotoEvent != null)
            {
                // Reverse dependency. Keep this limited in scope
                gui.ShowFile(destEditor.EMEVDPath);
                gotoEvent();
                return true;
            }
            return false;
        }

        public string GetCurrentDirectory()
        {
            if (Editors.Count == 0)
            {
                return null;
            }
            return Path.GetDirectoryName(Editors.Last().EMEVDPath);
        }

        public void SetStatus(string status, bool sticky = true)
        {
            stickyStatusMessage = sticky;
            statusLabel.Text = status;
        }

        public void ResetStatus(bool sticky)
        {
            if (stickyStatusMessage && !sticky)
            {
                return;
            }
            statusLabel.Text = "";
        }

        public void SetGlobalFont(Font font)
        {
            foreach (FastColoredTextBox tb in AllTextBoxes)
            {
                // Font sharing leads to post-disposal access, avoid it at all costs
                Font f = (Font)font.Clone();
                if (tb == InfoTip.tipBox)
                {
                    // Heuristic for less intrusive tips: above 8pt, show at 80% of size
                    if (f.SizeInPoints > 8)
                    {
                        f = new Font(f.Name, Math.Max(8f, f.SizeInPoints * 0.8f));
                    }
                }
                tb.Font = f;
            }
            TextStyles.Font = font;
        }

        public void RefreshGlobalStyles()
        {
            foreach (EditorGUI editor in Editors)
            {
                editor.RefreshGlobalStyles();
            }
            if (Editors.Count == 0)
            {
                docBox.BackColor = TextStyles.BackColor;
                docBox.ClearStylesBuffer();
                InfoTip.tipBox.ClearStylesBuffer();
            }
            else
            {
                Editors.Last().RefreshGlobalStyles(docBox);
                Editors.Last().RefreshGlobalStyles(InfoTip.tipBox);
            }
        }

        public void ShowFindDialog(string findText)
        {
            if (findText != null)
            {
                BFF.tbFind.Text = findText;
            }
            BFF.tbFind.SelectAll();
            BFF.Show();
            BFF.Focus();
            BFF.BringToFront();
        }

        public void ShowComplexFindDialog(string findText)
        {
            // Yep just copy-paste
            if (findText != null)
            {
                CFF.tbFind.Text = findText;
            }
            CFF.tbFind.SelectAll();
            CFF.Show();
            CFF.Focus();
            CFF.BringToFront();
        }

        public PreviewCompilationForm RefreshPreviewCompilationForm(Font font)
        {
            if (Preview != null && !Preview.IsDisposed)
            {
                Preview.Close();
            }
            Preview = new PreviewCompilationForm(font);
            return Preview;
        }

        public void CheckOodle(string emedfPath)
        {
            // Heuristic detection of oodle copying, for convenience
            string emedfName = Path.GetFileName(emedfPath);
            if (!(emedfName.StartsWith("er-common") || emedfName.StartsWith("sekiro-common")))
            {
                return;
            }
            // Assume current working directory is exe dir
            string dll = "oo2core_6_win64.dll";
            if (File.Exists(dll) || File.Exists($"lib/{dll}"))
            {
                return;
            }
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = $"Select {dll} from the game directory";
            ofd.Filter = $"Oodle DLL|{dll}";
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            string selected = ofd.FileName;
            if (Path.GetFileName(selected) != dll || File.Exists(dll) || File.Exists($"lib/{dll}"))
            {
                return;
            }
            File.Copy(selected, dll);
        }

        private void TipBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.SetStyle(TextStyles.ToolTipKeyword, JSRegex.DataType);
            e.ChangedRange.SetStyle(TextStyles.EnumType, new Regex(@"[<>]"));
        }

        private Action<(string, int, InstructionDocs)> loadDocTextDebounce;
        // Should probably make Debounce more idempotency-aware, but this is a cheap way to avoid cancelling unnecessarily
        private (string, int) callingDoc;
        public void LoadDocText(string func, int argIndex, InstructionDocs docs, bool immediate)
        {
            if (!Properties.Settings.Default.ArgDocbox)
            {
                argIndex = -1;
            }
            if (immediate)
            {
                LoadDocTextInternal((func, argIndex, docs));
            }
            else
            {
                if (loadDocTextDebounce == null)
                {
                    loadDocTextDebounce = Debounce((Action<(string, int, InstructionDocs)>)LoadDocTextInternal, 50);
                }
                (string, int) call = (func, argIndex);
                if (call != callingDoc)
                {
                    loadDocTextDebounce((func, argIndex, docs));
                    callingDoc = call;
                }
            }
        }

        private (string, int) currentDoc;
        private void LoadDocTextInternal((string, int, InstructionDocs) input)
        {
            (string func, int argIndex, InstructionDocs Docs) = input;
            if (func == null)
            {
                docBox.Clear();
                return;
            }
            if (Docs == null) return;
            if (Docs.AllAliases.TryGetValue(func, out string realName))
            {
                func = realName;
            }
            List<EMEDF.ArgDoc> args = null;
            ScriptAst.BuiltIn builtin = null;
            if (!Docs.AllArgs.TryGetValue(func, out args) && !ScriptAst.ReservedWords.TryGetValue(func, out builtin)) return;
            if (builtin != null && builtin.Doc == null) return;

            (string, int) newDoc = (func, argIndex);
            if (currentDoc == newDoc) return;
            currentDoc = newDoc;
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
            Place? argPlace = null;

            // Optional args are just the last n args, rather than per-arg, but so far this seems fine.
            int optCount = 0;
            string shortText = null;
            if (Docs.Translator != null)
            {
                if (Docs.Translator.CondDocs.TryGetValue(func, out InstructionTranslator.FunctionDoc funcDoc))
                {
                    optCount = funcDoc.OptionalArgs;
                }
                else if (Docs.Translator.ShortDocs.TryGetValue(func, out InstructionTranslator.ShortVariant shortVariant))
                {
                    optCount = shortVariant.OptionalArgs;
                    string mainName = Docs.Translator.InstrDocs[shortVariant.Cmd].DisplayName;
                    shortText = $"{(shortVariant.Hidden ? "deprecated" : "simpler")} version of {mainName}";
                }
                else if (Docs.Functions.TryGetValue(func, out (int, int) pos))
                {
                    EMEDF.InstrDoc instrDoc = Docs.DOC[pos.Item1][pos.Item2];
                    optCount = instrDoc.OptionalArgs;
                    string cmdId = InstructionDocs.FormatInstructionID(pos.Item1, pos.Item2);
                    if (Docs.Translator.ShortSelectors.TryGetValue(cmdId, out InstructionTranslator.ShortSelector selector))
                    {
                        List<InstructionTranslator.ShortVariant> shorts = selector.Variants.Where(v => !v.Hidden).ToList();
                        if (shorts.Count > 0)
                        {
                            shortText = $"simpler version{(shorts.Count == 1 ? "" : "s")}: "
                                + string.Join(", ", shorts.Select(v => v.Name));
                        }
                    }
                }
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
                bool selectedArg = false;
                if (argIndex >= 0)
                {
                    selectedArg = argIndex == i || (i == args.Count - 1 && argIndex > i);
                }
                string prefix = selectedArg ? "\u2b9a " : "  ";

                bool displayEnum = false;
                if (argDoc.EnumName == null)
                {
                    docBox.AppendText($"{prefix}{InstructionDocs.TypeString(argDoc.Type)} ", TextStyles.Keyword);
                }
                else if (argDoc.EnumName == "BOOL")
                {
                    docBox.AppendText($"{prefix}bool ", TextStyles.Keyword);
                }
                else if (argDoc.EnumDoc != null)
                {
                    docBox.AppendText($"{prefix}enum ", TextStyles.Keyword);
                    displayEnum = true;
                }

                docBox.AppendText(argDoc.DisplayName);
                if (optional) docBox.AppendText($" (default {argDoc.GetDisplayValue(argDoc.Default)})", TextStyles.Comment);
                if (argDoc.Vararg) docBox.AppendText(" (vararg)", TextStyles.Comment);

                if (selectedArg)
                {
                    argPlace = new Place(0, docBox.Range.End.iLine);
                }

                if (displayEnum)
                {
                    EMEDF.EnumDoc enm = argDoc.EnumDoc;
                    foreach (var kv in enm.DisplayValues)
                    {
                        docBox.AppendText(Environment.NewLine);
                        docBox.AppendText($" {kv.Key.PadLeft(6)}", TextStyles.String);
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
                docBox.AppendText(Environment.NewLine + Environment.NewLine + $"(condition{(altNames.Count == 1 ? "" : "s")}: {string.Join(", ", altNames)})");
            }
            if (shortText != null)
            {
                docBox.AppendText(Environment.NewLine + Environment.NewLine + $"({shortText})");
            }
            Place jumpPlace = argPlace ?? docBox.Range.Start;
            docBox.Selection = docBox.GetRange(jumpPlace, jumpPlace);
            docBox.DoSelectionVisible();
        }

        // https://stackoverflow.com/questions/28472205/c-sharp-event-debounce
        // Can only be called from the UI thread, and runs the given action in the UI thread.
        public static Action<T> Debounce<T>(Action<T> func, int ms)
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
    }
}
