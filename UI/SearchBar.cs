using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;
using System.Drawing.Drawing2D;


namespace MusicBeePlugin.UI
{
    public partial class SearchBar : Form
    {
        // Core Services & Interfaces
        private MusicBeeApiInterface mbApi;
        private Control musicBeeControl;
        private SynchronizationContext musicBeeContext;
        private SearchService searchService;
        private ImageService imageService;
        private ActionService actionService;

        // UI Controls
        private TextBox searchBox;
        private CustomResultList resultsListBox;
        private OverlayForm overlay;
        private PictureBox loadingIndicator;
        private Panel spacerPanel;
        private Panel dragPanel;

        // Configuration
        private readonly SearchUIConfig searchUIConfig;
        private readonly Theme theme;
        private readonly int iconSize;

        // State
        private bool isLoading = true;
        private CancellationTokenSource _currentSearchCts;
        private long _currentSearchSequence = 0;
        private int preservedIndex = -1; // Used for Ctrl+Enter workaround
        private bool _isImageLoading = false;
        private bool isDetached = false;
        private bool isDragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private bool _suppressDeactivate = false;

        // Timer to check if MusicBee window is still open in detached mode
        private System.Windows.Forms.Timer _mbWindowCheckTimer;

        private void UpdateOverlayState()
        {
            bool shouldShowOverlay = !isDetached && searchUIConfig.OverlayOpacity > 0;

            if (shouldShowOverlay)
            {
                if (overlay == null || overlay.IsDisposed)
                {
                    if (musicBeeControl != null && musicBeeControl.IsHandleCreated)
                    {
                        _suppressDeactivate = true;
                        try
                        {
                            overlay = new OverlayForm(musicBeeControl, searchUIConfig.OverlayOpacity, 0.08);
                            overlay.Show();
                            // Ensure SearchBar stays on top of the overlay and keeps focus
                            this.BringToFront();
                            this.Activate();
                        }
                        finally
                        {
                            _suppressDeactivate = false;
                        }
                    }
                }
            }
            else // Should NOT show overlay
            {
                if (overlay != null)
                {
                    if (!overlay.IsDisposed)
                    {
                        overlay.Close();
                    }
                    overlay = null;
                }
            }
        }

        // Timers
        private System.Windows.Forms.Timer imageLoadDebounceTimer;

        // Icons (cached)
        private Image songIcon;
        private Image albumIcon;
        private Image artistIcon;
        private Image playlistIcon;
        private Image commandIcon; // New icon for commands

        // Fonts
        private Font searchBoxFont;
        private Font resultFont;
        private Font resultDetailFont;
        private Font topMatchResultFont;
        private Font topMatchResultDetailFont;

        // Scaled Metrics
        private readonly int searchBoxHeight;

        // Constants
        private const int IMAGE_DEBOUNCE_MS = 10;
        private const bool INCREMENTAL_UPDATE = false;
        private const int VISIBLE_ITEMS_BUFFER = 2; // Buffer for loading images for partially visible items


        public void SetSearchText(string text)
        {
            if (searchBox == null || IsDisposed) return;

            searchBox.Text = text;
            searchBox.Select(text.Length, 0);
            searchBox.Focus();
        }

        private void ToggleDetachedMode()
        {
            isDetached = !isDetached;
            dragPanel.Visible = isDetached;
            TopMost = !isDetached;

            if (isDetached)
            {
                Deactivate -= HandleFormDeactivate;
                _mbWindowCheckTimer.Start();
            }
            else // Re-attaching
            {
                Deactivate += HandleFormDeactivate;
                ResetPosition();
                _mbWindowCheckTimer.Stop();
            }

            UpdateOverlayState();
        }

