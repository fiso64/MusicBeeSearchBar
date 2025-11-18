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
    public partial class SearchControl : UserControl
    {
        // Core Services & Interfaces
        private MusicBeeApiInterface mbApi;
        private SynchronizationContext musicBeeContext;
        private SearchService searchService;
        private ImageService imageService;

        // UI Controls
        private TextBox searchBox;
        private CustomResultList resultsListBox;
        private PictureBox loadingIndicator;
        private Panel spacerPanel;

        // Configuration
        private readonly SearchUIConfig searchUIConfig;
        private readonly int iconSize;

        // State
        private bool isLoading = true;
        private CancellationTokenSource _currentSearchCts;
        private long _currentSearchSequence = 0;
        private int preservedIndex = -1; // Used for Ctrl+Enter workaround
        private bool _isImageLoading = false;

        public event EventHandler CloseRequested;
        public event Action<Size> ResizeRequested;
        public event Func<string, SearchResult, KeyEventArgs, Task> ResultAccepted;
        
        // Timers
        private System.Windows.Forms.Timer imageLoadDebounceTimer;

        // Icons (cached)
        private Image songIcon;
        private Image albumIcon;
        private Image artistIcon;
        private Image playlistIcon;
        private Image commandIcon;

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
        private const int VISIBLE_ITEMS_BUFFER = 2;


        public void SetSearchText(string text)
        {
            if (searchBox == null || IsDisposed) return;

            searchBox.Text = text;
            searchBox.Select(text.Length, 0);
            searchBox.Focus();
        }

        public void FocusSearchBox()
        {
            if (searchBox != null && !IsDisposed)
            {
                searchBox.Focus();
            }
        }

        public SearchControl(
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            SearchUIConfig searchUIConfig
        )
        {
            this.AutoScaleMode = AutoScaleMode.Dpi;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.DoubleBuffered = true;

            float dpiScale;
            using (var g = this.CreateGraphics())
            {
                dpiScale = g.DpiX / 96.0f;
            }

            // --- Scale UI metrics based on DPI ---
            searchBoxHeight = (int)(34 * dpiScale);

            this.searchBoxFont = new Font("Arial", 12f, FontStyle.Bold);
            this.resultFont = new Font("Arial", 11f, FontStyle.Bold);
            this.resultDetailFont = new Font("Arial", 10f, FontStyle.Regular);
            this.topMatchResultFont = new Font(this.resultFont.FontFamily, this.resultFont.Size * 1.4f, FontStyle.Bold);
            this.topMatchResultDetailFont = new Font(this.resultDetailFont.FontFamily, this.resultDetailFont.Size * 1.2f, FontStyle.Regular);

            int scaledItemHeight = (int)(searchUIConfig.ResultItemHeight * dpiScale);
            int defaultImageSize = Math.Max(scaledItemHeight - (int)(12 * dpiScale), 5);
            iconSize = Math.Max(scaledItemHeight - (int)(20 * dpiScale), 5);

            this.musicBeeContext = musicBeeContext;
            mbApi = musicBeeApi;
            this.searchUIConfig = searchUIConfig;
            searchService = new SearchService(musicBeeApi, searchUIConfig);
            if (searchUIConfig.ShowImages)
            {
                int scaledCornerRadius = (int)(8 * dpiScale);
                imageService = new ImageService(musicBeeApi, searchService, searchUIConfig, defaultImageSize, scaledCornerRadius);
                InitializeImageLoadingTimer();
            }

            InitializeUI(dpiScale);
            InitializeHotkeys();

            LoadTracksAsync();
        }

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

            // Don't draw a border for the main control in panel mode.

            // Draw border for the search box
            if (searchBox != null && searchBox.Parent != null)
            {
                var searchContainer = searchBox.Parent;
                var rect = searchContainer.Bounds;
                using (var pen = new Pen(Color.FromArgb(100, Color.Gray), 1))
                using (var path = GetRoundedRectPath(new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1), 8))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        private bool _dataLoadInitiated = false;
        private readonly object _dataLoadLock = new object();

        private void InitiateDataLoad()
        {
            // Use a lock to ensure the async task is only started once.
            if (_dataLoadInitiated) return;
            lock (_dataLoadLock)
            {
                if (_dataLoadInitiated) return;

                Debug.WriteLine("[SearchControl] Initiating data load...");
                isLoading = true;
                Task.Run(LoadTracksAsync); // Fire-and-forget background load.
                _dataLoadInitiated = true;
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !IsDisposed && !DesignMode)
            {
                // This is a safer place for initialization logic than OnLoad.
                // It ensures the control is actually visible before we do anything.
                InitiateDataLoad();
                
                if (CanFocus)
                {
                    searchBox?.Focus();
                }
                SearchBoxSetDefaultResults();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                imageService?.Dispose();
                imageLoadDebounceTimer?.Dispose();
                _currentSearchCts?.Dispose();
                songIcon?.Dispose();
                albumIcon?.Dispose();
                artistIcon?.Dispose();
                playlistIcon?.Dispose();
                commandIcon?.Dispose();
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