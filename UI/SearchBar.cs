using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static MusicBeePlugin.Plugin;


namespace MusicBeePlugin.UI
{
    public class SearchBar : Form
    {
        private MusicBeeApiInterface mbApi;
        private Control musicBeeControl;
        private SynchronizationContext musicBeeContext;
        private SearchService searchService;
        private ImageService imageService;
        private System.Windows.Forms.Timer imageLoadDebounceTimer;
        private const int IMAGE_DEBOUNCE_MS = 100;
        private const int ITEM_HEIGHT = 42;
        private const int IMAGE_SIZE = 32;
        private const int ICON_SIZE = 20;

        private Func<string, SearchResult, KeyEventArgs, bool> resultAcceptAction;

        private TextBox searchBox;
        private ListBox resultsListBox;
        private Image songIcon;
        private Image albumIcon;
        private Image artistIcon;
        private Image playlistIcon;
        private OverlayForm overlay;

        private const bool INCREMENTAL_UPDATE = false;
        private bool isLoading = true;
        private SearchUIConfig searchUIConfig;
        private PictureBox loadingIndicator;

        private CancellationTokenSource _currentSearchCts;
        private long _currentSearchSequence = 0;

        public SearchBar(
            Control musicBeeControl,
            SynchronizationContext musicBeeContext,
            MusicBeeApiInterface musicBeeApi,
            Func<string, SearchResult, KeyEventArgs, bool> resultAcceptAction,
            SearchUIConfig searchUIConfig,
            string defaultText = null
        )
        {
            this.musicBeeControl = musicBeeControl;
            this.musicBeeContext = musicBeeContext;
            mbApi = musicBeeApi;
            this.resultAcceptAction = resultAcceptAction;
            this.searchUIConfig = searchUIConfig;
            searchService = new SearchService(musicBeeApi, searchUIConfig);
            if (searchUIConfig.ShowImages)
            {
                imageService = new ImageService(musicBeeApi, searchService, IMAGE_SIZE);
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

        private void InitializeUI()
        {
            var mbHandle = mbApi.MB_GetWindowHandle();

            if (searchUIConfig.OverlayOpacity > 0 && WinApiHelpers.IsWindowFocused(mbHandle))
            {
                musicBeeContext.Post(_ =>
                {
                    overlay = new OverlayForm(musicBeeControl, searchUIConfig.OverlayOpacity, 0.08);
                    overlay.Show();
                }, null);

                FormClosed += (s, e) =>
                {
                    musicBeeContext.Post(_ =>
                    {
                        if (overlay != null && !overlay.IsDisposed)
                            overlay.Close();
                    }, null);
                };
            }

            int lineSize = 3;
            songIcon = CreateIcon(Color.DarkGray, ICON_SIZE, ICON_SIZE, ResultType.Song, lineSize);
            albumIcon = CreateIcon(Color.DarkGray, ICON_SIZE, ICON_SIZE, ResultType.Album, lineSize - 1);
            artistIcon = CreateIcon(Color.Gray, ICON_SIZE, ICON_SIZE, ResultType.Artist, lineSize);
            playlistIcon = CreateIcon(Color.DarkGray, ICON_SIZE, ICON_SIZE, ResultType.Playlist, lineSize);

            Size = searchUIConfig.InitialSize;
            BackColor = searchUIConfig.BaseColor;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;

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
                var mbBounds = musicBeeControl.Bounds;
                Location = new Point(
                    mbBounds.Left + (mbBounds.Width - Size.Width) / 2,
                    mbBounds.Top + 100
                );
            }


            Panel mainPanel = new Panel { Dock = DockStyle.Fill, BackColor = searchUIConfig.BaseColor };

            Panel searchBoxContainer = new Panel
            {
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                BackColor = searchUIConfig.BaseColor,
                Height = 42,
                BorderStyle = BorderStyle.FixedSingle,
            };

            searchBox = new TextBox
            {
                Font = new Font("Arial", 14),
                ForeColor = searchUIConfig.TextColor,
                BackColor = searchUIConfig.BaseColor,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                TextAlign = HorizontalAlignment.Left,
                ShortcutsEnabled = true,
                AutoCompleteMode = AutoCompleteMode.Suggest, // this is only needed to fix ctrl+backspace
                AutoCompleteSource = AutoCompleteSource.CustomSource,
            };
            searchBox.TextChanged += SearchBox_TextChanged;
            searchBoxContainer.Controls.Add(searchBox);
            searchBox.TabStop = true; // Ensure searchBox can be focused

            resultsListBox = new ListBox
            {
                Dock = DockStyle.Top, // Dock to top and adjust height dynamically
                BackColor = searchUIConfig.BaseColor,
                ForeColor = searchUIConfig.TextColor,
                BorderStyle = BorderStyle.None,
                Font = new Font("Arial", 12),
                ItemHeight = (int)(ITEM_HEIGHT * (CreateGraphics().DpiX / 96.0)), // Scale based on DPI
                Visible = false,
                TabStop = false, // To prevent focusing on listbox with tab key.
                Height = 0 // Initially set height to 0
            };

            resultsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            resultsListBox.DrawItem += ResultsListBox_DrawItem;
            resultsListBox.Click += ResultsListBox_Click;
            resultsListBox.TabStop = false; // To prevent focusing on listbox with tab key.
            
            resultsListBox.MouseWheel += (s, e) => LoadImagesForVisibleResults();

            mainPanel.Controls.Add(resultsListBox);
            mainPanel.Controls.Add(searchBoxContainer);
            Controls.Add(mainPanel);

            searchBox.Focus(); // Set focus to searchBox initially

            InitializeLoadingIndicator();
        }

        private int preservedIndex = -1;
        private void InitializeHotkeys()
        {
            searchBox.KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.P)
                {
                    Close();
                    musicBeeContext.Post(_ => Plugin.ShowConfigDialog(), null);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.ControlKey) 
                {
                    // temporary hack to fix control+enter bug that makes the selected index jump to 0
                    preservedIndex = resultsListBox.SelectedIndex;
                }
                else if (e.KeyCode == Keys.Enter && e.Control && preservedIndex != -1)
                {
                    resultsListBox.SelectedIndex = preservedIndex;
                    HandleSearchBoxEnter(e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    preservedIndex = -1;
                }
                else if (e.KeyCode == Keys.Enter)
                {
                    HandleSearchBoxEnter(e);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    Close();
                }
                else if (e.KeyCode == Keys.Down)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.BeginUpdate();
                        resultsListBox.SelectedIndex = (resultsListBox.SelectedIndex + 1) % resultsListBox.Items.Count;
                        resultsListBox.EndUpdate();
                        LoadImagesForVisibleResults();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Up)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.BeginUpdate();
                        if (resultsListBox.SelectedIndex > 0)
                            resultsListBox.SelectedIndex--;
                        else
                            resultsListBox.SelectedIndex = resultsListBox.Items.Count - 1;
                        resultsListBox.EndUpdate();
                        LoadImagesForVisibleResults();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.PageUp)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.BeginUpdate();
                        resultsListBox.SelectedIndex = 0;
                        resultsListBox.EndUpdate();
                        LoadImagesForVisibleResults();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.PageDown)
                {
                    if (resultsListBox.Visible && resultsListBox.Items.Count > 0)
                    {
                        resultsListBox.BeginUpdate();
                        resultsListBox.SelectedIndex = resultsListBox.Items.Count - 1;
                        resultsListBox.EndUpdate();
                        LoadImagesForVisibleResults();
                    }
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            Deactivate += (s, e) => Close();

            KeyDown += (s, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.D)
                {
                    searchBox.Focus();
                    searchBox.SelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.Alt && e.KeyCode == Keys.R)
                {
                    HandleResultSelection(ArtistResult.FromSearchResult((SearchResult)resultsListBox.SelectedItem), e);
                }
                else if (e.Alt && e.KeyCode == Keys.A)
                {
                    HandleResultSelection(AlbumResult.FromSearchResult((SearchResult)resultsListBox.SelectedItem), e);
                }
            };
            KeyPreview = true; // Need to set KeyPreview to true for Form to receive KeyDown events before controls
        }

        private void InitializeLoadingIndicator()
        {
            const int ROTATION_INTERVAL = 20;
            const int ROTATION_ANGLE = 20;
            var COLOR = Color.FromArgb((int)(255 * 0.3), searchUIConfig.TextColor);

            Bitmap CreateLoadingSpinner(int size, Color color)
            {
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                using (Pen pen = new Pen(color, 2))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.DrawArc(pen, 2, 2, size - 4, size - 4, 0, 300);
                }
                return bmp;
            }

            void UpdateLoadingIndicatorPosition()
            {
                if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible)
                {
                    loadingIndicator.Location = new Point(
                        searchBox.Right - loadingIndicator.Width - 10,
                        searchBox.Top + (searchBox.Height - loadingIndicator.Height) / 2
                    );
                }
            }

            loadingIndicator = new PictureBox
            {
                Size = new Size(24, 24),
                BackColor = Color.Transparent,
                Image = CreateLoadingSpinner(24, COLOR),
                Visible = true
            };

            searchBox.TextChanged += (s, e) => UpdateLoadingIndicatorPosition();
            searchBox.SizeChanged += (s, e) => UpdateLoadingIndicatorPosition();
            UpdateLoadingIndicatorPosition();

            Controls.Add(loadingIndicator);
            loadingIndicator.BringToFront();

            var spinTimer = new System.Windows.Forms.Timer { Interval = ROTATION_INTERVAL };
            float rotationAngle = 0;
            spinTimer.Tick += (s, e) => {
                if (loadingIndicator != null && !loadingIndicator.IsDisposed && loadingIndicator.Visible)
                {
                    Bitmap rotatedBmp = new Bitmap(24, 24);
                    using (Graphics g = Graphics.FromImage(rotatedBmp))
                    using (Pen pen = new Pen(COLOR, 2))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.TranslateTransform(12, 12);
                        g.RotateTransform(rotationAngle);
                        g.TranslateTransform(-12, -12);
                        g.DrawArc(pen, 2, 2, 24 - 4, 24 - 4, 0, 300);
                    }
                    loadingIndicator.Image?.Dispose();
                    loadingIndicator.Image = rotatedBmp;
                    loadingIndicator.Refresh();

                    rotationAngle += ROTATION_ANGLE;
                    if (rotationAngle >= 360)
                    {
                        rotationAngle -= 360;
                    }
                }
            };
            spinTimer.Start();
        }

        private void InitializeImageLoadingTimer()
        {
            imageLoadDebounceTimer = new System.Windows.Forms.Timer
            {
                Interval = IMAGE_DEBOUNCE_MS
            };
            imageLoadDebounceTimer.Tick += async (s, e) =>
            {
                imageLoadDebounceTimer.Stop();
                await LoadImagesForVisibleResults();
            };
        }

        private async Task LoadImagesForVisibleResults()
        {
            if (!searchUIConfig.ShowImages || resultsListBox.Items.Count == 0) return;

            try
            {
                int startIndex = resultsListBox.TopIndex;
                int endIndex = Math.Min(startIndex + searchUIConfig.MaxResultsVisible, resultsListBox.Items.Count);

                // Create a list of tasks for loading all visible images
                var loadTasks = new List<Task>();

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (IsDisposed) return;

                    // Capture both the index and the result for validation
                    var result = (SearchResult)resultsListBox.Items[i];

                    var loadTask = Task.Run(async () => {
                        var image = await imageService.GetImageAsync(result);
                        if (image != null && !IsDisposed)
                        {
                            BeginInvoke((Action)(() => {
                                if (!IsDisposed)
                                {
                                    // Find the current index of this result, if it still exists
                                    int currentIndex = resultsListBox.Items.IndexOf(result);
                                    if (currentIndex >= 0)
                                    {
                                        resultsListBox.Invalidate(resultsListBox.GetItemRectangle(currentIndex));
                                    }
                                }
                            }));
                        }
                    });

                    loadTasks.Add(loadTask);
                }

                await Task.WhenAll(loadTasks);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading images: {ex}");
            }
        }

        private async Task LoadTracksAsync()
        {
            try
            {
                // Show default results immediately
                SearchBoxSetDefaultResults();

                await searchService.LoadTracksAsync();
                isLoading = false;
                
                // Update UI on completion
                if (!IsDisposed)
                {
                    BeginInvoke((Action)(() => {
                        loadingIndicator.Visible = false;
                        
                        // Only refresh results if there's a search in progress
                        if (!string.IsNullOrWhiteSpace(searchBox.Text))
                        {
                            SearchBox_TextChanged(null, EventArgs.Empty);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading tracks: {ex}");
            }
        }

        private Bitmap CreateIcon(Color color, int width, int height, ResultType type, int lineWidth = 1)
        {
            Bitmap icon = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(icon))
            using (Pen pen = new Pen(color, lineWidth))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                switch (type)
                {
                    case ResultType.Album:
                        // Thin outer circle (record)
                        g.DrawEllipse(pen, 2, 2, width - 4, height - 4);

                        // Thick inner circle (label)
                        using (Pen thickPen = new Pen(color, lineWidth * 2)) // Thicker pen for the inner circle
                        {
                            int innerCircleSize = width / 3; // Size of the inner circle
                            g.DrawEllipse(thickPen,
                                (width - innerCircleSize) / 2, // Center horizontally
                                (height - innerCircleSize) / 2, // Center vertically
                                innerCircleSize,
                                innerCircleSize
                            );
                        }
                        break;
                    case ResultType.Song:
                        // Triangle pointing right with rounded corners
                        using (GraphicsPath path = new GraphicsPath())
                        {
                            float cornerRadius = 2f;
                            PointF[] points = {
                                new PointF(4, 2),
                                new PointF(width - 2, (height - 2) / 2),
                                new PointF(4, height - 4)
                            };

                            // Create rounded corners using curves
                            path.AddBezier(
                                points[0].X, points[0].Y + cornerRadius,  // Start point
                                points[0].X, points[0].Y,                 // Control point 1
                                points[0].X + cornerRadius, points[0].Y,  // Control point 2
                                points[0].X + cornerRadius, points[0].Y   // End point
                            );
                            path.AddLine(
                                points[0].X + cornerRadius, points[0].Y,
                                points[1].X - cornerRadius, points[1].Y - cornerRadius
                            );
                            path.AddBezier(
                                points[1].X - cornerRadius, points[1].Y - cornerRadius,
                                points[1].X, points[1].Y - cornerRadius,
                                points[1].X, points[1].Y,
                                points[1].X - cornerRadius, points[1].Y + cornerRadius
                            );
                            path.AddLine(
                                points[1].X - cornerRadius, points[1].Y + cornerRadius,
                                points[2].X + cornerRadius, points[2].Y
                            );
                            path.AddBezier(
                                points[2].X + cornerRadius, points[2].Y,
                                points[2].X, points[2].Y,
                                points[2].X, points[2].Y - cornerRadius,
                                points[2].X, points[2].Y - cornerRadius
                            );
                            path.CloseFigure();

                            g.DrawPath(pen, path);
                        }
                        break;
                    case ResultType.Artist:
                        // Head (small circle)
                        int headSize = width / 3;
                        g.DrawEllipse(pen, 
                            (width - headSize) / 2,  // center horizontally
                            1,                       // near top
                            headSize, 
                            headSize
                        );
                        
                        // Body (oval)
                        int bodyWidth = width - 4;
                        int bodyHeight = height - headSize - 4;
                        g.DrawEllipse(pen,
                            2,                          // left edge
                            headSize + 2,               // below head
                            bodyWidth,
                            bodyHeight
                        );
                        break;
                    case ResultType.Playlist:
                        // List icon (3 horizontal lines)
                        int lineSpacing = 5;
                        for (int i = 0; i < 3; i++)
                        {
                            g.DrawLine(pen,
                                3,
                                4 + i * lineSpacing,
                                width - 4,
                                4 + i * lineSpacing);
                        }
                        break;
                }
            }
            return icon;
        }

        private void SearchBoxSetDefaultResults()
        {
            switch (searchUIConfig.DefaultResults)
            {
                case SearchUIConfig.DefaultResultsChoice.Playing:
                    var playingItems = searchService.GetPlayingItems();
                    UpdateResultsList(playingItems);
                    break;
                case SearchUIConfig.DefaultResultsChoice.Selected:
                    var selectedItems = searchService.GetSelectedTracks();
                    UpdateResultsList(selectedItems);
                    break;
                default:
                    UpdateResultsList(null);
                    break;
            }
        }

        private async void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string query = searchBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                SearchBoxSetDefaultResults();
                return;
            }

            if (isLoading)
            {
                UpdateResultsList(null);
                return;
            }

            // Cancel any ongoing search
            _currentSearchCts?.Cancel();
            _currentSearchCts = new CancellationTokenSource();

            // Increment sequence number for this search
            long searchSequence = Interlocked.Increment(ref _currentSearchSequence);

            try
            {
                loadingIndicator.Visible = true;

                var filter = ResultType.All;
                Dictionary<ResultType, int> resultLimits = null;

                if (query.EndsWith(".."))
                {
                    query = query.Substring(0, query.Length - 2).TrimEnd();
                    resultLimits = new Dictionary<ResultType, int> { { ResultType.All, 100 } };
                }

                if (query.StartsWith("a:", StringComparison.OrdinalIgnoreCase))
                {
                    filter = ResultType.Artist;
                    query = query.Substring(2).TrimStart();
                }
                else if (query.StartsWith("l:", StringComparison.OrdinalIgnoreCase))
                {
                    filter = ResultType.Album;
                    query = query.Substring(2).TrimStart();
                }
                else if (query.StartsWith("t:", StringComparison.OrdinalIgnoreCase) || query.StartsWith("s:", StringComparison.OrdinalIgnoreCase))
                {
                    filter = ResultType.Song;
                    query = query.Substring(2).TrimStart();
                }
                else if (query.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
                {
                    filter = ResultType.Playlist;
                    query = query.Substring(2).TrimStart();
                }

                Debug.WriteLine($"Query: {query}");
                var stopwatch = Stopwatch.StartNew();

                if (INCREMENTAL_UPDATE)
                {
                    await searchService.SearchIncrementalAsync(
                        query, 
                        filter, 
                        _currentSearchCts.Token,
                        results => {
                            if (!_currentSearchCts.Token.IsCancellationRequested)
                            {
                                BeginInvoke((Action)(() => {
                                    // Only update if this is still the most recent search
                                    if (searchSequence == _currentSearchSequence)
                                    {
                                        UpdateResultsList(results);
                                    }
                                }));
                            }
                        },
                        resultLimits
                    );
                }
                else
                {
                    var results = await searchService.SearchIncrementalAsync(query, filter, _currentSearchCts.Token, null, resultLimits);
                    if (!_currentSearchCts.Token.IsCancellationRequested && searchSequence == _currentSearchSequence)
                    {
                        try
                        {
                            BeginInvoke((Action)(() => UpdateResultsList(results)));
                        }
                        catch { }
                    }
                }

                stopwatch.Stop();
                Debug.WriteLine($"Search took {stopwatch.ElapsedMilliseconds} ms.");

                // Hide loading indicator if this is still the current search
                if (!_currentSearchCts.Token.IsCancellationRequested && searchSequence == _currentSearchSequence)
                {
                    loadingIndicator.Visible = false;
                }
            }
            catch (OperationCanceledException)
            {
                // Search was cancelled, ignore
            }

            // Reset image loading timer
            if (searchUIConfig.ShowImages)
            {
                imageLoadDebounceTimer.Stop();
                imageLoadDebounceTimer.Start();
            }
        }

        private void UpdateResultsList(List<SearchResult> searchResults)
        {
            if (searchResults == null || searchResults.Count == 0)
            {
                if (resultsListBox.Visible)
                {
                    resultsListBox.Visible = false;
                    resultsListBox.Items.Clear();
                    UpdateResultsListHeight(0);
                    Height = 42;
                }
                return;
            }

            resultsListBox.BeginUpdate();
            try
            {
                int currentSelection = resultsListBox.SelectedIndex;

                resultsListBox.Items.Clear();
                foreach (var result in searchResults)
                {
                    resultsListBox.Items.Add(result);
                }

                if (!resultsListBox.Visible)
                {
                    resultsListBox.Visible = true;
                }

                //if (currentSelection < resultsListBox.Items.Count && currentSelection >= 0)
                //{
                //    resultsListBox.SelectedIndex = currentSelection;
                //}
                //else
                //{
                //    resultsListBox.SelectedIndex = 0;
                //    resultsListBox.TopIndex = 0;
                //}

                resultsListBox.SelectedIndex = 0;
                resultsListBox.TopIndex = 0;

                int newHeight = Math.Min(searchResults.Count, searchUIConfig.MaxResultsVisible) * resultsListBox.ItemHeight;
                if (resultsListBox.Height != newHeight)
                {
                    UpdateResultsListHeight(searchResults.Count);
                    Height = 42 + resultsListBox.Height;
                }
            }
            finally
            {
                resultsListBox.EndUpdate();
            }
        }

        private void UpdateResultsListHeight(int resultCount)
        {
            int newHeight = Math.Min(resultCount, searchUIConfig.MaxResultsVisible) * resultsListBox.ItemHeight;
            resultsListBox.Height = newHeight;
        }

        private void ResultsListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= resultsListBox.Items.Count) return;

            SearchResult resultItem = (SearchResult)resultsListBox.Items[e.Index];
            Graphics g = e.Graphics;
            Rectangle bounds = e.Bounds;

            Color backgroundColor = searchUIConfig.BaseColor;
            if (e.State.HasFlag(DrawItemState.Selected) || resultsListBox.SelectedIndex == e.Index && resultsListBox.Visible && resultsListBox.Items.Count > 0)
            {
                backgroundColor = searchUIConfig.ResultHighlightColor;
            }

            using (SolidBrush backgroundBrush = new SolidBrush(backgroundColor))
            {
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
            }

            Color textColor = resultsListBox.ForeColor;
            using (SolidBrush textBrush = new SolidBrush(textColor))
            {
                int iconWidth = ICON_SIZE;
                int iconPaddingRight = 5;
                int offsetY = 4;
                int leftPadding = 6 + (IMAGE_SIZE - ICON_SIZE) / 2;
                int textStartX = bounds.X + IMAGE_SIZE + iconPaddingRight + 8;

                Font titleFont = new Font(resultsListBox.Font.FontFamily, 12, FontStyle.Regular);
                Font detailFont = new Font(resultsListBox.Font.FontFamily, 10, FontStyle.Regular);
                Color detailColor = Color.Gray;

                Image displayImage = null;
                if (searchUIConfig.ShowImages)
                {
                    displayImage = imageService.GetCachedImage(resultItem);

                    if (displayImage != null)
                    {
                        leftPadding = 6;
                        iconWidth = IMAGE_SIZE;
                    }
                }

                // Fall back to icon if no image available
                if (displayImage == null)
                {
                    displayImage = GetIcon(resultItem.Type);
                }

                if (displayImage != null)
                {
                    int imageY = bounds.Y + (bounds.Height - iconWidth) / 2;
                    g.DrawImage(displayImage, bounds.X + leftPadding, imageY, iconWidth, iconWidth);
                }


                if (string.IsNullOrEmpty(resultItem.DisplayDetail))
                {
                    // Calculate maximum width available for text
                    int maxTextWidth = bounds.Width - textStartX - ICON_SIZE - 20; // 20 for right padding + icon padding
                    string truncatedTitle = TextRenderer.MeasureText(resultItem.DisplayTitle, titleFont).Width > maxTextWidth
                        ? TextRenderer.MeasureText(resultItem.DisplayTitle + "...", titleFont).Width <= maxTextWidth
                            ? resultItem.DisplayTitle + "..."
                            : resultItem.DisplayTitle.Substring(0, Math.Max(1, resultItem.DisplayTitle.Length * maxTextWidth / TextRenderer.MeasureText(resultItem.DisplayTitle, titleFont).Width - 3)) + "..."
                        : resultItem.DisplayTitle;
                    
                    g.DrawString(truncatedTitle, titleFont, textBrush, textStartX, bounds.Y + offsetY + 5);
                }
                else
                {
                    // Calculate maximum width available for text
                    int maxTextWidth = bounds.Width - textStartX - ICON_SIZE - 20; // 20 for right padding + icon padding
                    
                    string truncatedTitle = TextRenderer.MeasureText(resultItem.DisplayTitle, titleFont).Width > maxTextWidth
                        ? TextRenderer.MeasureText(resultItem.DisplayTitle + "...", titleFont).Width <= maxTextWidth
                            ? resultItem.DisplayTitle + "..."
                            : resultItem.DisplayTitle.Substring(0, Math.Max(1, resultItem.DisplayTitle.Length * maxTextWidth / TextRenderer.MeasureText(resultItem.DisplayTitle, titleFont).Width - 3)) + "..."
                        : resultItem.DisplayTitle;

                    string truncatedDetail = TextRenderer.MeasureText(resultItem.DisplayDetail, detailFont).Width > maxTextWidth
                        ? TextRenderer.MeasureText(resultItem.DisplayDetail + "...", detailFont).Width <= maxTextWidth
                            ? resultItem.DisplayDetail + "..."
                            : resultItem.DisplayDetail.Substring(0, Math.Max(1, resultItem.DisplayDetail.Length * maxTextWidth / TextRenderer.MeasureText(resultItem.DisplayDetail, detailFont).Width - 3)) + "..."
                        : resultItem.DisplayDetail;

                    g.DrawString(truncatedTitle, titleFont, textBrush, textStartX, bounds.Y + offsetY);
                    g.DrawString(truncatedDetail, detailFont, new SolidBrush(detailColor), textStartX, bounds.Y + offsetY + titleFont.GetHeight() + 2);
                }

                // Add type indicator icon on the right for albums and tracks when images are enabled
                if (searchUIConfig.ShowImages)
                {
                    var typeIcon = GetIcon(resultItem.Type);
                    int rightPadding = 10;
                    int iconY = (int)(bounds.Y + (bounds.Height - ICON_SIZE / 1.5) / 2);
                    g.DrawImage(typeIcon, (int)(bounds.Right - ICON_SIZE / 1.5 - rightPadding), iconY, (int)(ICON_SIZE / 1.5), (int)(ICON_SIZE / 1.5));
                }
            }
        }

        public Image GetIcon(ResultType resultType)
        {
            if (resultType == ResultType.Song)
                return songIcon;
            else if (resultType == ResultType.Album)
                return albumIcon;
            else if (resultType == ResultType.Artist)
                return artistIcon;
            else if (resultType == ResultType.Playlist)
                return playlistIcon;
            return null;
        }

        private void ResultsListBox_Click(object sender, EventArgs e)
        {
            if (resultsListBox.SelectedItem != null)
            {
                var modifiers = Keys.None;
                if (Control.ModifierKeys.HasFlag(Keys.Control)) modifiers |= Keys.Control;
                if (Control.ModifierKeys.HasFlag(Keys.Shift)) modifiers |= Keys.Shift;
                var keyEventArgs = new KeyEventArgs(modifiers);
                
                HandleResultSelection((SearchResult)resultsListBox.SelectedItem, keyEventArgs);
            }
        }

        private void HandleSearchBoxEnter(KeyEventArgs e)
        {
            if (resultsListBox.Visible && resultsListBox.SelectedIndex != -1) // Use selected index if list is visible
            {
                HandleResultSelection((SearchResult)resultsListBox.SelectedItem, e);
            }
            else if (resultsListBox.Visible && resultsListBox.Items.Count > 0) // If list is visible but no selection (shouldn't happen but for safety), take first item
            {
                HandleResultSelection((SearchResult)resultsListBox.Items[0], e);
            }
            else
            {
                Close();
            }
        }

        private void HandleResultSelection(SearchResult selectedItem, KeyEventArgs e)
        {
            //musicBeeContext.Post(_ =>
            //{
            //    if (resultAcceptAction(selectedItem, e))
            //    {
            //        BeginInvoke((Action)Close);
            //    }
            //}, null);
            Close();
            musicBeeContext.Post(_ => resultAcceptAction(searchBox.Text, selectedItem, e), null);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                imageLoadDebounceTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

