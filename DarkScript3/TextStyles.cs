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
using Range = FastColoredTextBoxNS.Range;

namespace DarkScript3
{
    public class TextStyles
    {
        // After changing these, make sure to SetGlobalStyles on any textbox.
        public static TextStyle Comment { get; private set; } = MakeStyle(87, 166, 74, renderCJK: true);
        public static TextStyle String = MakeStyle(214, 157, 133);
        public static TextStyle Keyword = MakeStyle(86, 156, 214);
        public static TextStyle ToolTipKeyword = MakeStyle(106, 176, 234);
        public static TextStyle EnumProperty = MakeStyle(255, 150, 239);
        public static TextStyle EnumConstant = MakeStyle(78, 201, 176);
        public static TextStyle Number = MakeStyle(181, 206, 168);
        public static TextStyle EnumType = MakeStyle(180, 180, 180);
        public static BoxStyle HighlightToken = BoxStyle.Make(Alphaify(127, Color.White));
        public static Color BackColor = Color.FromArgb(30, 30, 30);
        public static Color ForeColor = Color.Gainsboro;
        // Should be set to match ForeColor
        public static TextStyle DefaultCJK = MakeStyle(Color.Gainsboro, renderCJK: true);
        // The default alpha value in FCTB is 60 if none is specified.
        public static Color SelectionColor = Alphaify(100, Color.White);

        private static Color Alphaify(int alpha, Color color)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        // Always make defensive copies of this, since FastColoredTextBox likes disposing it
        private static Font InnerFont = new Font("Consolas", 8.25F);
        public static Font Font
        {
            // Prefer to use FontFor, for auto-resize handling.
            get => (Font)InnerFont.Clone();
            set => InnerFont = (Font)value.Clone();
        }

        public static Font FontFor(FastColoredTextBox tb)
        {
            // This is fairly hacky. This logic is used from both SetGlobalFont (cheap size change on zoom)
            // and RefreshGlobalStyles, used in a few circumstances.
            Font f = Font;
            if (tb?.Name == "tipBox")
            {
                // Heuristic for less intrusive tips: above 8pt, show at 80% of size
                if (f.SizeInPoints > 8)
                {
                    f = new Font(f.Name, Math.Max(8f, f.SizeInPoints * 0.8f));
                }
            }
            return f;
        }

        public static List<Style> HighlightStyles => new List<Style> { Keyword, Number, EnumConstant, EnumProperty };
        public static List<Style> SyntaxStyles => new List<Style> { Comment, String, Keyword, ToolTipKeyword, EnumProperty, EnumConstant, Number, EnumType };

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

        // Modifications

        private static string HexColor(TextStyle s)
        {
            return HexColor(s.ForeBrush);
        }

        private static string HexColor(Brush brush)
        {
            return HexColor((brush as SolidBrush).Color);
        }

        private static string HexColor(Color c)
        {
            return c.ToArgb().ToString("X");
        }

        public static bool ShowStyleEditor()
        {
            StyleConfig colors = new StyleConfig();
            if (colors.ShowDialog() != DialogResult.OK)
            {
                return false;
            }
            Comment = MakeStyle(colors.commentSetting.Color, renderCJK: true);
            String = MakeStyle(colors.stringSetting.Color);
            Keyword = MakeStyle(colors.keywordSetting.Color);
            ToolTipKeyword = MakeStyle(colors.ttKeywordSetting.Color);
            EnumProperty = MakeStyle(colors.enumPropSetting.Color);
            EnumConstant = MakeStyle(colors.globalConstSetting.Color);
            Number = MakeStyle(colors.numberSetting.Color);
            EnumType = MakeStyle(colors.toolTipEnumType.Color);
            HighlightToken = BoxStyle.Make(Alphaify(127, colors.highlightBoxSetting.Color));

            BackColor = colors.backgroundSetting.Color;
            SelectionColor = Alphaify(100, colors.highlightSetting.Color);
            ForeColor = colors.plainSetting.Color;
            DefaultCJK = MakeStyle(ForeColor, renderCJK: true);

            SetFontIfMonospace(colors.FontSetting);
            SaveColors();
            return true;
        }

        public static void SaveColors()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Comment=" + HexColor(Comment));
            sb.AppendLine("String=" + HexColor(String));
            sb.AppendLine("Keyword=" + HexColor(Keyword));
            sb.AppendLine("ToolTipKeyword=" + HexColor(ToolTipKeyword));
            sb.AppendLine("EnumProperty=" + HexColor(EnumProperty));
            sb.AppendLine("EnumConstant=" + HexColor(EnumConstant));
            sb.AppendLine("Number=" + HexColor(Number));
            sb.AppendLine("EnumType=" + HexColor(EnumType));
            sb.AppendLine("Background=" + HexColor(BackColor));
            sb.AppendLine("Highlight=" + HexColor(SelectionColor));
            sb.AppendLine("HighlightBox=" + HexColor(HighlightToken.BorderPen.Brush));
            sb.AppendLine("Default=" + HexColor(ForeColor));
            sb.AppendLine("Font=" + new FontConverter().ConvertToInvariantString(Font));
            File.WriteAllText("colors.cfg", sb.ToString());
        }

        public static void LoadColors()
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

            Comment = MakeStyle(colorFromHex("Comment"), renderCJK: true);
            String = MakeStyle(colorFromHex("String"));
            Keyword = MakeStyle(colorFromHex("Keyword"));
            ToolTipKeyword = MakeStyle(colorFromHex("ToolTipKeyword"));
            EnumProperty = MakeStyle(colorFromHex("EnumProperty"));
            EnumConstant = MakeStyle(colorFromHex("EnumConstant"));
            Number = MakeStyle(colorFromHex("Number"));
            EnumType = MakeStyle(colorFromHex("EnumType"));

            SelectionColor = colorFromHex("Highlight");
            BackColor = colorFromHex("Background");
            ForeColor = colorFromHex("Default");
            DefaultCJK = MakeStyle(ForeColor, renderCJK: true);

            // Added in later version
            if (cfg.ContainsKey("HighlightBox"))
            {
                HighlightToken = BoxStyle.Make(colorFromHex("HighlightBox"));
            }
            if (cfg.ContainsKey("Font"))
            {
                if (new FontConverter().ConvertFromInvariantString(cfg["Font"]) is Font font)
                {
                    if (new InstalledFontCollection().Families.Any(family => family.Name == font.Name))
                    {
                        SetFontIfMonospace(font);
                    }
                }
            }
        }

        private static void SetFontIfMonospace(Font newFont)
        {
            if (!new InstalledFontCollection().Families.Any(family => family.Name == newFont.Name))
            {
                return;
            }
            FastColoredTextBox testBox = new FastColoredTextBox();
            testBox.Font = (Font)newFont.Clone();
            if (testBox.Font.Name == newFont.Name)
            {
                Font = newFont;
            }
            else
            {
                FontConverter converter = new FontConverter();
                MessageBox.Show(
                    $"Font \"{converter.ConvertToInvariantString(newFont)}\" detected as not monospace."
                        + $" Reverting back to \"{converter.ConvertToInvariantString(Font)}\".");
            }
        }
    }
}
