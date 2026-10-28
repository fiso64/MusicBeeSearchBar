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
        private SearchToolbar searchToolbar;
        private Panel dragPanel;
        private Panel resizeGripLeft;
        private Panel resizeGripRight;

        // Configuration
        private readonly SearchUIConfig searchUIConfig;
        private readonly Theme theme;
        private readonly int iconSize;
        private readonly float dpiScale;

        // State
        private bool isLoading = true;
        private CancellationTokenSource _currentSearchCts;
        private long _currentSearchSequence = 0;
        private int preservedIndex = -1; // Used for Ctrl+Enter workaround
        private bool _isImageLoading = false;
        private bool isDetached = false;
        private readonly bool isEmbedded;
        private readonly bool compactPanel;
        internal bool IsExpandedEmbedded => isEmbedded && !compactPanel;
        private bool UsesListSelection => IsExpandedEmbedded || (!isEmbedded && isDetached);
        private readonly bool squareCorners;
        private Form resultsPopup;
        private bool isDragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;
        private bool _suppressDeactivate = false;
        private int _attachmentVersion;
        private volatile bool _resourcesDisposed = false;
        private bool _closing;
        private bool _seenMusicBeeForeground;

        // Manual width-resize state (the form is borderless, so there's no native resize border)
        private bool _isResizingWidth = false;
        private bool _resizeFromLeft = false;
        private Point _resizeStartCursorPos;
        private int _resizeStartWidth;
        private int _resizeStartLeft;
        private int _resizeMaxWidth;
        private const int MIN_WIDTH_UNSCALED = 300;

        // Timer to check if MusicBee window is still open in detached mode
        private System.Windows.Forms.Timer _mbWindowCheckTimer;
        private System.Windows.Forms.Timer _foregroundCheckTimer;

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
                            var newOverlay = overlay;
                            newOverlay.MouseClick += (sender, e) =>
                            {
                                if (!isDetached && !_closing && !_resourcesDisposed && overlay == newOverlay)
                                    Close();
                            };
                            overlay.Activated += (sender, e) => RestoreFocusFromOverlay(newOverlay);
                            overlay.Show();
                            overlay.AddOwnedForm(this);
                            // Ensure SearchBar stays on top of the overlay and keeps focus
                            this.BringToFront();
                            this.Activate();
                            RestoreFocusFromOverlay(newOverlay);
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
                CloseOverlay();
            }
        }

        private void RestoreFocusFromOverlay(OverlayForm expectedOverlay)
        {
            int attachmentVersion = _attachmentVersion;
            PostToUi(() =>
            {
                if (_closing || isDetached || attachmentVersion != _attachmentVersion || overlay != expectedOverlay
                    || expectedOverlay.IsDisposed || !expectedOverlay.IsHandleCreated
                    || WinApiHelpers.GetForegroundWindow() != expectedOverlay.Handle)
                    return;

                Activate();
                searchBox.Focus();
            });
        }

        private void PostToUi(Action action)
        {
            if (_resourcesDisposed || IsDisposed || Disposing || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (!_resourcesDisposed && !IsDisposed && !Disposing) action();
                }));
            }
            catch (InvalidOperationException)
            {
                // The handle can be destroyed between the check and BeginInvoke.
            }
        }

        private void CloseOverlay()
        {
            var currentOverlay = overlay;
            overlay = null;
            if (currentOverlay == null || currentOverlay.IsDisposed) return;

            // The backdrop owns this form for stacking. Unlink it before closing
            // the backdrop so it cannot re-enter the search bar's close/dispose path.
            currentOverlay.RemoveOwnedForm(this);
            currentOverlay.Close();
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
        private const int VISIBLE_ITEMS_BUFFER = 2; // Buffer for loading images for partially visible items


        public void SetSearchText(string text)
        {
            if (searchBox == null || IsDisposed) return;

            searchBox.Text = text;
            searchBox.Select(text.Length, 0);
            searchBox.Focus();
        }

        internal bool FocusEmbeddedSearch()
        {
            return isEmbedded && !IsDisposed && !Disposing && searchBox != null
                && !searchBox.IsDisposed && searchBox.CanFocus && searchBox.Focus();
        }

        private void ToggleDetachedMode()
        {
            _attachmentVersion++;
            isDetached = !isDetached;
            searchToolbar?.SetDetached(isDetached);
            resultsListBox.DoubleClickToActivate = UsesListSelection;
            resultsListBox.SelectOnlyOnClickWithMultipleSelection = !isEmbedded && !isDetached;
            dragPanel.Visible = isDetached;
            Height += isDetached ? dragPanel.Height : -dragPanel.Height;
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
            if (IsHandleCreated)
            {
                bool wasSuppressingDeactivate = _suppressDeactivate;
                _suppressDeactivate = true;
                try { UpdateStyles(); }
                finally { _suppressDeactivate = wasSuppressingDeactivate; }
            }
            UpdateForegroundTracking();
        }

        private void UpdateForegroundTracking()
        {
            if (_foregroundCheckTimer == null) return;
            _foregroundCheckTimer.Stop();
            _seenMusicBeeForeground = false;
            if (Visible && !isDetached)
            {
                CheckForegroundWindow();
                if (!IsDisposed && !Disposing) _foregroundCheckTimer.Start();
            }
        }

        private void CheckForegroundWindow()
        {
            if (_closing || _resourcesDisposed || IsDisposed || Disposing || isDetached
                || _suppressDeactivate || !IsHandleCreated)
                return;

            var foreground = WinApiHelpers.GetForegroundWindow();
            if (foreground == IntPtr.Zero) return;
            var sameProcess = WinApiHelpers.AreWindowsInSameProcess(foreground, Handle);
            if (sameProcess == true)
                _seenMusicBeeForeground = true;
            else if (sameProcess == false && _seenMusicBeeForeground)
                Close();
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
            bool startDetached = false,
            bool embedded = false
        )
        {
            isEmbedded = embedded;
            squareCorners = searchUIConfig.DisableRoundedCorners && (!searchUIConfig.SquareCornersDockedOnly || embedded);
            compactPanel = embedded && searchUIConfig.DockedPanelLayout == DockedSearchLayout.Compact;
            if (embedded)
            {
                TopLevel = false;
                Region = null;
            }
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true; // Enable double buffering for flicker-free drawing

            float dpiScale;
            using (var g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96.0f;
            }
            this.dpiScale = dpiScale;

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
            this.theme = new Theme(searchUIConfig, isEmbedded);

            searchService = new SearchService(musicBeeApi, searchUIConfig);
            if (searchUIConfig.ShowImages)
            {
                int scaledCornerRadius = squareCorners ? 0 : (int)(8 * dpiScale);
                imageService = new ImageService(musicBeeApi, searchService, searchUIConfig, defaultImageSize, scaledCornerRadius);
                InitializeImageLoadingTimer();
            }

            _mbWindowCheckTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };
            _mbWindowCheckTimer.Tick += MbWindowCheckTimer_Tick;
            _foregroundCheckTimer = new System.Windows.Forms.Timer { Interval = 200 };
            _foregroundCheckTimer.Tick += (s, e) => CheckForegroundWindow();

            InitializeUI(dpiScale);
            if (!isEmbedded) InitializeResizeGrips(dpiScale);
            InitializeHotkeys();

            // Start loading tracks asynchronously
            LoadTracksAsync();

            if (startDetached && !isEmbedded)
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
            if (squareCorners || radius <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
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

            // Rebuilding the rounded-corner region calls into SetWindowRgn, which forces a
            // full recomposite of this WS_EX_COMPOSITED window - expensive enough that doing
            // it on every pixel of an interactive width drag is what makes growing lag behind
            // the cursor. Skip it while dragging (square corners for the moment) and restore
            // the real shape once in ResizeGrip_MouseUp.
            if (_isResizingWidth || isEmbedded) return;

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
                if (isEmbedded) return cp;
                cp.ExStyle |= 0x02000000;  // WS_EX_COMPOSITED
                if (isDetached)
                {
                    cp.ExStyle &= ~0x00000080; // WS_EX_TOOLWINDOW
                    cp.ExStyle |= 0x00040000;  // WS_EX_APPWINDOW
                }
                else
                {
                    cp.ExStyle &= ~0x00040000; // WS_EX_APPWINDOW
                    cp.ExStyle |= 0x00000080;  // WS_EX_TOOLWINDOW
                }
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color borderColor = theme.Border;

            // Draw the main form border only for the floating window.
            if (!isEmbedded)
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
            if (!isEmbedded)
            {
                UpdateOverlayState();
                UpdateForegroundTracking();
            }
            Debug.WriteLine("[SearchBar] OnShown: Firing. Setting focus and calling SearchBoxSetDefaultResults.");
            if (!isEmbedded) searchBox.Focus();
            if (!(isEmbedded && !compactPanel && _restoredScrollTop.HasValue
                && string.IsNullOrWhiteSpace(searchBox.Text)))
                SearchBoxSetDefaultResults();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _closing = true;
            if (!e.Cancel)
            {
                // Hide the form immediately to prevent visual artifacts (flashing unstyled window) during disposal
                this.Visible = false;
            }
            base.OnFormClosing(e);
            if (e.Cancel) _closing = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_resourcesDisposed)
            {
                _resourcesDisposed = true;
                _mbWindowCheckTimer?.Stop();
                _mbWindowCheckTimer?.Dispose();
                _foregroundCheckTimer?.Stop();
                _foregroundCheckTimer?.Dispose();
                imageLoadDebounceTimer?.Stop();
                imageLoadDebounceTimer?.Dispose();
                _selectionRefreshTimer?.Stop();
                _selectionRefreshTimer?.Dispose();
                _currentSearchCts?.Cancel();
                _currentSearchCts?.Dispose();

                resultsPopup?.Dispose();

                CloseOverlay();

                base.Dispose(disposing);

                imageService?.Dispose();
                
                songIcon?.Dispose();
                albumIcon?.Dispose();
                artistIcon?.Dispose();
                playlistIcon?.Dispose();
                commandIcon?.Dispose(); 

                loadingIndicator?.Image?.Dispose();
                
                searchBoxFont?.Dispose();
                resultFont?.Dispose();
                resultDetailFont?.Dispose();
                topMatchResultFont?.Dispose();
                topMatchResultDetailFont?.Dispose();
            }
            else
            {
                base.Dispose(disposing);
            }
        }
    }
}
