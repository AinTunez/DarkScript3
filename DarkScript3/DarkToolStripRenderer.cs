using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DarkScript3
{
    class DarkToolStripRenderer : ToolStripSystemRenderer
    {
        const int BorderThickness = 1;

        Color BackColor = Color.FromArgb(27, 27, 28);
        Color BackColor_Selected = Color.FromArgb(51, 51, 52);

        Color ForeColor = Color.FromArgb(240, 240, 240);
        Color ForeColor_Separator = Color.FromArgb(64, 64, 64);
        Color ForeColor_Disabled = Color.FromArgb(101, 101, 101);
        Color ForeColor_Outline = Color.FromArgb(51, 51, 55);

        static System.Drawing.Bitmap CustomCheck = null;

        protected override void InitializeItem(ToolStripItem item)
        {
            base.InitializeItem(item);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var brush = new System.Drawing.SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, 0, 0, e.Item.Bounds.Width, e.Item.Bounds.Height);
            }

            using (var brush = new System.Drawing.SolidBrush(ForeColor_Separator))
            {
                e.Graphics.FillRectangle(brush, 0, (e.Item.Bounds.Height / 2.0f) - 1, e.Item.Bounds.Width, 2);
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected && e.Item.Enabled)
            {
                using (var brush = new System.Drawing.SolidBrush(BackColor_Selected))
                {
                    e.Graphics.FillRectangle(brush, -2, -2, e.Item.Bounds.Width + 4, e.Item.Bounds.Height + 4);
                }
            }
            else
            {
                using (var brush = new System.Drawing.SolidBrush(BackColor))
                {
                    e.Graphics.FillRectangle(brush, -2, -2, e.Item.Bounds.Width + 4, e.Item.Bounds.Height + 4);
                }
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!e.Item.Enabled)
            {
                e.Item.ForeColor = ForeColor_Disabled;
            }
            else
            {
                e.Item.ForeColor = ForeColor;
            }

            base.OnRenderItemText(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            //base.OnRenderToolStripBackground(e);
            using (var brush = new System.Drawing.SolidBrush(BackColor))
            {
                e.Graphics.FillRectangle(brush, 0, 0, e.ToolStrip.Bounds.Width, e.ToolStrip.Bounds.Height);
            }

            using (var brush = new System.Drawing.SolidBrush(ForeColor_Outline))
            {
                e.Graphics.FillRectangle(brush, 0, 0, e.ToolStrip.Bounds.Width, BorderThickness);
                e.Graphics.FillRectangle(brush, 0, 0, BorderThickness, e.ToolStrip.Bounds.Height);
                e.Graphics.FillRectangle(brush, 0, e.ToolStrip.Bounds.Height - BorderThickness, e.ToolStrip.Bounds.Width, BorderThickness);
                e.Graphics.FillRectangle(brush, e.ToolStrip.Bounds.Width - BorderThickness, 0, BorderThickness, e.ToolStrip.Bounds.Height);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var brush = new System.Drawing.SolidBrush(ForeColor_Outline))
            {
                e.Graphics.FillRectangle(brush, 0, 0, e.ToolStrip.Bounds.Width, BorderThickness);
                e.Graphics.FillRectangle(brush, 0, 0, BorderThickness, e.ToolStrip.Bounds.Height);
                e.Graphics.FillRectangle(brush, 0, e.ToolStrip.Bounds.Height - BorderThickness, e.ToolStrip.Bounds.Width, BorderThickness);
                e.Graphics.FillRectangle(brush, e.ToolStrip.Bounds.Width - BorderThickness, 0, BorderThickness, e.ToolStrip.Bounds.Height);
            }
            //base.OnRenderToolStripBorder(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            if (CustomCheck == null)
            {
                CustomCheck = new System.Drawing.Bitmap(e.Image);
                for (int y = 0; (y <= (CustomCheck.Height - 1)); y++)
                {
                    for (int x = 0; (x <= (CustomCheck.Width - 1)); x++)
                    {
                        var c = CustomCheck.GetPixel(x, y);
                        CustomCheck.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, 255 - c.R, 255 - c.G, 255 - c.B));
                    }
                }
            }

            e.Graphics.DrawImage(CustomCheck, e.ImageRectangle);
        }
    }
}
