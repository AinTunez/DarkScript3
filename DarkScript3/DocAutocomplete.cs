using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FastColoredTextBoxNS;
using DarkScript3.Properties;
using Range = FastColoredTextBoxNS.Range;

namespace DarkScript3
{
    public static class DocAutocomplete
    {
        public class AutocompleteContext
        {
            // Creates a new context. At present, this is limited to per-file.
            public AutocompleteContext(string resourceStr, string filename)
            {
                Game = InstructionDocs.GameNameFromResourceName(resourceStr);
                if (filename != null && filename.StartsWith("m"))
                {
                    // Strip extension here
                    Map = filename.Split('.')[0];
                }
            }

            // Overall map data
            public string Game { get; private set; }
            public string Map { get; private set; }
            // Currently shown arg, determined through hacky parsing
            public string FuncName { get; set; }
            public int FuncArg { get; set; }
            // Updated only when needed, based on the above two. Use UpdateContextArgDoc
            public EMEDF.ArgDoc ArgDoc { get; set; }
            // Whether in a fancy event or not, determined through hacky parsing
            public FancyContextType Fancy { get; set; }
            // Heuristic for expression vs statement context. This is extremely poor at the moment and relies on newlines.
            public bool InExpression { get; set; }
            // Incremented and assigned every time an item is selected, to keep a priority list of sorts.
            // Only to be used by DocAutocomplete.
            internal int SelectedIndex { get; set; }

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
            Instruction, Condition, ExpressionInstruction, Enum,
            // These have subtypes and currently appear with specific arguments
            Map,
            // This category includes Param, Fmg, MapId, and others. For now, no need to distinguish them.
            Custom,
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
            [AutocompleteCategory.Map] = 3,
            [AutocompleteCategory.Custom] = 3,
        };

        public static readonly Regex AutocompleteWordRe = new Regex(@"[\w\.]");
        public static readonly Regex AutocompleteNonWordRe = new Regex(@"[^\w\.]");

        public class DocAutocompleteItemList : IEnumerable<AutocompleteItem>
        {
            private readonly List<DocAutocompleteItem> items;
            private readonly FastColoredTextBox editor;
            private readonly InstructionDocs Docs;
            private readonly SoapstoneMetadata metadata;
            private readonly AutocompleteContext context;

            public DocAutocompleteItemList(
                List<DocAutocompleteItem> items,
                FastColoredTextBox editor,
                InstructionDocs Docs,
                SoapstoneMetadata metadata,
                AutocompleteContext context)
            {
                this.items = items;
                this.editor = editor;
                this.Docs = Docs;
                this.metadata = metadata;
                this.context = context;
            }

            // Similar regex to JumpToEvent, but with not a specific number (id can be extracted)
            private static readonly Regex eventRegex = new Regex(@"^\s*(\$?)Event\(\s*([^,]+)\s*,", RegexOptions.Singleline);
            private static readonly bool useFancyContext = true;

            public IEnumerator<AutocompleteItem> GetEnumerator()
            {
                // Could also keep static system here (just return a list), as a global switch.
                // Not immediately possible with DarkAutocompleteItem, which requires precomputation.
                // For now, no reason to maintain it vs improving dynamic system. (We do lose contextless Ctrl+Space though.)

                // Don't autocomplete in comments, as far as we can detect them. Fragment lookup uses range start.
                Place startPlace = editor.Selection.Start;
                // Edge case is that end of line never has styles, so use character before end
                if (editor.Selection.CharAfterStart == '\n' && startPlace.iChar > 0)
                {
                    startPlace = new Place(startPlace.iChar - 1, startPlace.iLine);
                }
                List<Style> styles = editor.GetStylesOfChar(startPlace);
                if (styles.Contains(TextStyles.Comment))
                {
                    return Enumerable.Empty<AutocompleteItem>().GetEnumerator();
                }

                // First, duplicate the fragment lookup
                Range fragment = editor.Selection.GetFragment(AutocompleteWordRe.ToString());
                string fragmentText = fragment.Text;

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
                // Doing fancy filtering is almost certainly a good idea.
                // It could be made into a setting at some later point if people require no filtering.
                if (useFancyContext)
                {
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
                }

                return GetDynamicEnumerator(fragment, fragmentText);
            }

