using System;
using System.Drawing;
using System.Windows.Forms;

namespace QFG5Extractor.Core.UI
{
    public class QFG5PathField : UserControl
    {
        private TextBox textBox;
        
        public new string Text
        {
            get => textBox.Text;
            set => textBox.Text = value;
        }

        public new event KeyEventHandler KeyDown
        {
            add => textBox.KeyDown += value;
            remove => textBox.KeyDown -= value;
        }

        public QFG5PathField()
        {
            this.Height = 38;
            this.BackColor = Color.Transparent;
            this.Padding = new Padding(4, 5, 4, 4);

            textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new Font("Arial", 12, FontStyle.Bold),
                Multiline = true // Allows it to fill the height of the frame
            };
            this.Controls.Add(textBox);
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