        private void MbWindowCheckTimer_Tick(object sender, EventArgs e)
        {
            // This is only active in detached mode.
            // Using IsWindow is more reliable than checking for IntPtr.Zero.
            if (!Utils.WinApiHelpers.IsWindow(mbApi.MB_GetWindowHandle()))
            {
                if (!IsDisposed)
                {
                    _mbWindowCheckTimer.Stop();
                    Close();
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

        public SearchBar(
            Control musicBeeControl,
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            ActionService actionService,
            SearchUIConfig searchUIConfig,
            string defaultText = null,
            bool startDetached = false
        )
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true; // Enable double buffering for flicker-free drawing

            float dpiScale;
            using (var g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96.0f;
            }

            // --- Scale UI metrics based on DPI ---
            searchBoxHeight = (int)(34 * dpiScale);

            // Set base font sizes. AutoScaleMode.Dpi will handle the scaling.
            this.searchBoxFont = new Font("Arial", 12f, FontStyle.Bold);
            this.resultFont = new Font("Arial", 11f, FontStyle.Bold);
            this.resultDetailFont = new Font("Arial", 10f, FontStyle.Regular);
            this.topMatchResultFont = new Font(this.resultFont.FontFamily, this.resultFont.Size * 1.4f, FontStyle.Bold);
            this.topMatchResultDetailFont = new Font(this.resultDetailFont.FontFamily, this.resultDetailFont.Size * 1.2f, FontStyle.Regular);

            // The item height from config is for 96 DPI, scale it for the current DPI.
            int scaledItemHeight = (int)(searchUIConfig.ResultItemHeight * dpiScale);
            int defaultImageSize = Math.Max(scaledItemHeight - (int)(12 * dpiScale), 5); // For ImageService optimization check
            iconSize = Math.Max(scaledItemHeight - (int)(20 * dpiScale), 5);

            this.musicBeeControl = musicBeeControl;
            this.musicBeeContext = musicBeeContext;
            mbApi = musicBeeApi;
            this.actionService = actionService;
            this.searchUIConfig = searchUIConfig;
            this.theme = new Theme(searchUIConfig);

            searchService = new SearchService(musicBeeApi, searchUIConfig);
            if (searchUIConfig.ShowImages)
            {
                int scaledCornerRadius = (int)(8 * dpiScale); // 8 is from CustomResultList.ARTWORK_CORNER_RADIUS
                imageService = new ImageService(musicBeeApi, searchService, searchUIConfig, defaultImageSize, scaledCornerRadius);
                InitializeImageLoadingTimer();
            }

            _mbWindowCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };
            _mbWindowCheckTimer.Tick += MbWindowCheckTimer_Tick;

            InitializeUI(dpiScale);
            InitializeHotkeys();

            // Start loading tracks asynchronously
            LoadTracksAsync();

            if (startDetached)
            {
                ToggleDetachedMode();
            }

            if (!string.IsNullOrEmpty(defaultText))
            {
                searchBox.Text = defaultText;
                searchBox.Select(searchBox.Text.Length, 0);
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
            Color borderColor = theme.Border;

            // Draw the main form border
            using (var pen = new Pen(borderColor, 1))
            using (var path = GetRoundedRectPath(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), CORNER_RADIUS))
            {
                e.Graphics.DrawPath(pen, path);
            }

            // Draw border for the search box
            if (searchBox != null && searchBox.Parent != null)
            {
                var searchContainer = searchBox.Parent;
                var rect = searchContainer.Bounds;
                // The container already has padding, so we draw the border around it.
                using (var pen = new Pen(borderColor, 1))
                using (var path = GetRoundedRectPath(new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1), 8))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateOverlayState();
            Debug.WriteLine("[SearchBar] OnShown: Firing. Setting focus and calling SearchBoxSetDefaultResults.");
            searchBox.Focus();
            SearchBoxSetDefaultResults();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!e.Cancel)
            {
                // Hide the form immediately to prevent visual artifacts (flashing unstyled window) during disposal
                this.Visible = false;
            }
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _mbWindowCheckTimer?.Dispose();
                imageService?.Dispose();
                imageLoadDebounceTimer?.Dispose();
                _currentSearchCts?.Dispose();
                songIcon?.Dispose();
                albumIcon?.Dispose();
                artistIcon?.Dispose();
                playlistIcon?.Dispose();
                commandIcon?.Dispose(); // Dispose the new command icon
                loadingIndicator?.Image?.Dispose();
                loadingIndicator?.Dispose();
                searchBoxFont?.Dispose();
                resultFont?.Dispose();
                resultDetailFont?.Dispose();
                topMatchResultFont?.Dispose();
                topMatchResultDetailFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
