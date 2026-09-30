using System;
using System.Drawing;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    public class QFG5ProgressBar : UserControl
    {
        private float percent = 0;
        public float Value
        {
            get => percent;
            set { percent = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        public QFG5ProgressBar()
        {
            this.Size = new Size(200, 24);
            this.DoubleBuffered = true;
            this.BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Graphics g = e.Graphics;

            // Removed border per request
            // Color borderColor = Color.FromArgb(40, 30, 20);
            // using (Pen p = new Pen(borderColor, 4))
            // {
            //     g.DrawRectangle(p, 2, 2, this.Width - 4, this.Height - 4);
            // }

            if (percent > 0)
            {
                int fillWidth = (int)((this.Width - 4) * (percent / 100f));
                if (fillWidth > 0) {
                    using (SolidBrush b = new SolidBrush(Color.Gold))
                    {
                        g.FillRectangle(b, 0, 0, fillWidth, this.Height);
                    }
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}
