using MusicBeePlugin.Config;
using MusicBeePlugin.Services;
using MusicBeePlugin.Utils;
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
                int startIndex = resultsListBox.FirstVisibleIndex;
                int lastVisibleIndex = resultsListBox.LastVisibleIndex;
                int endIndex = Math.Min(lastVisibleIndex + VISIBLE_ITEMS_BUFFER, resultsListBox.Items.Count);

                // Create a list of tasks for loading all visible images
                var loadTasks = new List<Task>();

                for (int i = startIndex; i < endIndex; i++)
                {
                    if (IsDisposed) return;

                    // Capture both the index and the result for validation
                    var result = resultsListBox.Items[i];

                    int sizeToLoad = result.IsTopMatch ? resultsListBox.TopMatchImageSize : resultsListBox.NormalImageSize;

                    var loadTask = Task.Run(async () => {
                        if (imageService.GetCachedImage(result, sizeToLoad) == null)
                        {
                            var image = await imageService.GetImageAsync(result, sizeToLoad);
                            if (image != null && !IsDisposed)
                            {
                                // Invalidate the whole list as items might move
                                PostToUi(() => {
                                    if (!IsDisposed)
                                    {
                                        resultsListBox.Invalidate();
                                    }
                                });
                            }
                        }
                    });

                    loadTasks.Add(loadTask);
                }

                await Task.WhenAll(loadTasks);
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading images", ex);
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
                    PostToUi(() => {
                        loadingIndicator.Visible = false;

                        // A newly created docked view may already have restored results.
                        // Keep its position if its database finishes loading afterward.
                        if (_preserveRestoredPositionOnLoad)
                        {
                            _preserveRestoredPositionOnLoad = false;
                            if (!string.IsNullOrWhiteSpace(searchBox.Text)) RunSearch(true);
                        }
                        else SearchBox_TextChanged(null, EventArgs.Empty);
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error loading tracks", ex);
            }
        }

        private void SearchBoxSetDefaultResults()
        {
            Debug.WriteLine("[SearchBar] SearchBoxSetDefaultResults: Fired.");
            long defaultSequence = Interlocked.Increment(ref _defaultResultsSequence);

            if (searchUIConfig.DefaultResults == SearchUIConfig.DefaultResultsChoice.None)
            {
                Debug.WriteLine("[SearchBar] SearchBoxSetDefaultResults: Default results set to 'None'.");
                _defaultSearchResults = null;
                UpdateResultsList(null);
                return;
            }

            Task.Run(() =>
            {
                List<SearchResult> items = null;
                try
                {
                    switch (searchUIConfig.DefaultResults)
                    {
                        case SearchUIConfig.DefaultResultsChoice.Playing:
                            items = searchService.GetPlayingItems();
                            Debug.WriteLine($"[SearchBar] SearchBoxSetDefaultResults: Found {items?.Count ?? 0} 'Playing' items.");
                            break;
                        case SearchUIConfig.DefaultResultsChoice.Selected:
                            items = searchService.GetSelectedTracks();
                            Debug.WriteLine($"[SearchBar] SearchBoxSetDefaultResults: Found {items?.Count ?? 0} 'Selected' items.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("Error fetching default results", ex);
                }

                if (!IsDisposed && IsHandleCreated)
                {
                    PostToUi(() =>
                    {
                        if (!IsDisposed && defaultSequence == Interlocked.Read(ref _defaultResultsSequence)
                            && string.IsNullOrWhiteSpace(searchBox.Text))
                        {
                            _defaultSearchResults = items ?? new List<SearchResult>();
                            UpdateResultsList(BuildDefaultPagedResults());
                            LoadImagesForVisibleResults();
                        }
                    });
                }
            });
        }

        private static readonly ResultType[] PageableResultTypes =
        {
            ResultType.Artist, ResultType.Album, ResultType.Song, ResultType.Playlist
        };
        private const int ShowMorePageSize = 100;
        private string _expandedQuery;
        private readonly Dictionary<ResultType, int> _expandedByType = new Dictionary<ResultType, int>();
        private int _expandedUngrouped;
        private bool _showAllResults;
        private ResultType? _selectAllAfterSearch;
        private string _selectAllQuery;
        private List<SearchResult> _defaultSearchResults;
        private long _defaultResultsSequence;
        private System.Windows.Forms.Timer _selectionRefreshTimer;

        internal sealed class ExpandedSearchState
        {
            public string Query;
            public List<SearchResult> Results;
            public List<SearchResult> DefaultResults;
            public string ExpandedQuery;
            public Dictionary<ResultType, int> ExpandedByType;
            public int ExpandedUngrouped;
            public int ScrollTop;
            public int SelectedIndex;
            public int[] SelectedIndices;
            public int SelectionAnchor;
            public bool ShowAllResults;
        }

        internal ExpandedSearchState CaptureExpandedSearchState()
        {
            if (!isEmbedded || compactPanel || IsDisposed || Disposing) return null;
            return new ExpandedSearchState
            {
                Query = searchBox.Text,
                Results = new List<SearchResult>(resultsListBox.Items),
                DefaultResults = _defaultSearchResults == null ? null : new List<SearchResult>(_defaultSearchResults),
                ExpandedQuery = _expandedQuery,
                ExpandedByType = new Dictionary<ResultType, int>(_expandedByType),
                ExpandedUngrouped = _expandedUngrouped,
                ScrollTop = resultsListBox.ScrollTop,
                SelectedIndex = resultsListBox.SelectedIndex,
                SelectedIndices = resultsListBox.SelectedIndices,
                SelectionAnchor = resultsListBox.SelectionAnchor,
                ShowAllResults = _showAllResults
            };
        }

        internal void RestoreExpandedSearchState(ExpandedSearchState state)
        {
            if (!isEmbedded || compactPanel || IsDisposed || Disposing || state == null) return;
            _currentSearchCts?.Cancel();
            Interlocked.Increment(ref _currentSearchSequence);
            Interlocked.Increment(ref _defaultResultsSequence);
            _selectionRefreshTimer?.Stop();
            _restoringExpandedSearch = true;
            try
            {
                searchBox.Text = state.Query;
                searchToolbar?.SetActivePrefix(state.Query);
                _showAllResults = state.ShowAllResults;
                searchToolbar?.SetShowAll(_showAllResults);
                _defaultSearchResults = state.DefaultResults;
                _expandedQuery = state.ExpandedQuery;
                _expandedByType.Clear();
                foreach (var entry in state.ExpandedByType) _expandedByType[entry.Key] = entry.Value;
                _expandedUngrouped = state.ExpandedUngrouped;
                resultsListBox.Items = state.Results;
                resultsListBox.Visible = state.Results.Count > 0;
                resultsListBox.RestoreSelection(state.SelectedIndices, state.SelectedIndex, state.SelectionAnchor);
                resultsListBox.SetScrollPosition(state.ScrollTop);
                _restoredScrollTop = state.ScrollTop;
                _preserveRestoredPositionOnLoad = isLoading && state.Results.Count > 0;
            }
            finally { _restoringExpandedSearch = false; }
            if (searchUIConfig.ShowImages) { imageLoadDebounceTimer.Stop(); imageLoadDebounceTimer.Start(); }
        }

        private bool _restoringExpandedSearch;
        private int? _restoredScrollTop;
        private bool _preserveRestoredPositionOnLoad;

        internal void FinishExpandedStateLayout()
        {
            if (!_restoredScrollTop.HasValue || IsDisposed || Disposing || !Visible) return;
            int position = _restoredScrollTop.Value;
            _restoredScrollTop = null;
            resultsListBox.SetScrollPosition(position);
        }
        internal long ExpandedStateVersion { get; set; }
        internal bool ExpandedStateSyncEnabled { get; private set; }
        internal void EnableExpandedStateSync() { ExpandedStateSyncEnabled = true; }

        internal void ScheduleSelectedResultsRefresh()
        {
            if (!isEmbedded || _resourcesDisposed || IsDisposed || Disposing || !Visible
                || searchUIConfig.DefaultResults != SearchUIConfig.DefaultResultsChoice.Selected
                || !string.IsNullOrWhiteSpace(searchBox.Text)) return;

            // Invalidate in-flight work immediately, even while the next refresh is debounced.
            Interlocked.Increment(ref _defaultResultsSequence);
            if (_selectionRefreshTimer == null)
            {
                _selectionRefreshTimer = new System.Windows.Forms.Timer { Interval = 100 };
                _selectionRefreshTimer.Tick += (s, e) =>
                {
                    _selectionRefreshTimer.Stop();
                    if (!_resourcesDisposed && !IsDisposed && !Disposing
                        && string.IsNullOrWhiteSpace(searchBox.Text))
                        SearchBoxSetDefaultResults();
                };
            }
            _selectionRefreshTimer.Stop();
            _selectionRefreshTimer.Start();
        }

        private void SearchBox_TextChanged(object sender, EventArgs e)
        {
            if (_restoringExpandedSearch) return;
            if (_selectAllAfterSearch.HasValue && _selectAllQuery != searchBox.Text) _selectAllAfterSearch = null;
            if (sender != null) { _restoredScrollTop = null; _preserveRestoredPositionOnLoad = false; }
            searchToolbar?.SetActivePrefix(searchBox.Text);
            RunSearch(false);
            if (sender != null && isEmbedded && !compactPanel) Plugin.SaveExpandedSearchState(this);
        }

        private void ExpandSearchResults(ResultType type)
        {
            if (type == ResultType.All)
                _expandedUngrouped = AddResultCount(_expandedUngrouped, ShowMorePageSize);
            else
            {
                _expandedByType.TryGetValue(type, out int current);
                _expandedByType[type] = AddResultCount(current, ShowMorePageSize);
            }
            if (string.IsNullOrWhiteSpace(searchBox.Text) && _defaultSearchResults != null)
            {
                int scroll = resultsListBox.ScrollTop;
                int selection = resultsListBox.SelectedIndex;
                UpdateResultsList(BuildDefaultPagedResults(), true, scroll, selection);
            }
            else
                RunSearch(true);
        }

        private void ToggleShowAllResults()
        {
            _selectAllAfterSearch = null;
            _showAllResults = !_showAllResults;
            searchToolbar?.SetShowAll(_showAllResults);
            if (string.IsNullOrWhiteSpace(searchBox.Text) && _defaultSearchResults != null)
                UpdateResultsList(BuildDefaultPagedResults(), true, resultsListBox.ScrollTop, resultsListBox.SelectedIndex);
            else RunSearch(true);
            if (isEmbedded && !compactPanel) Plugin.SaveExpandedSearchState(this);
        }

        internal bool SelectAllEmbeddedResults()
        {
            if (!isEmbedded || IsDisposed || Disposing || resultsListBox == null || !resultsListBox.Visible
                || !(resultsListBox.SelectedItem is SearchResult result)
                || result.Type == ResultType.Header || result.Type == ResultType.ShowMore || result.Type == ResultType.Command)
                return false;
            SelectAllResults(result.Type);
            return true;
        }

        private void SelectAllResults(ResultType type)
        {
            _selectAllAfterSearch = type;
            _selectAllQuery = searchBox.Text;
            _showAllResults = true;
            searchToolbar?.SetShowAll(true);
            if (string.IsNullOrWhiteSpace(searchBox.Text) && _defaultSearchResults != null)
                UpdateResultsList(BuildDefaultPagedResults(), true, resultsListBox.ScrollTop, resultsListBox.SelectedIndex);
            else RunSearch(true);
        }

        private void ApplyPendingSelectAll()
        {
            if (!_selectAllAfterSearch.HasValue || _selectAllQuery != searchBox.Text || !resultsListBox.Items.Any()) return;
            var type = _selectAllAfterSearch.Value;
            _selectAllAfterSearch = null;
            resultsListBox.SelectAllOfType(type);
        }

        private static int AddResultCount(int count, int increment)
        {
            return (int)Math.Min((long)int.MaxValue - 1, (long)count + increment);
        }

        private async void RunSearch(bool preservePosition)
        {
            if (_resourcesDisposed || IsDisposed || Disposing) return;
            string originalQuery = searchBox.Text;
            if (_expandedQuery != originalQuery)
            {
                _expandedQuery = originalQuery;
                _expandedByType.Clear();
                _expandedUngrouped = 0;
                preservePosition = false;
            }

            int previousScroll = resultsListBox.ScrollTop;
            int previousSelection = resultsListBox.SelectedIndex;
            _currentSearchCts?.Cancel();
            _currentSearchCts = new CancellationTokenSource();
            var searchToken = _currentSearchCts.Token;
            long searchSequence = Interlocked.Increment(ref _currentSearchSequence);

            if (string.IsNullOrWhiteSpace(originalQuery))
            {
                SearchBoxSetDefaultResults();
                return;
            }
            if (isLoading && !originalQuery.TrimStart().StartsWith(">"))
            {
                UpdateResultsList(null);
                return;
            }

            try
            {
                loadingIndicator.Visible = true;
                if (originalQuery.TrimStart().StartsWith(">"))
                {
                    string commandQuery = originalQuery.TrimStart().Substring(1);
                    var commands = searchService.SearchCommands(commandQuery, searchToken);
                    _expandedByType.TryGetValue(ResultType.Command, out int commandExtra);
                    int commandLimit = _showAllResults ? int.MaxValue : AddResultCount(ShowMorePageSize,
                        searchUIConfig.GroupResultsByType ? commandExtra : _expandedUngrouped);
                    var visibleCommands = commands.Take(commandLimit).ToList();
                    if (commands.Count > commandLimit)
                        visibleCommands.Add(new ShowMoreResult(searchUIConfig.GroupResultsByType ? ResultType.Command : ResultType.All));
                    if (!searchToken.IsCancellationRequested && searchSequence == _currentSearchSequence)
                    {
                        PostToUi(() =>
                        {
                            if (!searchToken.IsCancellationRequested && searchSequence == _currentSearchSequence)
                                UpdateResultsList(visibleCommands, preservePosition, previousScroll, previousSelection);
                        });
                        loadingIndicator.Visible = false;
                    }
                    return;
                }

                string query = originalQuery.Trim();
                var filter = ResultType.All;
                int? suffixLimit = null;
                int trailingDots = 0;
                while (query.Length > trailingDots && query[query.Length - 1 - trailingDots] == '.') trailingDots++;
                if (trailingDots >= 2 && trailingDots % 2 == 0)
                {
                    query = query.Substring(0, query.Length - trailingDots).TrimEnd();
                    suffixLimit = (int)Math.Min(int.MaxValue - 1, 100 * Math.Pow(10, (trailingDots / 2) - 1));
                }

                if (query.StartsWith("r:", StringComparison.OrdinalIgnoreCase))
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

                var initialLimits = new Dictionary<ResultType, int>
                {
                    { ResultType.Artist, suffixLimit ?? searchUIConfig.ArtistResultLimit },
                    { ResultType.Album, suffixLimit ?? searchUIConfig.AlbumResultLimit },
                    { ResultType.Song, suffixLimit ?? searchUIConfig.SongResultLimit },
                    { ResultType.Playlist, suffixLimit ?? searchUIConfig.PlaylistResultLimit }
                };
                if (filter != ResultType.All)
                    initialLimits[filter] = Math.Max(30, initialLimits[filter]);
                var visibleLimits = new Dictionary<ResultType, int>();
                var fetchLimits = new Dictionary<ResultType, int>();
                foreach (var type in PageableResultTypes)
                {
                    int initial = _showAllResults ? int.MaxValue : Math.Max(0, initialLimits[type]);
                    _expandedByType.TryGetValue(type, out int expanded);
                    int extra = searchUIConfig.GroupResultsByType ? expanded : _expandedUngrouped;
                    int limit = _showAllResults ? int.MaxValue : initial == 0 ? 0 : AddResultCount(initial, extra);
                    visibleLimits[type] = limit;
                    fetchLimits[type] = _showAllResults ? int.MaxValue : limit == 0 ? 0 : AddResultCount(limit, 1);
                }

                var results = await searchService.SearchIncrementalAsync(query, filter, searchToken, null, fetchLimits);
                if (_resourcesDisposed || IsDisposed || Disposing || !IsHandleCreated) return;
                if (!searchToken.IsCancellationRequested && searchSequence == _currentSearchSequence)
                {
                    var visible = _showAllResults ? results : BuildPagedResults(results, initialLimits, visibleLimits,
                        searchUIConfig.GroupResultsByType, _expandedUngrouped);
                    PostToUi(() =>
                    {
                        if (!searchToken.IsCancellationRequested && searchSequence == _currentSearchSequence)
                            UpdateResultsList(visible, preservePosition, previousScroll, previousSelection);
                    });
                    loadingIndicator.Visible = false;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Logger.Error("Error during search", ex);
                if (!_resourcesDisposed && !IsDisposed && !Disposing) loadingIndicator.Visible = false;
            }

            if (!_resourcesDisposed && !IsDisposed && !Disposing && searchUIConfig.ShowImages)
            {
                imageLoadDebounceTimer.Stop();
                imageLoadDebounceTimer.Start();
            }
        }

        private static List<SearchResult> BuildPagedResults(List<SearchResult> candidates,
            Dictionary<ResultType, int> initialLimits, Dictionary<ResultType, int> visibleLimits,
            bool grouped, int ungroupedExtra)
        {
            var shown = new HashSet<SearchResult>();
            foreach (var type in PageableResultTypes)
            {
                int limit = grouped ? visibleLimits[type] : Math.Max(0, initialLimits[type]);
                foreach (var result in candidates.Where(item => item.Type == type).Take(limit)) shown.Add(result);
            }
            if (!grouped)
            {
                foreach (var result in candidates.Where(item => !shown.Contains(item)).Take(ungroupedExtra)) shown.Add(result);
            }

            var visible = candidates.Where(shown.Contains).ToList();
            if (!grouped)
            {
                if (candidates.Count > shown.Count) visible.Add(new ShowMoreResult(ResultType.All));
                return visible;
            }

            foreach (var type in PageableResultTypes)
            {
                if (candidates.Count(item => item.Type == type) <= visibleLimits[type]) continue;
                int last = visible.FindLastIndex(item => item.Type == type);
                if (last >= 0) visible.Insert(last + 1, new ShowMoreResult(type));
            }
            return visible;
        }

        private List<SearchResult> BuildDefaultPagedResults()
        {
            if (_showAllResults) return new List<SearchResult>(_defaultSearchResults);
            if (!searchUIConfig.GroupResultsByType)
            {
                int limit = AddResultCount(ShowMorePageSize, _expandedUngrouped);
                var visible = _defaultSearchResults.Take(limit).ToList();
                if (_defaultSearchResults.Count > limit) visible.Add(new ShowMoreResult(ResultType.All));
                return visible;
            }

            var limits = new Dictionary<ResultType, int>();
            var counts = new Dictionary<ResultType, int>();
            var grouped = new List<SearchResult>();
            foreach (var item in _defaultSearchResults)
            {
                if (!limits.ContainsKey(item.Type))
                {
                    _expandedByType.TryGetValue(item.Type, out int extra);
                    limits[item.Type] = AddResultCount(ShowMorePageSize, extra);
                }
                counts.TryGetValue(item.Type, out int count);
                counts[item.Type] = count + 1;
                if (count < limits[item.Type]) grouped.Add(item);
            }
            foreach (var entry in counts)
            {
                if (entry.Value <= limits[entry.Key]) continue;
                int last = grouped.FindLastIndex(item => item.Type == entry.Key);
                if (last >= 0) grouped.Insert(last + 1, new ShowMoreResult(entry.Key));
            }
            return grouped;
        }

        private void UpdateResultsList(List<SearchResult> searchResults, bool preservePosition = false, int previousScroll = 0, int previousSelection = -1)
        {
            // A pending select-all replaces the selection after the refresh, so
            // comparing every previously selected item would be unnecessary quadratic work.
            var oldSelection = preservePosition && !_selectAllAfterSearch.HasValue ? resultsListBox.SelectedItems : null;
            var oldFocused = preservePosition ? resultsListBox.SelectedItem : null;
            var oldAnchor = preservePosition && resultsListBox.SelectionAnchor >= 0 && resultsListBox.SelectionAnchor < resultsListBox.Items.Count
                ? resultsListBox.Items[resultsListBox.SelectionAnchor] : null;
            var results = searchResults ?? new List<SearchResult>();
            Debug.WriteLine($"[SearchBar] UpdateResultsList: Received {results.Count} results.");

            if (searchUIConfig.GroupResultsByType && searchUIConfig.ShowTypeHeaders && results.Count > 0)
            {
                var newResults = new List<SearchResult>();

                if (results[0].IsTopMatch)
                {
                    newResults.Add(new HeaderResult("Top Match"));
                }

                bool isDefaultView = string.IsNullOrWhiteSpace(searchBox.Text);

                if (isDefaultView) // Default results view has custom ordering
                {
                    Type previousResultType = null;
                    foreach (var result in results)
                    {
                        if (result is ShowMoreResult)
                        {
                            newResults.Add(result);
                            continue;
                        }
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
                        if (result is ShowMoreResult)
                        {
                            newResults.Add(result);
                            continue;
                        }
                        if (!result.IsTopMatch && result.Type != currentType)
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

            if (isEmbedded)
            {
                resultsListBox.Items = results;
                if (preservePosition)
                {
                    RestoreResultSelection(results, oldSelection, oldFocused, oldAnchor, previousSelection);
                    resultsListBox.SetScrollPosition(previousScroll);
                }
                if (compactPanel) UpdateResultsPopup();
                else resultsListBox.Visible = results.Count > 0;
                ApplyPendingSelectAll();
                if (!compactPanel && !_restoringExpandedSearch) Plugin.SaveExpandedSearchState(this);
                return;
            }

            var mainPanel = resultsListBox.Parent as Panel;
            if (mainPanel == null) return;

            var searchContainer = searchBox.Parent as Panel;
            if (searchContainer == null) return;

            // Calculate height based on actual, scaled control dimensions.
            int nonListHeight = searchContainer.Height + spacerPanel.Height + mainPanel.Padding.Vertical
                + (searchUIConfig.ShowSearchToolbar ? searchToolbar.Height : 0)
                + (isDetached ? dragPanel.Height : 0);

            int listHeight = 0;
            int visibleItemCount = 0;
            for (int i = 0; i < results.Count && visibleItemCount < searchUIConfig.MaxResultsVisible; i++)
            {
                var currentItem = results[i];
                int itemHeight = resultsListBox.GetItemHeight(currentItem, i);
                
                listHeight += itemHeight;

                // Headers don't count towards the MaxResultsVisible limit
                if (currentItem.Type != ResultType.Header)
                {
                    visibleItemCount++;
                }
            }

            if (results.Count > 0)
            {
                if (!spacerPanel.Visible) spacerPanel.Visible = true;
                if (!resultsListBox.Visible) resultsListBox.Visible = true;

                int desiredHeight = nonListHeight + spacerPanel.Height + listHeight;
                int maxAllowedHeight;
                const int margin = 20; // A small margin from the bottom edge.

                if (isDetached)
                {
                    // In detached mode, limit to screen working area.
                    var screen = Screen.FromControl(this);
                    maxAllowedHeight = screen.WorkingArea.Bottom - this.Top - margin;
                }
                else
                {
                    int mbWindowBottom = 0;
                    bool isMbWindowVisible = false;

                    // Synchronously invoke on the MusicBee main thread to get window bounds safely.
                    musicBeeContext.Send(state =>
                    {
                        var mbForm = musicBeeControl?.FindForm();
                        if (mbForm != null && mbForm.WindowState != FormWindowState.Minimized)
                        {
                            isMbWindowVisible = true;
                            var mbScreenRect = musicBeeControl.RectangleToScreen(musicBeeControl.ClientRectangle);
                            mbWindowBottom = mbScreenRect.Bottom;
                        }
                    }, null);

                    if (isMbWindowVisible)
                    {
                        // Limit the height to the MusicBee window's bounds.
                        maxAllowedHeight = mbWindowBottom - this.Top - margin;
                    }
                    else
                    {
                        // Fallback: Limit the height to the screen's working area.
                        var screen = Screen.FromControl(this);
                        maxAllowedHeight = screen.WorkingArea.Bottom - this.Top - margin;
                    }
                }
                
                Height = Math.Min(desiredHeight, maxAllowedHeight);
            }
            else
            {
                if (spacerPanel.Visible) spacerPanel.Visible = false;
                if (resultsListBox.Visible) resultsListBox.Visible = false;
                
                Height = nonListHeight;
            }

            resultsListBox.Items = results;
            if (preservePosition)
            {
                RestoreResultSelection(results, oldSelection, oldFocused, oldAnchor, previousSelection);
                resultsListBox.SetScrollPosition(previousScroll);
            }
            ApplyPendingSelectAll();
        }

        private static bool SameResult(SearchResult a, SearchResult b)
        {
            if (a == null || b == null || a.Type != b.Type || a.IsTopMatch != b.IsTopMatch) return false;
            if (a is SongResult song && b is SongResult otherSong)
                return string.Equals(song.Filepath, otherSong.Filepath, StringComparison.OrdinalIgnoreCase);
            if (a is AlbumResult album && b is AlbumResult otherAlbum)
                return string.Equals(album.Album, otherAlbum.Album, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(album.AlbumArtist, otherAlbum.AlbumArtist, StringComparison.OrdinalIgnoreCase);
            if (a is ArtistResult artist && b is ArtistResult otherArtist)
                return string.Equals(artist.Artist, otherArtist.Artist, StringComparison.OrdinalIgnoreCase);
            if (a is PlaylistResult playlist && b is PlaylistResult otherPlaylist)
                return string.Equals(playlist.PlaylistPath, otherPlaylist.PlaylistPath, StringComparison.OrdinalIgnoreCase);
            return ReferenceEquals(a, b);
        }

        private void RestoreResultSelection(List<SearchResult> results, List<SearchResult> selected, SearchResult focused, SearchResult anchor, int fallback)
        {
            int focusIndex = results.FindIndex(r => SameResult(r, focused));
            if (focusIndex < 0 && fallback >= 0 && fallback < results.Count && results[fallback].Type != ResultType.Header)
                focusIndex = fallback;
            if (focusIndex < 0) return;
            int[] indices = selected == null ? null : results.Select((r, i) => new { r, i })
                .Where(x => selected.Any(old => SameResult(old, x.r))).Select(x => x.i).ToArray();
            int anchorIndex = results.FindIndex(r => SameResult(r, anchor));
            resultsListBox.RestoreSelection(indices, focusIndex, anchorIndex);
        }
    }
}