            private IEnumerator<AutocompleteItem> GetDynamicEnumerator(Range fragment, string fragmentText)
            {
                List<DocAutocompleteItem> argItems = new List<DocAutocompleteItem>();
                if (context.ArgDoc?.MetaType != null && metadata.IsOpen())
                {
                    // Recalculate arg data, so everything is consistent
                    List<string> argList = new List<string>();
                    EditorGUI.ParseFuncAtRange(fragment, out string funcName, out int argIndex, argList);
                    EMEDF.ArgDoc argDoc = Docs.GetHeuristicArgDoc(funcName, argIndex);
                    if (argDoc?.MetaType != null)
                    {
                        argItems.AddRange(GetTypeEnumerator(funcName, argDoc, argList));
                    }
                }

                DocAutocompleteItem latest = null;
                // argItems comes before items since it's type appropriate, even if the category is later
                foreach (DocAutocompleteItem item in argItems.Concat(items))
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

                return argItems.Concat(items)
                    .Where(i => i.Result != AutocompleteResult.None)
                    .OrderBy(i => i.GetSortKey(context))
                    .GetEnumerator();
            }

            private IEnumerable<DocAutocompleteItem> GetTypeEnumerator(string funcName, EMEDF.ArgDoc argDoc, List<string> argList)
            {
                EMEDF.DarkScriptType metaType = argDoc.MetaType;
                // Map entity data. It is filtered per-map
                if (metaType.DataType == "entity" && context.Map != null && metadata.IsMapDataAvailable())
                {
                    // Three cases: normal mapping, override first arg, override second arg
                    if (metaType.OverrideTypes == null)
                    {
                        List<string> types = metaType.AllTypes.ToList();
                        if (types != null && types.Count == 0)
                        {
                            types = null;
                        }
                        // This is just a simple filter. Hopefully not too much CPU is involved, given the critical path here.
                        // It could be cached, but we'd also want filters for type etc.
                        return metadata.GetMapAutocompleteItems(context.Game, context.Map, types);
                    }
                    else
                    {
                        if (argDoc.EnumDoc?.DisplayName == metaType.OverrideEnum)
                        {
                            return metadata.GetMultiMapAutocompleteItems(context.Game, context.Map, metaType);
                        }
                        else
                        {
                            List<int> enumVals = Docs.GetArgsAsInts(argList, funcName, new List<string> { metaType.MultiNames[0] });
                            if (enumVals != null && enumVals.Count == 1)
                            {
                                List<string> types = metaType.OverrideTypes.Where(e => e.Value.Value == enumVals[0]).Select(e => e.Key).ToList();
                                if (types.Count > 0)
                                {
                                    return metadata.GetMapAutocompleteItems(context.Game, context.Map, types);
                                }
                            }
                        }
                    }
                }
                else if (metaType.DataType == "param" && metaType.Type != null)
                {
                    return metadata.GetParamRowItems(context.Game, metaType.Type);
                }
                else if (metaType.DataType == "param" && metaType.OverrideTypes != null)
                {
                    Console.WriteLine($"here with {argDoc.EnumDoc?.DisplayName} {metaType.OverrideEnum}");
                    // WaitFor(PlayerHasItem
                    // Override first arg, or override second arg (if it uniquely determines a param)
                    if (argDoc.EnumDoc?.DisplayName == metaType.OverrideEnum)
                    {
                        return metadata.GetMultiParamAutocompleteItems(context.Game, metaType);
                    }
                    else
                    {
                        List<int> enumVals = Docs.GetArgsAsInts(argList, funcName, new List<string> { metaType.MultiNames[0] });
                        if (enumVals != null && enumVals.Count == 1)
                        {
                            List<string> types = metaType.OverrideTypes.Where(e => e.Value.Value == enumVals[0]).Select(e => e.Key).ToList();
                            if (types.Count == 1)
                            {
                                return metadata.GetParamRowItems(context.Game, types[0]);
                            }
                        }
                    }
                }
                else if (metaType.DataType == "fmg" && metaType.Type != null)
                {
                    return metadata.GetFmgEntryItems(context.Game, metaType.Type);
                }
                else if (metaType.DataType == "mapint")
                {
                    return metadata.GetMapNamePartsItems(context.Game);
                }
                else if (metaType.DataType == "mapparts" && metaType.MultiNames != null)
                {
                    // Only support matching at first argument.
                    // Otherwise definitely have to deal with rewriting other arguments.
                    if (argDoc?.Name == metaType.MultiNames[0])
                    {
                        return metadata.GetMapNamePartsItems(context.Game);
                    }
                }
                return Array.Empty<DocAutocompleteItem>();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public class DocAutocompleteItem : AutocompleteItem, IComparable<DocAutocompleteItem>
        {
            private readonly List<string> prefixes = new List<string>();

            public readonly object ID;
            public readonly AutocompleteCategory Category;
            public readonly FancyContextType Fancy;
            public readonly bool LowQuality;
            // Object for filtering, bit of a loose system. Should be a fixed type for a given Category.
            // For Enum, this is a fixed enum name.
            // For Map, this is an EntityData
            public readonly object SubType;

            public AutocompleteResult Result { get; private set; }
            public bool TypeAppropriate { get; private set; }
            public bool SpecialSelected { get; set; }

            // Also info on last-selected (use some kind of global counter perhaps? start from 0, set selected to counter++)
            public DocAutocompleteItem(
                object ID,
                string menuText,
                AutocompleteCategory Category,
                FancyContextType Fancy,
                bool LowQuality,
                object SubType)
                : base(ID.ToString(), ImageIndices[Category], menuText)
            {
                this.ID = ID;
                this.Category = Category;
                this.Fancy = Fancy;
                this.LowQuality = LowQuality;
                this.SubType = SubType;
                // Heuristic for DarkScript3 names: capital letter after non-capital one, keep it simple.
                // This can't really distinguish word boundaries like SFXType.
                if (ID is string && Text.Length > 0)
                {
                    // First prefix is special, allows Prefix match (instead of Infix)
                    prefixes.Add(Text);
                    bool prevUpper = char.IsUpper(Text[0]);
                    for (int i = 1; i < Text.Length; i++)
                    {
                        bool upper = char.IsUpper(Text[i]);
                        if (upper && !prevUpper)
                        {
                            prefixes.Add(Text.Substring(i));
                        }
                        prevUpper = upper;
                    }
                }
                // Match ids exactly, also add suffix matches for entity ids.
                if (ID is int num)
                {
                    prefixes.Add(Text);
                    if (Category == AutocompleteCategory.Map && Text.Length > 5)
                    {
                        // Last 4 digits, and last 3 for an enemy
                        prefixes.Add(Text.Substring(Text.Length - 4));
                        if (num % 10000 < 1000)
                        {
                            prefixes.Add(Text.Substring(Text.Length - 3));
                        }
                    }
                }
                if (SubType is SoapstoneMetadata.DisplayData displayData && displayData.MatchText != null)
                {
                    // This should be preprocessed with infixes
                    prefixes.AddRange(displayData.MatchText);
                }
            }

            public DocAutocompleteItem CopyWithPrefix(string prefix)
            {
                DocAutocompleteItem item = (DocAutocompleteItem)MemberwiseClone();
                item.Text = prefix + item.Text;
                return item;
            }

            public bool IsFunction => Category == AutocompleteCategory.Instruction || Category == AutocompleteCategory.Condition;

            public override string GetTextForReplace()
            {
                // Instructions/builtins and conditions are both functions at present, though this may change.
                // Historically, we've added the (, though e.g. Visual Studio doesn't do this.
                // In just this case, there is no automatic re-autocomplete (which is fine for enums),
                // since the insert action is its own KeyPressed which is not an autocomplete.
                // For functions, however, we should manually re-autocomplete on selection, to start loading the argument.
                if (IsFunction)
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

                if (Category == AutocompleteCategory.Enum && SubType != null && SubType.Equals(context.ArgDoc?.EnumDoc?.Name))
                {
                    TypeAppropriate = true;
                }
                if (Category == AutocompleteCategory.Map || Category == AutocompleteCategory.Custom)
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
                // The latter case is not exactly what we want - many names appear as infixes in other names, and we don't want
                // to miss an item from the menu which should be selected. We probably do want to avoid a single menu item of
                // just what's already selected (esp an enum), but even this may be useful with selecting its case.
                // Add this very custom case probably at menu-level if needed.
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
                return (!TypeAppropriate, cat, Result, ID);
            }

            public int CompareTo(DocAutocompleteItem other)
            {
                return GetSortKey().CompareTo(other.GetSortKey());
            }
        }
    }
}
