using QFG5Extractor.Core;
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
            get => textBox != null ? textBox.Text : string.Empty;
            set { if (textBox != null) textBox.Text = value; }
        }

        public new string AccessibleName
        {
            get { return base.AccessibleName; }
            set
            {
                base.AccessibleName = value;
                if (textBox != null) textBox.AccessibleName = value;
            }
        }

        public new string AccessibleDescription
        {
            get { return base.AccessibleDescription; }
            set
            {
                base.AccessibleDescription = value;
                if (textBox != null) textBox.AccessibleDescription = value;
            }
        }

        public bool ReadOnly
        {
            get => textBox != null && textBox.ReadOnly;
            set { if (textBox != null) textBox.ReadOnly = value; }
        }

        public new event KeyEventHandler KeyDown
        {
            add => textBox.KeyDown += value;
            remove => textBox.KeyDown -= value;
        }

        public QFG5PathField()
        {
            this.Height = 30;
            this.DoubleBuffered = true;
            this.BackColor = QFG5Theme.ColorSurfaceRecessed;
            this.Padding = new Padding(6, 4, 6, 4);

            textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = QFG5Theme.ColorSurfaceRecessed,
                ForeColor = QFG5Theme.ColorTextPrimary,
                Font = QFG5Theme.BodyBoldFont,
                Multiline = false,
                AccessibleName = base.AccessibleName,
                AccessibleDescription = base.AccessibleDescription
            };

            textBox.TextChanged += (s, e) => OnTextChanged(e);
            this.Controls.Add(textBox);

            UpdateTextBoxBounds();
        }

        private void UpdateTextBoxBounds()
        {
            if (textBox == null) return;
            int padX = this.Padding.Left;
            int availWidth = Math.Max(10, this.ClientSize.Width - padX - this.Padding.Right);
            int tbHeight = textBox.PreferredHeight;
            int y = Math.Max(0, (this.ClientSize.Height - tbHeight) / 2);
            textBox.SetBounds(padX, y, availWidth, tbHeight);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            UpdateTextBoxBounds();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateTextBoxBounds();
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.FillRectangle(QFG5Theme.BrushSurfaceRecessed, this.ClientRectangle);
            QFG5Theme.DrawRecessedFrame(e.Graphics, this.ClientRectangle);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (textBox != null) textBox.Focus();
        }
    }
}
