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
            this.BackColor = Color.Transparent;
            this.Padding = new Padding(2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Strokes removed for Phase 15 polish
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }
    }
}
