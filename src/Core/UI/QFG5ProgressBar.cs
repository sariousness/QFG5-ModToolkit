using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    public class QFG5ProgressBar : UserControl
    {
        private float percent = 0;
        public float Value
        {
            get => percent;
            set
            {
                float newPercent = Math.Max(0, Math.Min(100, value));
                if (percent == newPercent) return;
                percent = newPercent;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
            }
        }

        protected override AccessibleObject CreateAccessibilityInstance()
        {
            return new QFG5ProgressBarAccessibleObject(this);
        }

        private sealed class QFG5ProgressBarAccessibleObject : Control.ControlAccessibleObject
        {
            private readonly QFG5ProgressBar owner;

            public QFG5ProgressBarAccessibleObject(QFG5ProgressBar owner)
                : base(owner)
            {
                this.owner = owner;
            }

            public override AccessibleRole Role
            {
                get { return AccessibleRole.ProgressBar; }
            }

            public override string Value
            {
                get
                {
                    float currentPercent = owner.percent;
                    return currentPercent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
                }
                set { }
            }

            public override string Description
            {
                get
                {
                    return string.IsNullOrEmpty(owner.AccessibleDescription)
                        ? "Progress from 0 to 100 percent."
                        : owner.AccessibleDescription;
                }
            }
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
            Graphics g = e.Graphics;

            // Recessed track background
            g.FillRectangle(QFG5Theme.BrushSurfaceRecessed, this.ClientRectangle);

            // Subtle recessed frame around track
            QFG5Theme.DrawRecessedFrame(g, this.ClientRectangle);

            // Progress fill (inset by 2px within the frame)
            if (percent > 0)
            {
                int maxFillWidth = this.Width - 4;
                int fillWidth = (int)(maxFillWidth * (percent / 100f));
                if (fillWidth > 0 && this.Height > 4)
                {
                    Rectangle fillRect = new Rectangle(2, 2, fillWidth, this.Height - 4);
                    
                    // Rich warm gold fill
                    g.FillRectangle(QFG5Theme.BrushGold, fillRect);

                    // Metallic sheen: 1px top highlight line
                    using (Pen pHi = new Pen(Color.FromArgb(255, 230, 130)))
                    {
                        g.DrawLine(pHi, fillRect.Left, fillRect.Top, fillRect.Right - 1, fillRect.Top);
                    }

                    // 1px bottom bevel shadow line
                    using (Pen pLo = new Pen(Color.FromArgb(150, 110, 20)))
                    {
                        g.DrawLine(pLo, fillRect.Left, fillRect.Bottom - 1, fillRect.Right - 1, fillRect.Bottom - 1);
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
