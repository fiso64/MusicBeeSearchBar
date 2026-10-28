using System;
using System.Drawing;
using System.Windows.Forms;
using MusicBeePlugin.Utils;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
        private sealed class PopupWheelFilter : IMessageFilter
        {
            private readonly SearchBar owner;

            public PopupWheelFilter(SearchBar owner) { this.owner = owner; }

            public bool PreFilterMessage(ref Message message)
            {
                const int WM_MOUSEWHEEL = 0x020A;
                var popup = owner.resultsPopup;
                var list = owner.resultsListBox;
                var musicBeeApi = owner.mbApi;
                if (message.Msg != WM_MOUSEWHEEL || owner.IsDisposed || owner.Disposing
                    || popup == null || popup.IsDisposed || !popup.Visible
                    || list == null || list.IsDisposed || !list.IsHandleCreated || !list.Visible
                    || message.HWnd == list.Handle
                    || !WinApiHelpers.IsWindowFocused(musicBeeApi.MB_GetWindowHandle()))
                    return false;

                long coordinates = message.LParam.ToInt64();
                var screenPoint = new Point(unchecked((short)(coordinates & 0xffff)),
                    unchecked((short)((coordinates >> 16) & 0xffff)));
                if (!list.RectangleToScreen(list.ClientRectangle).Contains(screenPoint)) return false;

                int delta = unchecked((short)((message.WParam.ToInt64() >> 16) & 0xffff));
                list.ScrollByWheel(delta);
                return true;
            }
        }

        private sealed class ResultsPopup : Form
        {
            protected override bool ShowWithoutActivation => true;
            protected override CreateParams CreateParams
            {
                get
                {
                    var cp = base.CreateParams;
                    cp.ExStyle |= 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                    return cp;
                }
            }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x21) { m.Result = (IntPtr)3; return; } // MA_NOACTIVATE
                base.WndProc(ref m);
            }
        }

        private void InitializeResultsPopup()
        {
            resultsPopup = new ResultsPopup
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = searchUIConfig.BaseColor
            };
            resultsListBox.Parent.Controls.Remove(resultsListBox);
            resultsPopup.Controls.Add(resultsListBox);
            resultsListBox.Dock = DockStyle.Fill;
            if (searchUIConfig.ShowSearchToolbar)
            {
                searchToolbar.Parent.Controls.Remove(searchToolbar);
                resultsPopup.Controls.Add(searchToolbar);
                searchToolbar.Dock = DockStyle.Top;
            }
            spacerPanel.Visible = false;
            var wheelFilter = new PopupWheelFilter(this);
            Application.AddMessageFilter(wheelFilter);
            Disposed += (s, e) => Application.RemoveMessageFilter(wheelFilter);
            searchBox.Enter += (s, e) => UpdateResultsPopup();
            searchBox.Leave += (s, e) => PostToUi(() =>
            {
                if (!searchBox.Focused) resultsPopup?.Hide();
            });
            VisibleChanged += (s, e) => { if (!Visible) resultsPopup?.Hide(); };
            var positionTimer = new Timer { Interval = 100 };
            positionTimer.Tick += (s, e) =>
            {
                if (resultsPopup == null || resultsPopup.IsDisposed || !resultsPopup.Visible) return;
                if (!Visible || !searchBox.Focused) resultsPopup.Hide();
                else PositionResultsPopup();
            };
            positionTimer.Start();
            Disposed += (s, e) => positionTimer.Dispose();
        }

        private void UpdateResultsPopup()
        {
            if (resultsPopup == null || resultsPopup.IsDisposed || !IsHandleCreated) return;
            if (!Visible || !searchBox.Focused || (!searchUIConfig.ShowSearchToolbar && resultsListBox.Items.Count == 0))
            {
                resultsPopup.Hide();
                return;
            }
            resultsListBox.Visible = resultsListBox.Items.Count > 0;
            PositionResultsPopup();
            if (!resultsPopup.Visible)
            {
                var owner = musicBeeControl?.FindForm();
                if (owner != null && !owner.IsDisposed) resultsPopup.Show(owner);
                else resultsPopup.Show();
            }
        }

        private void PositionResultsPopup()
        {
            int height = searchUIConfig.ShowSearchToolbar ? searchToolbar.Height : 0, count = 0;
            for (int i = 0; i < resultsListBox.Items.Count && count < searchUIConfig.MaxResultsVisible; i++)
            {
                var item = resultsListBox.Items[i];
                height += resultsListBox.GetItemHeight(item, i);
                if (item.Type != Services.ResultType.Header) count++;
            }
            // MusicBee may retain the previous expanded panel height when changing
            // layouts. Anchor to the input, not the host panel's bottom.
            Control anchor = searchBox.Parent;
            var stripBottom = anchor.PointToScreen(new Point(0, anchor.Height));
            var below = new Point(PointToScreen(Point.Empty).X, stripBottom.Y);
            var area = Screen.FromControl(this).WorkingArea;
            height = Math.Max(1, Math.Min(height, area.Height));
            int y = below.Y + height <= area.Bottom ? below.Y : Math.Max(area.Top, anchor.PointToScreen(Point.Empty).Y - height);
            int width = Math.Min(Width, area.Width);
            int x = Math.Max(area.Left, Math.Min(below.X, area.Right - width));
            var bounds = new Rectangle(x, y, Math.Max(1, width), height);
            if (resultsPopup.Bounds != bounds) resultsPopup.Bounds = bounds;
        }
    }
}
