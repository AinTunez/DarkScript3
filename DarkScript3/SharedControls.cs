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

namespace DarkScript3
{
    public class SharedControls
    {
        public BetterFindForm BFF;
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
            currentFuncDoc = null;
        }

        public void SwitchEditor(EditorGUI editor)
        {
            HideTip();
            BFF.tb = editor.editor;
            docBox.Clear();
            currentFuncDoc = null;
            editor.RefreshGlobalStyles(docBox);
            // Add last as a hint for which global styles to use
            Editors.Remove(editor);
            Editors.Add(editor);
        }

        public void HideTip()
        {
            InfoTip.Hide();
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
                tb.Font = (Font)font.Clone();
            }
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

        public PreviewCompilationForm RefreshPreviewCompilationForm(Font font)
        {
            if (Preview != null && !Preview.IsDisposed)
            {
                Preview.Close();
            }
            Preview = new PreviewCompilationForm(font);
            return Preview;
        }

        private void TipBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.SetStyle(TextStyles.ToolTipKeyword, JSRegex.DataType);
            e.ChangedRange.SetStyle(TextStyles.EnumType, new Regex(@"[<>]"));
        }

        private Action<(string, InstructionDocs)> loadDocTextDebounce;
        public void LoadDocText(string func, InstructionDocs docs, bool immediate)
        {
            if (immediate)
            {
                LoadDocTextInternal((func, docs));
            }
            else
            {
                if (loadDocTextDebounce == null)
                {
                    loadDocTextDebounce = Debounce((Action<(string, InstructionDocs)>)LoadDocTextInternal, 50);
                }
                loadDocTextDebounce((func, docs));
            }
        }

        private string currentFuncDoc;
        private void LoadDocTextInternal((string, InstructionDocs) input)
        {
            string func = input.Item1;
            InstructionDocs Docs = input.Item2;
            if (func == null)
            {
                docBox.Clear();
                return;
            }
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
                docBox.AppendText(Environment.NewLine + Environment.NewLine + $"(func{(altNames.Count == 1 ? "" : "s")}: {string.Join(", ", altNames)})");
            }
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
