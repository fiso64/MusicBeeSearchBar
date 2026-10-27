using System;
using System.Drawing;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    internal sealed class TooltipWindow : Form
    {
        private readonly Label _label;
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return parameters;
            }
        }

        public TooltipWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = SystemColors.Info;
            ForeColor = SystemColors.InfoText;
            Padding = new Padding(1);
            _label = new Label { AutoSize = true, Location = new Point(1, 1), Padding = new Padding(5, 3, 5, 3) };
            Controls.Add(_label);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, SystemColors.ControlDark, ButtonBorderStyle.Solid);
        }

        public void ShowNearCursor(Form owner, string text)
        {
            _label.Text = text;
            PerformLayout();
            var size = GetPreferredSize(Size.Empty);
            var cursorPosition = Cursor.Position;
            var cursor = Cursor.Current ?? Cursors.Default;
            var cursorTop = cursorPosition.Y - cursor.HotSpot.Y;
            var cursorBottom = cursorTop + cursor.Size.Height;
            var area = Screen.FromPoint(cursorPosition).WorkingArea;
            int margin = Math.Max(4, (int)Math.Ceiling(6 * owner.DeviceDpi / 96.0));
            int x = Math.Max(area.Left, Math.Min(cursorPosition.X + margin, area.Right - size.Width));
            int y = cursorBottom + margin;
            if (y + size.Height > area.Bottom) y = cursorTop - margin - size.Height;
            y = Math.Max(area.Top, y);
            Location = new Point(x, y);
            if (!Visible) Show(owner);
        }
    }

}
