using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public partial class SearchBarForm : Form
    {
        private readonly SearchBarControl searchBarControl;
        private readonly SearchUIConfig searchUIConfig;
        private readonly MusicBeeApiInterface mbApi;
        private readonly Control musicBeeControl;
        private readonly SynchronizationContext musicBeeContext;
        private readonly Theme theme;

        private OverlayForm overlay;
        private Panel dragPanel;

        private bool isDragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        public SearchBarForm(
            Control musicBeeControl,
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            Func<string, SearchResult, KeyEventArgs, Task<bool>> resultAcceptAction,
            SearchUIConfig searchUIConfig,
            string defaultText = null)
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;

            this.searchUIConfig = searchUIConfig;
            this.mbApi = musicBeeApi;
            this.musicBeeControl = musicBeeControl;
            this.musicBeeContext = musicBeeContext;
            this.theme = new Theme(searchUIConfig);

            InitializeComponent();
            
            searchBarControl = new SearchBarControl(musicBeeControl, musicBeeContext, mbApi, resultAcceptAction, searchUIConfig, true, defaultText);
            searchBarControl.Dock = DockStyle.Fill;
            searchBarControl.HeightChanged += (s, h) => { if (!IsDisposed) this.Height = h; };
            searchBarControl.CloseRequested += (s, e) => { if (!IsDisposed) this.Close(); };
            
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(8)
            };
            mainPanel.Controls.Add(searchBarControl);
            mainPanel.Controls.Add(dragPanel);
            
            this.Controls.Add(mainPanel);

            Deactivate += (s, e) => { if (!IsDisposed) this.Close(); };
            KeyPreview = true;
            KeyDown += (s, e) => {
                if (e.Alt && e.KeyCode == Keys.D)
                {
                    searchBarControl.FocusSearchBox();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            
            UpdateOverlayState(forInitialCreation: true);
            FormClosed += (s, e) => {
                 if (overlay != null && !overlay.IsDisposed) {
                     musicBeeContext.Post(__ => {
                         if (overlay != null && !overlay.IsDisposed) {
                             overlay.Close();
                         }
                     }, null);
                 }
            };
        }
        
        private void InitializeComponent()
        {
            Size = searchUIConfig.InitialSize;
            BackColor = searchUIConfig.BaseColor;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;

            ResetPosition();

            dragPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 12,
                BackColor = Color.Transparent,
                Cursor = Cursors.SizeAll,
                Visible = true,
            };
            dragPanel.MouseDown += (s, e) => { isDragging = true; dragCursorPoint = Cursor.Position; dragFormPoint = Location; };
            dragPanel.MouseMove += (s, e) => { if (isDragging) { Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint)); Location = Point.Add(dragFormPoint, new Size(dif)); } };
            dragPanel.MouseUp += (s, e) => { isDragging = false; };
        }

        public void SetSearchText(string text)
        {
            searchBarControl.SetSearchText(text);
        }

        private void UpdateOverlayState(bool forInitialCreation = false)
        {
            bool shouldShowOverlay = searchUIConfig.OverlayOpacity > 0;

            if (forInitialCreation && !WinApiHelpers.IsWindowFocused(mbApi.MB_GetWindowHandle()))
            {
                shouldShowOverlay = false;
            }

            if (shouldShowOverlay)
            {
                if (overlay == null || overlay.IsDisposed)
                {
                    musicBeeContext.Post(_ =>
                    {
                        if (musicBeeControl != null && musicBeeControl.IsHandleCreated && (overlay == null || overlay.IsDisposed))
                        {
                            overlay = new OverlayForm(musicBeeControl, searchUIConfig.OverlayOpacity, 0.08);
                            overlay.Show();
                        }
                    }, null);
                }
            }
            else
            {
                if (overlay != null)
                {
                    var overlayToClose = overlay;
                    overlay = null; 
                    
                    musicBeeContext.Post(__ =>
                    {
                        if (overlayToClose != null && !overlayToClose.IsDisposed)
                        {
                            overlayToClose.Close();
                        }
                    }, null);
                }
            }
        }
        
        private void ResetPosition()
        {
            var mbHandle = mbApi.MB_GetWindowHandle();
            bool minimized = WinApiHelpers.WinGetMinMax(mbHandle) == WinApiHelpers.WindowState.Minimized;

            if (minimized)
            {
                Location = new Point(
                    (Screen.PrimaryScreen.Bounds.Width - Size.Width) / 2,
                    Screen.PrimaryScreen.Bounds.Height / 4 - 50
                );
            }
            else
            {
                if (musicBeeControl != null)
                {
                    var mbBounds = musicBeeControl.Bounds;
                    Location = new Point(
                        mbBounds.Left + (mbBounds.Width - Size.Width) / 2,
                        mbBounds.Top + 100
                    );
                }
                else
                {
                    Location = new Point(
                       (Screen.PrimaryScreen.WorkingArea.Width - Size.Width) / 2,
                       (Screen.PrimaryScreen.WorkingArea.Height - Size.Height) / 2
                   );
                }
            }
        }

        private const int CORNER_RADIUS = 10;

        private GraphicsPath GetRoundedRectPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, radius, radius, 180, 90);
            path.AddArc(bounds.Right - radius, bounds.Y, radius, radius, 270, 90);
            path.AddArc(bounds.Right - radius, bounds.Bottom - radius, radius, radius, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - radius, radius, radius, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.ClientRectangle.Width > 0 && this.ClientRectangle.Height > 0)
            {
                using (var path = GetRoundedRectPath(this.ClientRectangle, CORNER_RADIUS))
                {
                    this.Region = new Region(path);
                }
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // WS_EX_COMPOSITED
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(theme.Border, 1))
            using (var path = GetRoundedRectPath(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), CORNER_RADIUS))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }
    }
}