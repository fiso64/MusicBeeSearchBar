using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using System;
using System.Collections.Generic;
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
                        if (imageService.GetCachedImage(result) == null)
                        {
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

        private void SearchBoxSetDefaultResults()
        {
            switch (searchUIConfig.DefaultResults)
            {
                case SearchUIConfig.DefaultResultsChoice.Playing:
                    var playingItems = searchService.GetPlayingItems();
                    UpdateResultsList(playingItems);
                    LoadImagesForVisibleResults();
                    break;
                case SearchUIConfig.DefaultResultsChoice.Selected:
                    var selectedItems = searchService.GetSelectedTracks();
                    UpdateResultsList(selectedItems);
                    LoadImagesForVisibleResults();
                    break;
                default:
                    UpdateResultsList(null);
                    break;
            }
        }

        private async void SearchBox_TextChanged(object sender, EventArgs e)
        {
            string query = searchBox.Text; // Trim later, depending on command/search path

            if (string.IsNullOrWhiteSpace(query.Trim()))
            {
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
            if (searchResults == null || searchResults.Count == 0)
            {
                if (resultsListBox.Visible)
                {
                    resultsListBox.Visible = false;
                    resultsListBox.Items.Clear();
                    UpdateResultsListHeight(0); // Method in SearchBar.UI.cs
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

                resultsListBox.SelectedIndex = 0;
                resultsListBox.TopIndex = 0;

                int newHeight = Math.Min(searchResults.Count, searchUIConfig.MaxResultsVisible) * resultsListBox.ItemHeight;
                if (resultsListBox.Height != newHeight)
                {
                    UpdateResultsListHeight(searchResults.Count); // Method in SearchBar.UI.cs
                    Height = 42 + resultsListBox.Height;
                }
            }
            finally
            {
                resultsListBox.EndUpdate();
            }
        }
    }
}
