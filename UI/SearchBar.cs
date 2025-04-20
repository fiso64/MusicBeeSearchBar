using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;


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
        private ListBox resultsListBox;
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
