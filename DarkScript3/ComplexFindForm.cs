using System;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using FastColoredTextBoxNS;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Drawing;
using Range = FastColoredTextBoxNS.Range;

namespace DarkScript3
{
    public partial class ComplexFindForm : Form
    {
        private static readonly TextStyle linkStyle = new TextStyle(Brushes.Blue, null, FontStyle.Underline);
        private static readonly Regex placePartsRe = new Regex(@"^([^:]+):(\d+):(.*)$");

        private SharedControls SharedControls;
        private string Dir { get; set; }
        private bool working = false;
        public FastColoredTextBox tb;
        public ToolControl infoTip = new ToolControl();

        public ComplexFindForm(SharedControls controls)
        {
            InitializeComponent();
            SharedControls = controls;
        }

        private void btClose_Click(object sender, EventArgs e)
        {
            Close();
        }
        
        private async void btSearch_Click(object sender, EventArgs e)
        {
            if (working) return;
            string pattern = tbFind.Text;
            if (string.IsNullOrWhiteSpace(pattern)) return;
            // Mostly copy-pasted from BetterFindForm
            RegexOptions opt = cbMatchCase.Checked ? RegexOptions.None : RegexOptions.IgnoreCase;
            if (!cbRegex.Checked)
                pattern = Regex.Escape(pattern);
            if (cbWholeWord.Checked)
                pattern = "\\b" + pattern + "\\b";
            Regex regex = new Regex(pattern, opt);

            string dir = SharedControls.GetCurrentDirectory();
            if (dir == null || !Directory.Exists(dir)) return;

            // Use same selection logic as the side window
            List<string> jsPaths = new List<string>();
            foreach (string path in Directory.GetFiles(dir))
            {
                if (path.EndsWith(".emevd.js") || path.EndsWith(".emevd.dcx.js"))
                {
                    jsPaths.Add(path);
                }
            }

            working = true;
            Cursor prev = Cursor;
            Cursor = Cursors.WaitCursor;
            box.Clear();
            Dir = dir;
            // https://stackoverflow.com/questions/14075029/have-a-set-of-tasks-with-only-x-running-at-a-time
            SemaphoreSlim semaphore = new SemaphoreSlim(20);
            List<Task> tasks = new List<Task>();
            foreach (string jsPath in jsPaths)
            {
                await semaphore.WaitAsync();
                // For better efficiency, can use producer/consumer pattern for IO and CPU,
                // but this is sufficient for the scale of this problem.
                Task task = Task.Factory.StartNew(() => { })
                    .ContinueWith(_ => Search(regex, jsPath))
                    .ContinueWith(t => Report(t.Result), TaskScheduler.FromCurrentSynchronizationContext())
                    .ContinueWith(_ => semaphore.Release());
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
            Cursor = prev;
            working = false;
        }

        private List<Result> Search(Regex regex, string jsPath)
        {
            string name = Path.GetFileName(jsPath);
            name = name.Substring(0, name.Length - 3);
            List<Result> ret = new List<Result>();
            if (!File.Exists(jsPath)) return ret;
            int line = 1;
            try
            {
                using (StreamReader reader = new StreamReader(jsPath))
                {
                    // Just read everything and copy the parsing behavior in GUI exactly
                    // Trying to replicate it line-by-line is way too hacky.
                    string allText = reader.ReadToEnd();
                    allText = Regex.Replace(allText, @"(^|\n)\s*// ==EMEVD==(.|\n)*// ==/EMEVD==", "");
                    allText = allText.Trim();
                    // while ((text = reader.ReadLine()) != null)
                    foreach (string text in Regex.Split(allText, @"\r?\n"))
                    {
                        // Alternatively, we could do a regex search in the entire file and use a Reader wrapper, but it's
                        // a lot trickier to keep track of that state. This is just a bit more computation-heavy.
                        MatchCollection matches = regex.Matches(text);
                        if (matches.Count > 0)
                        {
                            string prefix = $"{name}:{line}:";
                            Result result = new Result
                            {
                                Text = $"{prefix}{text}",
                                Matches = new List<(int, int)>(matches.Count),
                            };
                            foreach (Match match in matches)
                            {
                                result.Matches.Add((match.Index + prefix.Length, match.Length));
                            }
                            ret.Add(result);
                        }
                        line++;
                    }
                }
            }
            catch (Exception) { }
            // if (ret.Count > 0) Console.WriteLine($"result {jsPath}, ret {ret.Count}, searched {line} for {regex}");
            return ret;
        }

        private class Result
        {
            public string Text { get; set; }
            // Matches to highlight, starting from the start of Text.
            // These consist of (start index, length)
            public List<(int, int)> Matches { get; set; }
        }

        private void Report(List<Result> results)
        {
            if (box.LinesCount >= 2000)
            {
                // This fallback is nondeterministic, but just stop at some extreme point
                return;
            }
            (string, int) getKey(string text)
            {
                string[] parts = text.Split(new[] { ':' }, 3);
                if (parts.Length == 3 && int.TryParse(parts[1], out int line))
                {
                    return (parts[0], line);
                }
                if (text == "")
                {
                    return ("~", 0);
                }
                return (text, 0);
            }
            foreach (Result result in results)
            {
                if (box.LinesCount <= 1 && string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Selection = box.GetRange(box.Range.Start, box.Range.Start);
                }
                else
                {
                    int index = BinarySearch(box.Lines, result.Text, (a, b) => getKey(a).CompareTo(getKey(b)));
                    if (index < 0)
                    {
                        // In theory, there should be no duplicate lines, but who knows.
                        index = ~index;
                    }
                    if (index == box.LinesCount)
                    {
                        box.Selection = box.GetRange(box.Range.End, box.Range.End);
                    }
                    else
                    {
                        Range range = box.GetLine(index);
                        // Console.WriteLine($"check {index}, {range.Start} for {result}");
                        box.Selection = box.GetRange(range.Start, range.Start);
                    }
                }
                int insertIndex = 0;
                for (int i = 0; i <= result.Matches.Count; i++)
                {
                    // Insert the text before the style text, then style text (if non-last iteration).
                    // This all very much must be run in the UI thread.
                    bool last = i == result.Matches.Count;
                    // Exclusive end index
                    int endNormal = last ? result.Text.Length : result.Matches[i].Item1;
                    if (endNormal > insertIndex)
                    {
                        box.InsertText(result.Text.Substring(insertIndex, endNormal - insertIndex), false);
                        insertIndex = endNormal;
                    }
                    if (last)
                    {
                        box.InsertText(Environment.NewLine, false);
                    }
                    else if (result.Matches[i].Item2 > 0)
                    {
                        box.InsertText(result.Text.Substring(insertIndex, result.Matches[i].Item2), linkStyle, false);
                        insertIndex += result.Matches[i].Item2;
                    }
                }
            }
        }

        // https://stackoverflow.com/questions/967047/how-to-perform-a-binary-search-on-ilistt
        // Thanks C#
        private static int BinarySearch<TItem, TSearch>(
            IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
        {
            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = comparer(value, list[middle]);
                if (comparisonResult < 0)
                {
                    upper = middle - 1;
                }
                else if (comparisonResult > 0)
                {
                    lower = middle + 1;
                }
                else
                {
                    return middle;
                }
            }
            return ~lower;
        }

        private void box_MouseMove(object sender, MouseEventArgs e)
        {
            bool linkHover = box.GetStylesOfChar(box.PointToPlace(e.Location)).Contains(linkStyle);
            box.Cursor = linkHover ? Cursors.Hand : Cursors.Default;
        }

        private void box_MouseUp(object sender, MouseEventArgs e)
        {
            Place place = box.PointToPlace(e.Location);
            bool linkHover = box.GetStylesOfChar(place).Contains(linkStyle);
            if (linkHover)
            {
                // Try to rewrite
                string text = box.GetLineText(place.iLine);
                Match m = placePartsRe.Match(text);
                if (m.Success)
                {
                    string filePrefix = m.Groups[1].Value;
                    string file = Path.Combine(Dir, $"{filePrefix}.js");
                    // 1-indexed to 0-indexed expected by SharedControls
                    int lineHint = int.Parse(m.Groups[2].Value) - 1;
                    string lineText = m.Groups[3].Value;
                    // Special calculation to get start of match range, rather than jumping to the clicked-on part
                    Range matchRange = box.GetRange(place, place).GetFragment(linkStyle, false);
                    Place matchPlace = matchRange.Start;
                    int lineChar = Math.Max(0, Math.Min(matchPlace.iChar - (text.Length - lineText.Length), lineText.Length - 1));
                    SharedControls.JumpToLineInFile(file, lineText, lineChar, lineHint);
                }
            }
        }

        // Bunch of stuff from BetterFindForm
        private void tbFind_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\r')
            {
                btSearch.PerformClick();
                e.Handled = true;
                return;
            }
            if (e.KeyChar == '\x1b')
            {
                Hide();
                e.Handled = true;
                return;
            }
        }

        private void FindForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                // Is this same hack from BetterFindForm really necessary?
                // This form should be a singleton, but enforcing that is annoying.
                e.Cancel = true;
                Hide();
            }
            if (tb != null)
            {
                tb.Focus();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                this.Close();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnActivated(EventArgs e)
        {
            tbFind.Focus();
        }

        private void cbMatchCase_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void ComplexFindForm_Shown(object sender, EventArgs e)
        {
            // Is this the right event? Should it go in OnActivated?
            // Seems to be run once.
            box.Font = TextStyles.Font;
        }
    }
}
