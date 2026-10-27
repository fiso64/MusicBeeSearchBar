using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    internal sealed class SearchToolbar : Control
    {
        private static readonly string[] Labels = { "Artists", "Albums", "Songs", "Playlists", "Commands" };
        private static readonly string[] Prefixes = { "a:", "l:", "s:", "p:", ">" };
        private readonly Rectangle[] _pills = new Rectangle[Labels.Length];
        private readonly Theme _theme;
        private readonly float _scale;
        private readonly Font _pillFont;
        private readonly Timer _tooltipTimer;
        private TooltipWindow _tooltip;
        private Rectangle _detachBounds;
        private Rectangle _settingsBounds;
        private int _hovered = -1;
        private int _pressed = -1;
        private string _activePrefix;
        private bool _isDetached;

        public event Action<string> PrefixSelected;
        public event EventHandler DetachClicked;
        public event EventHandler SettingsClicked;

        public SearchToolbar(Theme theme, float scale, Font baseFont)
        {
            _theme = theme;
            _scale = scale;
            _pillFont = new Font(baseFont, FontStyle.Regular);
            _tooltipTimer = new Timer { Interval = 500 };
            _tooltipTimer.Tick += TooltipTimer_Tick;
            Font = _pillFont;
            Height = (int)(42 * scale);
            BackColor = theme.Background;
            ForeColor = theme.Foreground;
            TabStop = false;
            AccessibleName = "Search filters and controls";
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void SetDetached(bool detached)
        {
            if (_isDetached == detached) return;
            _isDetached = detached;
            HideTooltip();
            Invalidate();
        }

        public void SetActivePrefix(string text)
        {
            string prefix = GetPrefix(text);
            if (_activePrefix == prefix) return;
            _activePrefix = prefix;
            Invalidate();
        }

        public static string GetPrefix(string text)
        {
            string value = (text ?? string.Empty).TrimStart();
            if (value.StartsWith(">", StringComparison.Ordinal)) return ">";
            if (value.Length < 2 || value[1] != ':') return null;
            switch (char.ToLowerInvariant(value[0]))
            {
                case 'a': return "a:";
                case 'l': return "l:";
                case 's':
                case 't': return "s:";
                case 'p': return "p:";
                default: return null;
            }
        }

        public static string ReplacePrefix(string text, string prefix)
        {
            string value = (text ?? string.Empty).TrimStart();
            string existing = GetPrefix(value);
            if (existing != null) value = value.Substring(existing == ">" ? 1 : 2).TrimStart();
            return prefix == null || existing == prefix ? value : prefix + " " + value;
        }

        public static string NextPrefix(string text, bool backwards)
        {
            int current = Array.IndexOf(Prefixes, GetPrefix(text)) + 1;
            int count = Prefixes.Length + 1; // Include the unfiltered state.
            int next = (current + (backwards ? count - 1 : 1)) % count;
            return next == 0 ? null : Prefixes[next - 1];
        }

        public static int ContentStart(string text)
        {
            text = text ?? string.Empty;
            int start = 0;
            while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
            string prefix = GetPrefix(text);
            if (prefix != null)
            {
                start += prefix == ">" ? 1 : 2;
                while (start < text.Length && char.IsWhiteSpace(text[start])) start++;
            }
            return start;
        }

        private void UpdateBounds(Graphics graphics)
        {
            int pad = Math.Max(1, (int)(3 * _scale));
            int topInset = Math.Max(1, (int)(8 * _scale));
            int gap = Math.Max(1, (int)(4 * _scale));
            int gearWidth = Math.Max(20, (int)(28 * _scale));
            int itemHeight = Math.Max(1, Height - 2 * pad - topInset);
            int top = pad + topInset;
            _settingsBounds = new Rectangle(Math.Max(0, Width - gearWidth - pad), top, gearWidth, itemHeight);
            _detachBounds = new Rectangle(Math.Max(0, _settingsBounds.Left - gearWidth - gap), top, gearWidth, itemHeight);
            int available = Math.Max(0, _detachBounds.Left - 2 * pad - gap * Labels.Length);
            int[] widths = new int[Labels.Length];
            int total = 0;
            for (int i = 0; i < Labels.Length; i++)
            {
                widths[i] = TextRenderer.MeasureText(graphics, Labels[i], Font, Size.Empty,
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + (int)(14 * _scale);
                total += widths[i];
            }
            int x = pad;
            int used = 0;
            for (int i = 0; i < Labels.Length; i++)
            {
                int width = total <= available ? widths[i] : (i == Labels.Length - 1
                    ? Math.Max(0, available - used) : (int)((long)widths[i] * available / total));
                _pills[i] = new Rectangle(x, top, width, itemHeight);
                x += width + gap;
                used += width;
            }
        }

        private static GraphicsPath Rounded(Rectangle rect, int diameter)
        {
            var path = new GraphicsPath();
            diameter = Math.Min(diameter, Math.Min(rect.Width, rect.Height));
            if (diameter <= 1) { path.AddRectangle(rect); return path; }
            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            UpdateBounds(e.Graphics);
            for (int i = 0; i < _pills.Length; i++)
            {
                var rect = _pills[i];
                if (rect.Width <= 2 || rect.Height <= 2) continue;
                bool selected = _activePrefix == Prefixes[i];
                bool hovered = _hovered == i;
                using (var path = Rounded(rect, rect.Height))
                using (var brush = new SolidBrush(selected ? _theme.Highlight : Color.FromArgb(hovered ? 26 : 10, _theme.Foreground)))
                using (var pen = new Pen(Color.FromArgb(selected ? 100 : hovered ? 50 : 30, _theme.Foreground)))
                {
                    e.Graphics.FillPath(brush, path);
                    e.Graphics.DrawPath(pen, path);
                }
                TextRenderer.DrawText(e.Graphics, Labels[i], Font, rect,
                    selected ? _theme.Foreground : hovered ? _theme.Icon : _theme.IconDimmed,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            }

            DrawDetachIcon(e.Graphics);
            if (_settingsBounds.Width <= 0 || _settingsBounds.Height <= 0) return;
            float cx = _settingsBounds.Left + _settingsBounds.Width / 2f;
            float cy = _settingsBounds.Top + _settingsBounds.Height / 2f;
            float radius = Math.Min(7.5f * _scale, Math.Min(_settingsBounds.Width, _settingsBounds.Height) / 3f);
            var points = new PointF[32];
            for (int i = 0; i < 8; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    double angle = (i + (j + 0.5) / 4) * Math.PI / 4;
                    float distance = radius * ((j == 1 || j == 2) ? 1f : 0.72f);
                    points[i * 4 + j] = new PointF(cx + (float)Math.Cos(angle) * distance,
                        cy + (float)Math.Sin(angle) * distance);
                }
            }
            using (var pen = new Pen(Color.FromArgb(220, _hovered == Labels.Length + 1 ? _theme.Icon : _theme.IconDimmed), Math.Max(1f, 1.4f * _scale)))
            using (var path = new GraphicsPath())
            {
                pen.LineJoin = LineJoin.Round;
                path.AddPolygon(points);
                e.Graphics.DrawPath(pen, path);
                float hole = radius * 0.32f;
                e.Graphics.DrawEllipse(pen, cx - hole, cy - hole, hole * 2, hole * 2);
            }
        }

        private void DrawDetachIcon(Graphics graphics)
        {
            float cx = _detachBounds.Left + _detachBounds.Width / 2f;
            float cy = _detachBounds.Top + _detachBounds.Height / 2f;
            float unit = Math.Min(_scale, Math.Min(_detachBounds.Width, _detachBounds.Height) / 20f);
            var state = graphics.Save();
            try
            {
                graphics.TranslateTransform(cx, cy);
                if (_isDetached) graphics.RotateTransform(-35);
                using (var pen = new Pen(Color.FromArgb(220, _hovered == Labels.Length ? _theme.Icon : _theme.IconDimmed), Math.Max(1f, 1.4f * unit)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    graphics.DrawLines(pen, new[]
                    {
                        new PointF(-4 * unit, -6 * unit), new PointF(4 * unit, -6 * unit),
                        new PointF(3 * unit, -4 * unit), new PointF(3 * unit, -1 * unit),
                        new PointF(5 * unit, 1 * unit), new PointF(5 * unit, 2 * unit),
                        new PointF(-5 * unit, 2 * unit), new PointF(-5 * unit, unit),
                        new PointF(-3 * unit, -unit), new PointF(-3 * unit, -4 * unit),
                        new PointF(-4 * unit, -6 * unit)
                    });
                    graphics.DrawLine(pen, 0, 2 * unit, 0, 7 * unit);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void HideTooltip()
        {
            _tooltipTimer.Stop();
            if (_tooltip != null && !_tooltip.IsDisposed) _tooltip.Hide();
        }

        private void TooltipTimer_Tick(object sender, EventArgs e)
        {
            _tooltipTimer.Stop();
            if (_tooltip != null && !_tooltip.IsDisposed && _tooltip.Visible)
            {
                _tooltip.Hide();
                return;
            }
            if (_hovered != Labels.Length || IsDisposed || Disposing || !Visible) return;
            var owner = FindForm();
            if (owner == null || owner.IsDisposed || owner.Disposing) return;
            if (_tooltip == null || _tooltip.IsDisposed) _tooltip = new TooltipWindow();
            _tooltip.ShowNearCursor(owner, _isDetached ? "Attach search bar" : "Detach search bar");
            _tooltipTimer.Interval = 5000;
            _tooltipTimer.Start();
        }

        private int HitTest(Point location)
        {
            using (var graphics = CreateGraphics()) UpdateBounds(graphics);
            if (_settingsBounds.Contains(location)) return Labels.Length + 1;
            if (_detachBounds.Contains(location)) return Labels.Length;
            for (int i = 0; i < _pills.Length; i++)
                if (_pills[i].Contains(location)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hovered = HitTest(e.Location);
            if (_hovered != hovered)
            {
                HideTooltip();
                _hovered = hovered;
                if (hovered == Labels.Length)
                {
                    _tooltipTimer.Interval = 500;
                    _tooltipTimer.Start();
                }
                Invalidate();
            }
            Cursor = hovered >= 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            HideTooltip();
            _hovered = -1;
            Cursor = Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            HideTooltip();
            _pressed = HitTest(e.Location);
            if (_pressed >= 0) Capture = true;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int pressed = _pressed;
            _pressed = -1;
            if (Capture) Capture = false;
            if (e.Button != MouseButtons.Left || pressed < 0 || HitTest(e.Location) != pressed) return;
            if (pressed == Labels.Length + 1) SettingsClicked?.Invoke(this, EventArgs.Empty);
            else if (pressed == Labels.Length) DetachClicked?.Invoke(this, EventArgs.Empty);
            else PrefixSelected?.Invoke(Prefixes[pressed]);
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) _pressed = -1;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tooltipTimer.Stop();
                _tooltipTimer.Dispose();
                _tooltip?.Dispose();
            }
            base.Dispose(disposing);
            if (disposing) _pillFont?.Dispose();
        }
    }
}
