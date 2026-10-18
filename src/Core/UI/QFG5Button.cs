using QFG5Extractor.Core;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    public class QFG5Button : Button
    {
        public QFG5Button()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 0;
            this.FlatAppearance.MouseDownBackColor = Color.Transparent;
            this.FlatAppearance.MouseOverBackColor = Color.Transparent;
            this.BackColor = Color.Transparent;
            this.ForeColor = QFG5Theme.ColorTextDark;
            this.Font = QFG5Theme.ButtonFont;
            this.GotFocus += (s, e) => this.Invalidate();
            this.LostFocus += (s, e) => this.Invalidate();
        }

        public bool ImageOnLeft { get; set; }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!this.Enabled)
            {
                isHovered = false;
                ModelType = ButtonState.Normal;
            }
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            Image bgImg = (this.Enabled && this.ModelType == ButtonState.Pressed)
                ? QFG5Theme.ButtonPressed
                : QFG5Theme.ButtonIdle;

            DrawButtonSurface(g, bgImg, this.ClientRectangle);

            if (!this.Enabled) {
                g.FillRectangle(QFG5Theme.BrushDisabledOverlay, 0, 0, this.Width, this.Height);
            }
            else if (isSelected) {
                g.FillRectangle(QFG5Theme.BrushSelected, 0, 0, this.Width, this.Height);
            }
            else if (isHovered && this.ModelType != ButtonState.Pressed) {
                g.FillRectangle(QFG5Theme.BrushHoverGlow, 0, 0, this.Width, this.Height);
            }
            if (isSelected && this.ImageOnLeft && this.Width > 5 && this.Height > 8)
            {
                using (SolidBrush accent = new SolidBrush(QFG5Theme.ColorGoldBright))
                    g.FillRectangle(accent, 2, 4, 3, this.Height - 8);
            }

            Rectangle textRect = this.ClientRectangle;

            if (this.Image != null && ImageOnLeft) {
                int iconSize = Math.Min(30, Math.Max(16, this.Height - 12));
                int iconX = 10;
                int iconY = (this.Height - iconSize) / 2;
                DrawButtonIcon(g, iconX, iconY, iconSize, iconSize);
                textRect = new Rectangle(iconX + iconSize + 8, 0,
                    Math.Max(0, this.Width - iconX - iconSize - 16), this.Height);
            }
            else if (this.Image != null) {
                int iconSize = Math.Min(32, Math.Max(16, this.Height / 2));
                int iconX = (this.Width - iconSize) / 2;
                int iconY = 4;
                DrawButtonIcon(g, iconX, iconY, iconSize, iconSize);
                textRect = new Rectangle(0, iconY + iconSize, this.Width,
                    Math.Max(0, this.Height - iconY - iconSize));
            }

            string text = this.Text;
            if (!string.IsNullOrEmpty(text)) {
                Rectangle paddedRect = new Rectangle(textRect.X + 8, textRect.Y,
                    Math.Max(0, textRect.Width - 16), textRect.Height);
                if (this.Enabled && this.ModelType == ButtonState.Pressed) {
                    paddedRect.Offset(1, 1);
                }

                Color textColor = this.Enabled ? this.ForeColor : QFG5Theme.ColorTextDisabled;
                using (Font buttonFont = new Font(this.Font.FontFamily,
                    Math.Max(11f, this.Font.Size + 1.5f), FontStyle.Bold, this.Font.Unit))
                {
                    TextFormatFlags flags = TextFormatFlags.HorizontalCenter |
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                    flags |= paddedRect.Height >= buttonFont.Height * 2
                        ? TextFormatFlags.WordBreak
                        : TextFormatFlags.SingleLine;
                    TextRenderer.DrawText(g, text, buttonFont, paddedRect, textColor, flags);
                }
            }

            if (this.Focused && this.ShowFocusCues)
            {
                Rectangle focusRect = this.ClientRectangle;
                focusRect.Inflate(-3, -3);
                if (focusRect.Width > 0 && focusRect.Height > 0)
                    ControlPaint.DrawFocusRectangle(g, focusRect, QFG5Theme.ColorTextPrimary, QFG5Theme.ColorSurface);
            }
        }

        private void DrawButtonIcon(Graphics graphics, int x, int y, int width, int height)
        {
            if (!Enabled)
                ControlPaint.DrawImageDisabled(graphics, Image, x, y, QFG5Theme.ColorSurface);
            else
                graphics.DrawImage(Image, x, y, width, height);
        }

        private static void DrawButtonSurface(Graphics graphics, Image texture, Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            Rectangle outer = new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
            using (SolidBrush wood = new SolidBrush(QFG5Theme.ColorSurface))
                graphics.FillRectangle(wood, outer);

            Rectangle stone = Rectangle.Inflate(outer, -3, -3);
            if (stone.Width > 0 && stone.Height > 0)
            {
                if (texture != null)
                    DrawStoneTexture(graphics, texture, stone);
                else
                {
                    using (System.Drawing.Drawing2D.LinearGradientBrush fill =
                        new System.Drawing.Drawing2D.LinearGradientBrush(stone,
                            Color.FromArgb(112, 105, 88), Color.FromArgb(66, 60, 49),
                            System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                        graphics.FillRectangle(fill, stone);
                }
            }

            using (Pen border = new Pen(QFG5Theme.ColorBorderOuter))
                graphics.DrawRectangle(border, outer);
            if (outer.Width > 5 && outer.Height > 5)
            {
                using (Pen highlight = new Pen(Color.FromArgb(120, QFG5Theme.ColorGold)))
                    graphics.DrawLine(highlight, outer.Left + 2, outer.Top + 2, outer.Right - 2, outer.Top + 2);
                using (Pen shadow = new Pen(QFG5Theme.ColorBorderInner))
                    graphics.DrawRectangle(shadow, stone);
            }
        }

        private static void DrawStoneTexture(Graphics graphics, Image texture, Rectangle destination)
        {
            Rectangle source = new Rectangle(
                (int)(texture.Width * 0.035),
                (int)(texture.Height * 0.09),
                (int)(texture.Width * 0.93),
                (int)(texture.Height * 0.82));
            float sourceRatio = (float)source.Width / source.Height;
            float destinationRatio = (float)destination.Width / destination.Height;

            if (sourceRatio > destinationRatio)
            {
                int width = (int)(source.Height * destinationRatio);
                source.X += (source.Width - width) / 2;
                source.Width = width;
            }
            else if (sourceRatio < destinationRatio)
            {
                int height = (int)(source.Width / destinationRatio);
                source.Y += (source.Height - height) / 2;
                source.Height = height;
            }

            graphics.DrawImage(texture, destination, source, GraphicsUnit.Pixel);
        }

        private ButtonState ModelType = ButtonState.Normal;
        private bool isHovered = false;
        private bool isSelected = false;

        public bool Selected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                this.Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e) {
            if (!this.Enabled) return;
            isHovered = true;
            this.Invalidate();
            base.OnMouseEnter(e);
        }

        public void ManualHover(bool hovered) {
            if (!this.Enabled && hovered) return;
            this.isHovered = hovered;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) {
            isHovered = false;
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent) {
            if (!this.Enabled) return;
            ModelType = ButtonState.Pressed;
            base.OnMouseDown(mevent);
            this.Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs mevent) {
            ModelType = ButtonState.Normal;
            base.OnMouseUp(mevent);
            this.Invalidate();
        }
        
        private enum ButtonState { Normal, Pressed }
    }
}
