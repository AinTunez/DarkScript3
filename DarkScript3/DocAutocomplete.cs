using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FastColoredTextBoxNS;
using Range = FastColoredTextBoxNS.Range;

namespace DarkScript3
{
    public static class DocAutocomplete
    {
        public class AutocompleteContext
        {
            // Incremented and assigned every time an item is selected, to keep a priority list of sorts
            public int SelectedIndex { get; set; }
            // Currently shown arg, determined through hacky parsing
            public string FuncName { get; set; }
            public int FuncArg { get; set; }
            // Updated only when needed, based on the above two
            public EMEDF.ArgDoc ArgDoc { get; set; }
            // Whether in a fancy event or not, determined through hacky parsing
            public FancyContextType Fancy { get; set; }
            // Heuristic for expression vs statement context. This is extremely poor at the moment and relies on newlines.
            public bool InExpression { get; set; }
            // Can also swap out some data depending on the event (e.g. event ids used), if any local context is used.
            // This can be parsed out when unpacking, as well as when saving (can try extremely lenient syntax-parsing on load,
            // or alternatively, saving it as auxiliary metadata when file is saved - super complicated to manage though)
        }

        public enum FancyContextType
        {
            Any, RegularOnly, FancyOnly
        }

        public enum AutocompleteCategory
        {
            // Main order for showing categories
            // ExpressionInstruction is lower priority for instruction in expression context
            Instruction, Condition, ExpressionInstruction, Enum
        }

        public enum AutocompleteResult
        {
            // Ordering within categories
            Unconditional, Prefix, Infix, LowQualityInfix, None
        }

        public static readonly Dictionary<AutocompleteCategory, int> ImageIndices = new Dictionary<AutocompleteCategory, int>
        {
            [AutocompleteCategory.Instruction] = 0,
            [AutocompleteCategory.ExpressionInstruction] = 0,
            [AutocompleteCategory.Condition] = 1,
            [AutocompleteCategory.Enum] = 2,
        };

        public class DocAutocompleteItemList : IEnumerable<AutocompleteItem>
        {
            private readonly List<DocAutocompleteItem> items;
            private readonly FastColoredTextBox editor;
            private readonly InstructionDocs Docs;
            private readonly AutocompleteContext context;

            public DocAutocompleteItemList(
                List<DocAutocompleteItem> items,
                FastColoredTextBox editor,
                InstructionDocs Docs,
                AutocompleteContext context)
            {
                this.items = items;
                this.editor = editor;
                this.Docs = Docs;
                this.context = context;
            }

            // Similar regex to JumpToEvent, but with not a specific number (id can be extracted)
            private static readonly Regex eventRegex = new Regex(@"^\s*(\$?)Event\(\s*([^,]+)\s*,", RegexOptions.Singleline);

            public IEnumerator<AutocompleteItem> GetEnumerator()
            {
                // Could also keep static system here (just return a list), as a global switch.
                // Not immediately possible with DarkAutocompleteItem, which requires precomputation.
                // For now, no reason to maintain it vs improving dynamic system. (We do lose contextless Ctrl+Space though.)

                // First, duplicate the fragment lookup
                Range fragment = editor.Selection.GetFragment(@"[\w\.]");
                string fragmentText = fragment.Text;

                // Recalculate arg
                context.ArgDoc = null;
                if (context.FuncName != null && context.FuncArg >= 0)
                {
                    if (!Docs.AllAliases.TryGetValue(context.FuncName, out string name))
                    {
                        name = context.FuncName;
                    }
                    if (Docs.AllArgs.TryGetValue(name, out List<EMEDF.ArgDoc> args) && args.Count > 0)
                    {
                        if (context.FuncArg < args.Count)
                        {
                            context.ArgDoc = args[context.FuncArg];
                        }
                        else if (args.Last().Vararg)
                        {
                            // This doesn't do anything at present - generic entity/flag etc autocomplete in future,
                            // but initializations should actually have types at some point.
                            context.ArgDoc = args.Last();
                        }
                    }
                }

                // Require non-empty, or in an argument context
                // See also the altered MinFragmentLength - basically, we use our own filtering.
                // This makes Ctrl+Space not that effective without a way to detect true in Show(bool forced).
                if (fragmentText.Length == 0)
                {
                    if (context.ArgDoc == null)
                    {
                        return Enumerable.Empty<AutocompleteItem>().GetEnumerator();
                    }
                    string before = fragment.GetCharsBeforeStart(2);
                    if (!(before.EndsWith("(") || before.EndsWith(", ")))
                    {
                        return Enumerable.Empty<AutocompleteItem>().GetEnumerator();
                    }
                }

                // Hacky way to detect being in an expression (middle of line or not)
                Range linePrefix = editor.GetRange(
                    new Place(0, fragment.Start.iLine),
                    fragment.Start);
                context.InExpression = !string.IsNullOrWhiteSpace(linePrefix.Text);

                // Update event-specific state.
                context.Fancy = FancyContextType.Any;
                // This involves multi-line scans, so unless we maintain this state in an incremental way per-line
                // (e.g. tracking line adds/removes/changes and using Line UniqueId), probably best for performance
                // to regenerate event-specific metadata only on save/load, and use it only debounced (like here in auto-complete).
                // Expand the original range far above us (not whole file, similar to folding block range).
                Range previous = editor.GetRange(
                    new Place(0, Math.Max(0, fragment.Start.iLine - 2000)),
                    fragment.End);
                foreach (Range r in previous.GetRangesByLinesReversed(eventRegex.ToString(), eventRegex.Options))
                {
                    Match result = eventRegex.Match(r.Text);
                    if (result.Success)
                    {
                        if (result.Groups[1].Value.Length > 0)
                        {
                            context.Fancy = FancyContextType.FancyOnly;
                        }
                        else
                        {
                            context.Fancy = FancyContextType.RegularOnly;
                        }
                    }
                    break;
                }

                return GetDynamicEnumerator(fragmentText);
            }

