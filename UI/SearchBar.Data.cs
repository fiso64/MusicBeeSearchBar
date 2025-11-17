using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicBeePlugin.UI
{
    public partial class SearchBar
    {
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
        int startIndex = resultsListBox.FirstVisibleIndex;
        int endIndex = Math.Min(startIndex + searchUIConfig.MaxResultsVisible + VISIBLE_ITEMS_BUFFER, resultsListBox.Items.Count);

                // Create a list of tasks for loading all visible images
                var loadTasks = new List<Task>();

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (IsDisposed) return;

                    // Capture both the index and the result for validation
                    var result = resultsListBox.Items[i];

                    var loadTask = Task.Run(async () => {
                        if (imageService.GetCachedImage(result) == null)
                        {
                            var image = await imageService.GetImageAsync(result);
                            if (image != null && !IsDisposed)
                            {
                                // Invalidate the whole list as items might move
                                BeginInvoke((Action)(() => {
                                    if (!IsDisposed)
                                    {
                                        resultsListBox.Invalidate();
                                    }
                                }));
                            }
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

        private void SearchBoxSetDefaultResults()
        {
            Debug.WriteLine("[SearchBar] SearchBoxSetDefaultResults: Fired.");
            List<SearchResult> defaultItems = null;

            switch (searchUIConfig.DefaultResults)
            {
                case SearchUIConfig.DefaultResultsChoice.Playing:
                    var playingItems = searchService.GetPlayingItems();
                    Debug.WriteLine($"[SearchBar] SearchBoxSetDefaultResults: Found {playingItems?.Count ?? 0} 'Playing' items.");
                    UpdateResultsList(playingItems);
                    LoadImagesForVisibleResults();
                    break;
                case SearchUIConfig.DefaultResultsChoice.Selected:
                    var selectedItems = searchService.GetSelectedTracks();
                    Debug.WriteLine($"[SearchBar] SearchBoxSetDefaultResults: Found {selectedItems?.Count ?? 0} 'Selected' items.");
                    UpdateResultsList(selectedItems);
                    LoadImagesForVisibleResults();
                    break;
                default:
                    Debug.WriteLine("[SearchBar] SearchBoxSetDefaultResults: Default results set to 'None'.");
                    UpdateResultsList(null);
                    break;
            }
        }

        private async void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string query = searchBox.Text; // Trim later, depending on command/search path

            if (string.IsNullOrWhiteSpace(query.Trim()))
            {
                Debug.WriteLine("[SearchBar] SearchBox_TextChanged: Query is empty, calling SearchBoxSetDefaultResults.");
                SearchBoxSetDefaultResults();
                return;
            }

            if (isLoading && !query.TrimStart().StartsWith(">")) // Allow command search even when tracks are loading
            {
                UpdateResultsList(null);
                return;
            }

            // Cancel any ongoing search/command listing
            _currentSearchCts?.Cancel();
            _currentSearchCts = new CancellationTokenSource();

            // Increment sequence number for this search
            long searchSequence = Interlocked.Increment(ref _currentSearchSequence);

            try
            {
                loadingIndicator.Visible = true;

                if (query.TrimStart().StartsWith(">"))
                {
                    string commandQuery = query.TrimStart().Substring(1);
                    // Call the synchronous method from SearchService
                    var commandResults = searchService.SearchCommands(commandQuery, _currentSearchCts.Token);
                    if (!_currentSearchCts.Token.IsCancellationRequested && searchSequence == _currentSearchSequence)
                    {
                        BeginInvoke((Action)(() => UpdateResultsList(commandResults)));
                        loadingIndicator.Visible = false;
                    }
                    return; // Command search handled, exit here
                }

                // Existing search logic starts here
                query = query.Trim(); // Trim for regular search
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

            // Reset image loading timer (only if not a command search)
            if (searchUIConfig.ShowImages && (query.TrimStart().Length == 0 || !query.TrimStart().StartsWith(">")))
            {
                imageLoadDebounceTimer.Stop();
                imageLoadDebounceTimer.Start();
            }
        }

        // Removed SearchCommandsAsync method from here, as it's now in SearchService

        private void UpdateResultsList(List<SearchResult> searchResults)
        {
            var results = searchResults ?? new List<SearchResult>();
            Debug.WriteLine($"[SearchBar] UpdateResultsList: Received {results.Count} results.");

            if (searchUIConfig.GroupResultsByType && searchUIConfig.ShowTypeHeaders && results.Count > 0)
            {
                var newResults = new List<SearchResult>();
                bool isDefaultView = string.IsNullOrWhiteSpace(searchBox.Text);

                if (isDefaultView) // Default results view has custom ordering
                {
                    Type previousResultType = null;
                    foreach (var result in results)
                    {
                        var currentResultType = result.GetType();
                        if (currentResultType != previousResultType)
                        {
                            previousResultType = currentResultType;

                            if (currentResultType == typeof(ArtistResult))
                                newResults.Add(new HeaderResult("Artists"));
                            else if (currentResultType == typeof(AlbumResult))
                                newResults.Add(new HeaderResult("Albums"));
                            else if (currentResultType == typeof(AlbumArtistResult))
                                newResults.Add(new HeaderResult("Album Artists"));
                            else if (currentResultType == typeof(CommandResult))
                                newResults.Add(new HeaderResult("Commands"));
                            else if (currentResultType == typeof(SongResult))
                                newResults.Add(new HeaderResult("Songs"));
                            else if (currentResultType == typeof(PlaylistResult))
                                newResults.Add(new HeaderResult("Playlists"));
                        }
                        newResults.Add(result);
                    }
                }
                else // Search results view is ordered by ResultType enum
                {
                    ResultType? currentType = null;
                    foreach (var result in results)
                    {
                        if (result.Type != currentType)
                        {
                            currentType = result.Type;
                            switch (currentType)
                            {
                                case ResultType.Command:
                                    newResults.Add(new HeaderResult("Commands"));
                                    break;
                                case ResultType.Artist:
                                    newResults.Add(new HeaderResult("Artists"));
                                    break;
                                case ResultType.Album:
                                    newResults.Add(new HeaderResult("Albums"));
                                    break;
                                case ResultType.Song:
                                    newResults.Add(new HeaderResult("Songs"));
                                    break;
                                case ResultType.Playlist:
                                    newResults.Add(new HeaderResult("Playlists"));
                                    break;
                            }
                        }
                        newResults.Add(result);
                    }
                }
                results = newResults;
            }

            var mainPanel = resultsListBox.Parent as Panel;
            if (mainPanel == null) return;

            var searchContainer = searchBox.Parent as Panel;
            if (searchContainer == null) return;

            int nonListHeight = mainPanel.Padding.Vertical + searchContainer.Height;

            int listHeight = 0;
            int itemsToMeasure = Math.Min(results.Count, searchUIConfig.MaxResultsVisible);
            for (int i = 0; i < itemsToMeasure; i++)
            {
                listHeight += (results[i].Type == ResultType.Header)
                    ? resultsListBox.HeaderHeight + ((i > 0) ? CustomResultList.HEADER_TOP_PADDING : 0)
                    : resultsListBox.ItemHeight;
            }

            if (results.Count > 0)
            {
                if (!spacerPanel.Visible) spacerPanel.Visible = true;
                if (!resultsListBox.Visible) resultsListBox.Visible = true;
                
                Height = nonListHeight + spacerPanel.Height + listHeight;
            }
            else
            {
                if (spacerPanel.Visible) spacerPanel.Visible = false;
                if (resultsListBox.Visible) resultsListBox.Visible = false;
                
                Height = nonListHeight;
            }

            resultsListBox.Items = results;
        }
    }
}
