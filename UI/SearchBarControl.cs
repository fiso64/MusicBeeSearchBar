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
    public partial class SearchBarControl : UserControl
    {
        public event EventHandler CloseRequested;
        public event EventHandler<int> HeightChanged;

        // Core Services & Interfaces
        private MusicBeeApiInterface mbApi;
        private Control musicBeeControl;
        private SynchronizationContext musicBeeContext;
        private SearchService searchService;
        private ImageService imageService;
        private Func<string, SearchResult, KeyEventArgs, Task<bool>> resultAcceptAction;

        // UI Controls
        private TextBox searchBox;
        private CustomResultList resultsListBox;
        private PictureBox loadingIndicator;
        private Panel spacerPanel;

        // Configuration
        private readonly SearchUIConfig searchUIConfig;
        private readonly int iconSize;
        private readonly bool _closeOnAccept;

        // State
        private bool isLoading = true;
        private CancellationTokenSource _currentSearchCts;
        private long _currentSearchSequence = 0;
        private int preservedIndex = -1; // Used for Ctrl+Enter workaround
        private bool _isImageLoading = false;

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

        public void FocusSearchBox()
        {
            if (searchBox != null && !IsDisposed)
            {
                searchBox.Focus();
                searchBox.SelectAll();
            }
        }

        public SearchBarControl(
            Control musicBeeControl,
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            Func<string, SearchResult, KeyEventArgs, Task<bool>> resultAcceptAction,
            SearchUIConfig searchUIConfig,
            bool closeOnAccept,
            string defaultText = null
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
            this.resultAcceptAction = resultAcceptAction;
            this.searchUIConfig = searchUIConfig;
            this._closeOnAccept = closeOnAccept;
            searchService = new SearchService(musicBeeApi, searchUIConfig);
            if (searchUIConfig.ShowImages)
            {
                int scaledCornerRadius = (int)(8 * dpiScale); // 8 is from CustomResultList.ARTWORK_CORNER_RADIUS
                imageService = new ImageService(musicBeeApi, searchService, searchUIConfig, defaultImageSize, scaledCornerRadius);
                InitializeImageLoadingTimer();
            }

            InitializeUI(dpiScale);
            InitializeHotkeys();

            // Start loading tracks asynchronously
            LoadTracksAsync();

            if (!string.IsNullOrEmpty(defaultText))
            {
                searchBox.Text = defaultText;
                searchBox.Select(searchBox.Text.Length, 0);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Debug.WriteLine("[SearchBarControl] OnLoad: Firing. Setting focus and calling SearchBoxSetDefaultResults.");
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
                topMatchResultFont?.Dispose();
                topMatchResultDetailFont?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