            private IEnumerator<AutocompleteItem> GetDynamicEnumerator(string fragmentText)
            {
                DocAutocompleteItem latest = null;
                foreach (DocAutocompleteItem item in items)
                {
                    if (item.CalculateResult(fragmentText, context))
                    {
                        // Figure out maximum selected index (most recent), but some tricky logic to only auto-select type-appropriately.
                        // Hide this behind SelectPriority, which is (TypeAppropriate, LastSelectedIndex)
                        if (item.LastSelectedIndex > 0)
                        {
                            // Either the first match, or replacing an inappropriate item, or more recent than previous item and eligible.
                            if (latest == null
                                || item.GetSelectPriority().CompareTo(latest.GetSelectPriority()) > 0)
                            {
                                latest = item;
                            }
                        }
                    }
                }
                if (latest != null)
                {
                    latest.SpecialSelected = true;
                }

                return items.Where(i => i.Result != AutocompleteResult.None).OrderBy(i => i.GetSortKey(context)).GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public class DocAutocompleteItem : AutocompleteItem
        {
            private readonly List<string> prefixes = new List<string>();
            private readonly string enumName;

            public readonly AutocompleteCategory Category;
            public readonly FancyContextType Fancy;
            public readonly bool LowQuality;

            public AutocompleteResult Result { get; private set; }
            public bool TypeAppropriate { get; private set; }
            public bool SpecialSelected { get; set; }

            // Also info on last-selected (use some kind of global counter perhaps? start from 0, set selected to counter++)
            public DocAutocompleteItem(
                AutocompleteCategory Category,
                FancyContextType Fancy,
                bool LowQuality,
                string text,
                string menuText)
                : base(text, ImageIndices[Category], menuText)
            {
                this.Category = Category;
                this.Fancy = Fancy;
                this.LowQuality = LowQuality;
                // Heuristic for now: capital letter after non-capital one, keep it simple.
                // This can't really distinguish word boundaries like SFXType.
                if (text.Length > 0)
                {
                    prefixes.Add(text);
                    bool prevUpper = char.IsUpper(text[0]);
                    for (int i = 1; i < text.Length; i++)
                    {
                        bool upper = char.IsUpper(text[i]);
                        if (upper && !prevUpper)
                        {
                            prefixes.Add(text.Substring(i));
                        }
                        prevUpper = upper;
                    }
                }
                if (Category == AutocompleteCategory.Enum)
                {
                    // Parse out enum types from names. This will ignore global ones.
                    // TODO: Pass in richer enum info here.
                    string[] parts = text.Split('.');
                    if (parts.Length == 2)
                    {
                        enumName = parts[0];
                    }
                }
            }

            public override string GetTextForReplace()
            {
                // Instructions/builtins and conditions are both functions at present, though this may change
                if (Category == AutocompleteCategory.Instruction || Category == AutocompleteCategory.Condition)
                {
                    return Text + "(";
                }
                // Default
                return Text;
            }

            public override string ToString()
            {
                // Text to show in menu, by default MenuText
                return base.ToString();
            }

            public bool CalculateResult(string fragmentText, AutocompleteContext context)
            {
                Result = AutocompleteResult.None;
                TypeAppropriate = false;
                SpecialSelected = false;

                if (enumName != null && context.ArgDoc?.EnumDoc?.DisplayName == enumName)
                {
                    TypeAppropriate = true;
                }
                if (fragmentText.Length == 0)
                {
                    if (TypeAppropriate)
                    {
                        Result = AutocompleteResult.Unconditional;
                    }
                    return TypeAppropriate;
                }

                // Normally, this checks if Text.StartsWith(fragmentText) && Text != fragmentText
                // The latter case may be confusing here, as many names appear as infixes in other names.
                int prefixIndex = prefixes.FindIndex(p => p.StartsWith(fragmentText, StringComparison.InvariantCultureIgnoreCase));
                if (prefixIndex == -1)
                {
                    return false;
                }
                if (context.Fancy != FancyContextType.Any && Fancy != FancyContextType.Any && Fancy != context.Fancy)
                {
                    return false;
                }
                if (prefixIndex == 0)
                {
                    Result = AutocompleteResult.Prefix;
                }
                else
                {
                    Result = LowQuality ? AutocompleteResult.LowQualityInfix : AutocompleteResult.Infix;
                }
                return true;
            }

            public override CompareResult Compare(string fragmentText)
            {
                if (Result == AutocompleteResult.None)
                {
                    return CompareResult.Hidden;
                }
                return SpecialSelected ? CompareResult.VisibleAndSelected : CompareResult.Visible;
            }

            public int LastSelectedIndex { get; private set; }

            public void MarkSelected(AutocompleteContext context)
            {
                LastSelectedIndex = ++context.SelectedIndex;
            }

            public IComparable GetSelectPriority()
            {
                return (TypeAppropriate, LastSelectedIndex);
            }

            public IComparable GetSortKey(AutocompleteContext context = null)
            {
                // Heuristics for ordering. Remember that false comes first!
                // At the moment, make category come before result, meaning prefix->infix within each category.
                // This is mainly acceptable because of our extremely hacky expression/statement heuristic.
                AutocompleteCategory cat = Category;
                if (context != null && context.InExpression && cat == AutocompleteCategory.Instruction)
                {
                    // The heuristic: bump instructions after conditions.
                    cat = AutocompleteCategory.ExpressionInstruction;
                }
                return (!TypeAppropriate, cat, Result, Text);
            }
        }
    }
}
