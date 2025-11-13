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
        private Func<string, SearchResult, KeyEventArgs, bool> resultAcceptAction;

        // UI Controls
        private TextBox searchBox;
        private CustomResultList resultsListBox;
        private OverlayForm overlay;
        private PictureBox loadingIndicator;

        // Configuration
        private readonly SearchUIConfig searchUIConfig;
        private readonly int imageSize;
        private readonly int iconSize;

        // State
        private bool isLoading = true;
        private CancellationTokenSource _currentSearchCts;
        private long _currentSearchSequence = 0;
        private int preservedIndex = -1; // Used for Ctrl+Enter workaround

        // Timers
        private System.Windows.Forms.Timer imageLoadDebounceTimer;

        // Icons (cached)
        private Image songIcon;
        private Image albumIcon;
        private Image artistIcon;
        private Image playlistIcon;
        private Image commandIcon; // New icon for commands

        // Fonts
        private readonly Font searchBoxFont = new Font("Arial", 14, FontStyle.Bold);
        private readonly Font resultFont = new Font("Arial", 11, FontStyle.Bold);
        private readonly Font resultDetailFont = new Font("Arial", 10, FontStyle.Regular);

        // Constants
        private const int IMAGE_DEBOUNCE_MS = 100;
        private const bool INCREMENTAL_UPDATE = false;

        public SearchBar(
            Control musicBeeControl,
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            Func<string, SearchResult, KeyEventArgs, bool> resultAcceptAction,
            SearchUIConfig searchUIConfig,
            string defaultText = null
        )
        {
            this.DoubleBuffered = true; // Enable double buffering for flicker-free drawing

            imageSize = Math.Max(searchUIConfig.ResultItemHeight - 12, 5);
            iconSize = Math.Max(searchUIConfig.ResultItemHeight - 30, 5);

            this.musicBeeControl = musicBeeControl;
            this.musicBeeContext = musicBeeContext;
            mbApi = musicBeeApi;
            this.resultAcceptAction = resultAcceptAction;
            this.searchUIConfig = searchUIConfig;
            searchService = new SearchService(musicBeeApi, searchUIConfig);
            if (searchUIConfig.ShowImages)
            {
                imageService = new ImageService(musicBeeApi, searchService, imageSize);
                InitializeImageLoadingTimer();
            }

            InitializeUI();
            InitializeHotkeys();

            // Start loading tracks asynchronously
            LoadTracksAsync();

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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw the main border
            using (var pen = new Pen(Color.Gray, 1))
            using (var path = GetRoundedRectPath(new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1), CORNER_RADIUS))
            {
                e.Graphics.DrawPath(pen, path);
            }
            
            // Draw the separator line if the results are visible
            if (resultsListBox.Visible)
            {
                using (var pen = new Pen(Color.Gray, 1))
                {
                    int lineY = searchBox.Parent.Bottom;
                    e.Graphics.DrawLine(pen, 0, lineY, Width, lineY);
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Debug.WriteLine("[SearchBar] OnShown: Firing. Setting focus and calling SearchBoxSetDefaultResults.");
            searchBox.Focus();
            SearchBoxSetDefaultResults();
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
                commandIcon?.Dispose(); // Dispose the new command icon
                loadingIndicator?.Image?.Dispose();
                loadingIndicator?.Dispose();
                searchBoxFont?.Dispose();
                resultFont?.Dispose();
                resultDetailFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
