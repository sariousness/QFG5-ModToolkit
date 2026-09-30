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
            this.ForeColor = Color.Black;
            this.Font = new Font("Arial", 10, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            Image bgImg = this.ModelType == ButtonState.Pressed ? QFG5Theme.ButtonPressed : QFG5Theme.ButtonIdle;
            
            if (bgImg != null) {
                g.DrawImage(bgImg, 0, 0, this.Width, this.Height);
            } else {
                g.FillRectangle(new SolidBrush(Color.SlateGray), 0, 0, this.Width, this.Height);
            }

            if (isSelected) {
                using (SolidBrush selBrush = new SolidBrush(Color.FromArgb(110, 218, 165, 32))) {
                    g.FillRectangle(selBrush, 0, 0, this.Width, this.Height);
                }
            }
            else if (isHovered && this.ModelType != ButtonState.Pressed) {
                using (SolidBrush hoverBrush = new SolidBrush(Color.FromArgb(80, 255, 252, 210))) {
                    g.FillRectangle(hoverBrush, 0, 0, this.Width, this.Height);
                }
            }
            
            Rectangle textRect = this.ClientRectangle;

            if (this.Image != null) {
                int iconWidth = 32;
                int iconHeight = 32;
                int iconX = (this.Width - iconWidth) / 2;
                int iconY = 5;
                g.DrawImage(this.Image, iconX, iconY, iconWidth, iconHeight);
                
                // Adjust text rect to be below icon
                textRect = new Rectangle(0, iconY + iconHeight, this.Width, this.Height - (iconY + iconHeight));
            }

            string text = this.Text.ToUpper();
            // Add internal padding of 5px on sides
            Rectangle paddedRect = new Rectangle(textRect.X + 5, textRect.Y, textRect.Width - 10, textRect.Height);
            TextRenderer.DrawText(g, text, this.Font, paddedRect, this.ForeColor, 
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
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
            isHovered = true;
            this.Invalidate();
            base.OnMouseEnter(e);
        }

        public void ManualHover(bool hovered) {
            this.isHovered = hovered;
            this.Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e) {
            isHovered = false;
            this.Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent) {
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
