using System;
using System.Drawing;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    public class QFG5PreviewFrame : UserControl
    {
        public QFG5PreviewFrame()
        {
            this.Size = new Size(200, 200);
            this.DoubleBuffered = true;
            this.BackColor = QFG5Theme.ColorSurfaceRecessed;
            this.Padding = new Padding(2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.FillRectangle(QFG5Theme.BrushSurfaceRecessed, this.ClientRectangle);
            QFG5Theme.DrawRecessedFrame(e.Graphics, this.ClientRectangle);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}
