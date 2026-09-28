using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.Services
{
    [Flags]
    public enum ResultType
    {
        Song = 1,
        Album = 2,
        Artist = 4,
        Playlist = 8,
        Command = 16,
        Header = 32,
        All = 31,
    }

    public class SearchResult
    {
        public string DisplayTitle;
        public string DisplayDetail;
        public ResultType Type;
        public bool IsTopMatch = false;
        public double Score;
    }

    public class SongResult : SearchResult
    {
        public string TrackTitle => Track.TrackTitle;
        public string Artist => Track.Artist;
        public string SortArtist => Track.SortArtist;
        public string Filepath => Track.Filepath;

        public Track Track;

        public SongResult(Track track, double score = 0)
        {
            Track = track;
            DisplayTitle = TrackTitle;
            DisplayDetail = Artist;
            Type = ResultType.Song;
            Score = score;
        }

        public static SongResult FromSearchResult(SearchResult result)
        {
            return (SongResult)result;
        }
    }

    public class AlbumResult : SearchResult
    {
        public string Album;
        public string AlbumArtist;
        public string SortAlbumArtist;

        public AlbumResult(string album, string albumArtist, string sortAlbumArtist, double score = 0)
        {
            Album = album;
            AlbumArtist = albumArtist;
            DisplayTitle = album;
            DisplayDetail = albumArtist;
            SortAlbumArtist = sortAlbumArtist;
            Type = ResultType.Album;
            Score = score;
        }

        public AlbumResult(SongResult songResult) 
            : this(songResult.Track.Album, songResult.Track.AlbumArtist, songResult.Track.SortAlbumArtist)
        {
        }

        public static AlbumResult FromSearchResult(SearchResult result)
        {
            if (result is AlbumResult albumResult)
                return albumResult;
            if (result is SongResult songResult)
                return new AlbumResult(songResult);
            return null;
        }
    }

    public class ArtistResult : SearchResult
    {
        public string Artist;
        public string SortArtist;

        public ArtistResult(string artist, string sortArtist, double score = 0)
        {
            Artist = artist;
            SortArtist = sortArtist;
            Type = ResultType.Artist;
            DisplayTitle = Artist;
            Score = score;

            //if (!Artist.Equals(SortArtist, StringComparison.OrdinalIgnoreCase))
            //    DisplayDetail = SortArtist;
        }

        public ArtistResult(AlbumResult albumResult) : this(albumResult.AlbumArtist, albumResult.SortAlbumArtist)
        {
        }

        public ArtistResult(SongResult songResult) : this(songResult.Track.AlbumArtist, songResult.Track.SortAlbumArtist)
        {
        }

        public static ArtistResult FromSearchResult(SearchResult result)
        {
            if (result is ArtistResult artistResult)
                return artistResult;
            if (result is AlbumResult albumResult)
                return new ArtistResult(albumResult);
            if (result is SongResult songResult)
                return new ArtistResult(songResult);
            return null;
        }
    }

    public class AlbumArtistResult : ArtistResult
    {
        public AlbumArtistResult(string artist, string sortArtist) : base(artist, sortArtist)
        {
        }
    }

    public class PlaylistResult : SearchResult
    {
        public string PlaylistName;
        public string PlaylistPath;

        public PlaylistResult(string playlistName, string playlistPath, double score = 0)
        {
            PlaylistName = playlistName;
            PlaylistPath = playlistPath;
            Type = ResultType.Playlist;
            DisplayTitle = PlaylistName;
            DisplayDetail = "Playlist";
            Score = score;
        }
    }

    public class CommandResult : SearchResult
    {
        public ApplicationCommand? Command; // Nullable for plugin commands
        public string PluginCommandName; // For plugin commands

        private CommandResult() { }

        public static CommandResult CreateBuiltinResult(ApplicationCommand command, string displayName)
        {
            var res = new CommandResult()
            { 
                Command = command,
                PluginCommandName = null,
                DisplayTitle = displayName,
                DisplayDetail = "Command",
                Type = ResultType.Command,            
            };
            return res;
        }

        public static CommandResult CreatePluginResult(string pluginCommandName)
        {
            var res = new CommandResult()
            {
                Command = null,
                PluginCommandName = pluginCommandName,
                DisplayTitle = pluginCommandName,
                DisplayDetail = "Plugin Command",
                Type = ResultType.Command,
            };
            return res;
        }
    }

    public class HeaderResult : SearchResult
    {
        public HeaderResult(string title)
        {
            DisplayTitle = title;
            Type = ResultType.Header;
        }
    }

    // Track and Database classes moved to Services/Database.cs

    public class SearchService
    {
        private Database db;
        private MusicBeeApiInterface mbApi;
        private Config.SearchUIConfig config;
        public bool IsLoaded { get; private set; } = false;

        // Incremental-narrowing cache: while EnableContainsCheck is on and the user keeps
        // extending the same query (pure append), each extra character only makes the
        // per-word substring check stricter - so anything that already failed to contain the
        // shorter query can never contain the longer one. That lets each keystroke re-filter
        // just the previous keystroke's surviving candidates instead of rescanning the whole
        // library. Falls back to a full scan whenever the query isn't a simple extension of
        // the cached one (an edit, a paste, backspacing, ...) or the library was reloaded.
        //
        // Each cache is tagged with the exact Database instance it was built from (captured
        // once per search, not read live off the `db` field) rather than relying on a reset
        // at reload time. A reload doesn't cancel a search that's already in flight, so
        // without this a stale search could finish after the reset and repopulate the cache
        // with entries from the just-replaced library.
        private class CandidateCache<T>
        {
            public Database Db;
            public string Query;
            public List<T> Candidates;
        }

        private readonly object _cacheLock = new object();
        private readonly CandidateCache<ArtistEntry> _artistCache = new CandidateCache<ArtistEntry>();
        private readonly CandidateCache<AlbumEntry> _albumCache = new CandidateCache<AlbumEntry>();
        private readonly CandidateCache<SongEntry> _songCache = new CandidateCache<SongEntry>();
        private readonly CandidateCache<(string Name, string Path)> _playlistCache = new CandidateCache<(string Name, string Path)>();

        // Returns the cached candidates for `scanDb`/`normalizedQuery` if they're usable,
        // or null if this search needs to fall back to a full scan.
        private List<T> TryGetCandidateSource<T>(CandidateCache<T> cache, Database scanDb, string normalizedQuery)
        {
            lock (_cacheLock)
            {
                if (config.EnableContainsCheck
                    && cache.Db == scanDb
                    && cache.Query != null
                    && normalizedQuery.StartsWith(cache.Query, StringComparison.Ordinal))
                {
                    return cache.Candidates;
                }
                return null;
            }
        }

        // Tags the cache with the Database this scan actually ran against (not whatever
        // `db` currently is - a concurrent reload may have already moved it on) so a later
        // search can never mistake candidates from a different library generation as valid.
        private void UpdateCandidateCache<T>(CandidateCache<T> cache, Database scanDb, List<T> matched, string normalizedQuery)
        {
            lock (_cacheLock)
            {
                if (config.EnableContainsCheck)
                {
                    cache.Db = scanDb;
                    cache.Query = normalizedQuery;
                    cache.Candidates = matched;
                }
                else
                {
                    cache.Db = null;
                    cache.Query = null;
                    cache.Candidates = null;
                }
            }
        }

        public SearchService(MusicBeeApiInterface mbApi, Config.SearchUIConfig config)
        {
            this.mbApi = mbApi;
            this.config = config;
        }

        public async Task LoadTracksAsync()
        {
            await Task.Run(() => {
                //var tracks = Tests.SyntheticDataTests.GenerateSyntheticDatabase(1000000).Result;

                var sw = Stopwatch.StartNew();

                mbApi.Library_QueryFilesEx("", out string[] files);
                if (files == null) files = Array.Empty<string>();

                sw.Stop();
                Debug.WriteLine($"Library_QueryFilesEx completed in {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                var tracks = files.Select(filepath => new Track(filepath)).ToArray();

                sw.Stop();
                Debug.WriteLine($"Tracks loaded in {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                db = new Database(tracks, GetEnabledTypes());
                IsLoaded = true;

                // Not strictly required for correctness (each cache entry is tagged with the
                // Database it was built from, so a stale one is simply never matched again),
                // but drops the old lists promptly instead of waiting for them to be
                // overwritten by the next contains-check search.
                lock (_cacheLock)
                {
                    _artistCache.Db = null; _artistCache.Query = null; _artistCache.Candidates = null;
                    _albumCache.Db = null; _albumCache.Query = null; _albumCache.Candidates = null;
                    _songCache.Db = null; _songCache.Query = null; _songCache.Candidates = null;
                    _playlistCache.Db = null; _playlistCache.Query = null; _playlistCache.Candidates = null;
                }

                sw.Stop();
                Debug.WriteLine($"Database created in {sw.ElapsedMilliseconds}ms");
            });
        }

        public async Task<List<SearchResult>> SearchIncrementalAsync(
            string query, 
            ResultType enabledTypes, 
            CancellationToken cancellationToken, 
            Action<List<SearchResult>> onResultsUpdate,
            Dictionary<ResultType, int> resultLimits = null)
        {
            if (!IsLoaded)
            {
                var results = new List<SearchResult>();
                onResultsUpdate?.Invoke(results);
                return results;
            }

            return await Task.Run(async () => {
                var results = new List<SearchResult>();
                string normalizedQuery = Database.NormalizeString(query);
                string[] sortedQueryWords = normalizedQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Array.Sort(sortedQueryWords, (a, b) => b.Length.CompareTo(a.Length));

                if (enabledTypes.HasFlag(ResultType.Artist) && GetResultLimit(ResultType.Artist, out var limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var artistResults = SearchArtists(sortedQueryWords, normalizedQuery, limit, cancellationToken);
                    results.AddRange(artistResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Album) && GetResultLimit(ResultType.Album, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var albumResults = SearchAlbums(sortedQueryWords, normalizedQuery, limit, cancellationToken);
                    results.AddRange(albumResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Song) && GetResultLimit(ResultType.Song, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var songResults = SearchSongs(sortedQueryWords, normalizedQuery, limit, cancellationToken);
                    results.AddRange(songResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Playlist) && GetResultLimit(ResultType.Playlist, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var playlistResults = SearchPlaylists(sortedQueryWords, normalizedQuery, limit, cancellationToken);
                    results.AddRange(playlistResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                return OrderResults(results, normalizedQuery);
            }, cancellationToken);
        }

        private List<SearchResult> OrderResults(List<SearchResult> results, string normalizedQuery)
        {
            if (results.Count == 0)
            {
                return results;
            }

            if (!config.ShowTopMatch || string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return results
                    .OrderByDescending(r => GetResultTypePriority(r.Type))
                    .ThenByDescending(r => r.Score)
                    .ToList();
            }

            var topMatch = results.OrderByDescending(x => x.Score).FirstOrDefault();

            if (topMatch == null)
            {
                return new List<SearchResult>();
            }

            topMatch.IsTopMatch = true;

            var regularResults = results.Where(x => x != topMatch)
                .OrderByDescending(x => GetResultTypePriority(x.Type))
                .ThenByDescending(x => x.Score)
                .ToList();

            var finalList = new List<SearchResult> { topMatch };
            finalList.AddRange(regularResults);

            return finalList;
        }

        private List<ArtistResult> SearchArtists(string[] sortedQueryWords, string normalizedQuery, int limit, CancellationToken cancellationToken)
        {
            var scoredArtists = new List<ArtistResult>();

            // Snapshot db once: a reload landing mid-scan must not change which library
            // generation this search (or its cache write below) is considered to belong to.
            var scanDb = db;
            List<ArtistEntry> searchSource = TryGetCandidateSource(_artistCache, scanDb, normalizedQuery) ?? scanDb.Artists;
            List<ArtistEntry> matchedEntities = config.EnableContainsCheck ? new List<ArtistEntry>() : null;

            foreach (var entity in searchSource)
            {
                // A superseded search (the user kept typing) must actually stop scanning
                // rather than run to completion on its thread-pool thread - otherwise fast
                // typing piles up multiple full-library scans running concurrently.
                cancellationToken.ThrowIfCancellationRequested();

                double bestScore = 0;
                string winningAlias = null;
                bool anyAliasMatched = false;

                var aliasesToScore = entity.AliasMap.Keys.AsEnumerable();
                if (config.EnableContainsCheck)
                    aliasesToScore = aliasesToScore.Where(alias => QueryMatchesWords(alias, sortedQueryWords, normalizeText: false));

                foreach (var alias in aliasesToScore)
                {
                    anyAliasMatched = true;
                    double currentScore = CalculateGeneralItemScore(alias, normalizedQuery, sortedQueryWords, normalizeStrings: false);
                    if (currentScore > bestScore)
                    {
                        bestScore = currentScore;
                        winningAlias = alias;
                    }
                }

                if (anyAliasMatched) matchedEntities?.Add(entity);

                if (bestScore > 0 && winningAlias != null)
                {
                    if (config.ArtistScoreMultiplier != 1.0)
                        bestScore *= config.ArtistScoreMultiplier;

                    scoredArtists.Add(new ArtistResult(entity.AliasMap[winningAlias].Artist, entity.AliasMap[winningAlias].SortArtist, bestScore));
                }
            }

            UpdateCandidateCache(_artistCache, scanDb, matchedEntities, normalizedQuery);

            return scoredArtists
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
        }

        private List<AlbumResult> SearchAlbums(string[] sortedQueryWords, string normalizedQuery, int limit, CancellationToken cancellationToken)
        {
            double multiplier = config.AlbumScoreMultiplier;

            var scanDb = db;
            IReadOnlyList<AlbumEntry> searchSource = TryGetCandidateSource(_albumCache, scanDb, normalizedQuery) ?? scanDb.Albums;
            IEnumerable<AlbumEntry> filtered = searchSource;

            if (config.EnableContainsCheck)
            {
                var matched = new List<AlbumEntry>();
                foreach (var x in searchSource)
                {
                    // See SearchArtists: lets a superseded search stop scanning immediately
                    // instead of scoring the rest of the library for a result nobody will see.
                    cancellationToken.ThrowIfCancellationRequested();
                    if (QueryMatchesWords(x.NormalizedAlbumArtist + " " + x.NormalizedAlbumName, sortedQueryWords, normalizeText: false))
                        matched.Add(x);
                }
                filtered = matched;
                UpdateCandidateCache(_albumCache, scanDb, matched, normalizedQuery);
            }
            else
            {
                UpdateCandidateCache(_albumCache, scanDb, null, normalizedQuery);
            }

            return filtered
                .Select(x =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new
                    {
                        Entry = x,
                        Score = CalculateArtistAndTitleScore(x.NormalizedAlbumArtist, x.NormalizedAlbumName, normalizedQuery, sortedQueryWords, normalizeStrings: false) * (multiplier != 1.0 ? multiplier : 1.0)
                    };
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new AlbumResult(x.Entry.Track.Album, x.Entry.Track.AlbumArtist, x.Entry.Track.SortAlbumArtist, x.Score))
                .ToList();
        }

        private List<SongResult> SearchSongs(string[] sortedQueryWords, string normalizedQuery, int limit, CancellationToken cancellationToken)
        {
            double multiplier = config.SongScoreMultiplier;

            var scanDb = db;
            IReadOnlyList<SongEntry> searchSource = TryGetCandidateSource(_songCache, scanDb, normalizedQuery) ?? scanDb.Songs;
            IEnumerable<SongEntry> filtered = searchSource;

            if (config.EnableContainsCheck)
            {
                var matched = new List<SongEntry>();
                foreach (var x in searchSource)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (QueryMatchesWords(x.NormalizedArtists + " " + x.NormalizedTitle, sortedQueryWords, normalizeText: false))
                        matched.Add(x);
                }
                filtered = matched;
                UpdateCandidateCache(_songCache, scanDb, matched, normalizedQuery);
            }
            else
            {
                UpdateCandidateCache(_songCache, scanDb, null, normalizedQuery);
            }

            return filtered
                .Select(x =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new
                    {
                        Entry = x,
                        Score = CalculateArtistAndTitleScore(x.NormalizedArtists, x.NormalizedTitle, normalizedQuery, sortedQueryWords, normalizeStrings: false) * (multiplier != 1.0 ? multiplier : 1.0)
                    };
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new SongResult(x.Entry.Track, x.Score))
                .ToList();
        }

        private List<PlaylistResult> SearchPlaylists(string[] sortedQueryWords, string normalizedQuery, int limit, CancellationToken cancellationToken)
        {
            double multiplier = config.PlaylistScoreMultiplier;

            // Playlists aren't part of Database, but are tagged against it anyway so a
            // library reload also refreshes the playlist cache, matching prior behavior.
            var scanDb = db;

            // On a cache hit this also skips re-querying MusicBee's playlist list via COM
            // interop for every keystroke, not just the fuzzy-matching cost.
            List<(string Name, string Path)> searchSource = TryGetCandidateSource(_playlistCache, scanDb, normalizedQuery) ?? GetAllPlaylists();
            IEnumerable<(string Name, string Path)> filtered = searchSource;

            if (config.EnableContainsCheck)
            {
                var matched = new List<(string Name, string Path)>();
                foreach (var p in searchSource)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (QueryMatchesWords(p.Name, sortedQueryWords))
                        matched.Add(p);
                }
                filtered = matched;
                UpdateCandidateCache(_playlistCache, scanDb, matched, normalizedQuery);
            }
            else
            {
                UpdateCandidateCache(_playlistCache, scanDb, null, normalizedQuery);
            }

            return filtered
                .Select(p =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return new
                    {
                        Playlist = p,
                        Score = CalculateGeneralItemScore(p.Name, normalizedQuery, sortedQueryWords) * (multiplier != 1.0 ? multiplier : 1.0)
                    };
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new PlaylistResult(x.Playlist.Name, x.Playlist.Path, x.Score))
                .ToList();
        }

        /// <summary>
        /// Checks if all query words can be found as substrings within the given text, with specific rules for matching.
        /// </summary>
        /// <remarks>
        /// This is a "bag of words" substring match with overlap prevention. Its key behaviors are:
        /// 1.  The search is performed on a version of the input `text` that has been normalized and has all spaces removed.
        ///     This allows queries like "foobar" to match the text "foo bar".
        /// 2.  Each character in the source text can only be used once across all matches. This prevents a single
        ///     word in the text (e.g., "artist") from satisfying multiple query words (e.g., "art art").
        /// 3.  To resolve ambiguity where a shorter word is a substring of a longer one (e.g., query "foo foobar" vs. text "foobar"),
        ///     the function matches longer query words first before attempting to match shorter ones.
        /// </remarks>
        /// <param name="text">The source text to search within.</param>
        /// <param name="sortedQueryWords">An array of query words to find within the text. These are assumed to be pre-normalized and pre-sorted by length.</param>
        /// <param name="normalizeText">If true, the input `text` is normalized before comparison.</param>
        /// <returns>True if all query words can be placed in the text without overlapping; otherwise, false.</returns>
        private bool QueryMatchesWords(string text, string[] sortedQueryWords, bool normalizeText = true)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (sortedQueryWords.Length == 0) return true;

            if (normalizeText)
                text = NormalizeString(text);

            string spacelessText = text.Replace(" ", "");
            var usedChars = new bool[spacelessText.Length];

            // Assumes queryWords has been pre-sorted by length, descending.
            foreach (var queryWord in sortedQueryWords)
            {
                int searchFromIndex = 0;
                bool foundMatchForThisWord = false;
                while (true)
                {
                    int matchIndex = spacelessText.IndexOf(queryWord, searchFromIndex);
                    if (matchIndex == -1)
                    {
                        break; // No more occurrences of this query word.
                    }

                    // Check if this match location is available.
                    bool overlaps = false;
                    for (int i = 0; i < queryWord.Length; i++)
                    {
                        if (usedChars[matchIndex + i])
                        {
                            overlaps = true;
                            break;
                        }
                    }

                    if (overlaps)
                    {
                        // This spot is taken, search for the next occurrence.
                        searchFromIndex = matchIndex + 1;
                        continue;
                    }

                    // Found an available spot. Mark it as used and move to the next query word.
                    for (int i = 0; i < queryWord.Length; i++)
                    {
                        usedChars[matchIndex + i] = true;
                    }
                    foundMatchForThisWord = true;
                    break; // Exit while loop for this query word
                }

                if (!foundMatchForThisWord)
                {
                    return false; // A query word could not be placed.
                }
            }

            return true; // All query words were placed successfully.
        }

        private bool GetResultLimit(ResultType type, out int limit, Dictionary<ResultType, int> resultLimits = null)
        {
            if (resultLimits != null)
            {
                if (resultLimits.TryGetValue(ResultType.All, out int allLimit))
                {
                    limit = allLimit;
                    return limit > 0;
                }
                if (resultLimits.TryGetValue(type, out int typeLimit))
                {
                    limit = typeLimit;
                    return limit > 0;
                }
            }

            switch (type)
            {
                case ResultType.Artist: limit = config.ArtistResultLimit; break;
                case ResultType.Album: limit = config.AlbumResultLimit; break;
                case ResultType.Song: limit = config.SongResultLimit; break;
                case ResultType.Playlist: limit = config.PlaylistResultLimit; break;
                default: limit = config.SongResultLimit; break;
            }

            return limit > 0;
        }

        private int GetResultTypePriority(ResultType type)
        {
            if (!config.GroupResultsByType) return 0;
            
            switch (type)
            {
                case ResultType.Command: return 4; // Commands listed first or high priority
                case ResultType.Artist: return 3;
                case ResultType.Album: return 2;
                case ResultType.Song: return 1;
                case ResultType.Playlist: return 0;
                default: return 0;
            }
        }

        private double CalculateGeneralItemScore(string item, string query, string[] queryWords, bool normalizeStrings = true)
        {
            if (string.IsNullOrEmpty(item)) return 0;

            if (normalizeStrings)
            {
                item = NormalizeString(item);
            }

            return FuzzySearch.FuzzyScoreNgram(item, query, queryWords);
        }

        private double CalculateArtistAndTitleScore(string artist, string title, string query, string[] queryWords, bool normalizeStrings = true)
        {
            double artistScore, titleScore;

            if (normalizeStrings)
            {
                title = NormalizeString(title);
                artist = NormalizeString(artist);
            }

            titleScore = string.IsNullOrEmpty(title) ? 0 : FuzzySearch.FuzzyScoreNgram(title, query, queryWords);
            artistScore = string.IsNullOrEmpty(artist) ? 0 : FuzzySearch.FuzzyScoreNgram(artist, query, queryWords);

            return titleScore + artistScore * 0.3;
        }

        // NormalizeString logic moved to Database.cs
        public string NormalizeString(string input)
        {
             return Database.NormalizeString(input);
        }

        public List<SearchResult> GetPlayingItems()
        {
            string path = mbApi.NowPlaying_GetFileUrl();

            if (string.IsNullOrEmpty(path))
                return null;

            var track = new Track(path);

            var res = new List<SearchResult>();
            var artistsInList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(track.Artists))
            {
                var trackArtists = track.Artists.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .DistinctBy(x => x.ToLower());

                foreach (var artist in trackArtists)
                {
                    res.Add(new ArtistResult(artist, artist));
                    artistsInList.Add(artist);
                }
            }

            if (!string.IsNullOrWhiteSpace(track.Album))
                res.Add(new AlbumResult(track.Album, track.AlbumArtist, track.SortAlbumArtist));

            if (!string.IsNullOrWhiteSpace(track.AlbumArtist))
            {
                var albumArtists = track.AlbumArtist.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                var sortAlbumArtists = !string.IsNullOrEmpty(track.SortAlbumArtist) ? track.SortAlbumArtist.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) : null;

                for (int i = 0; i < albumArtists.Length; i++)
                {
                    var albumArtist = albumArtists[i].Trim();
                    if (string.Equals(albumArtist, "Various Artists", StringComparison.OrdinalIgnoreCase) || artistsInList.Contains(albumArtist))
                        continue;

                    string sortAlbumArtist = null;
                    if (sortAlbumArtists != null && i < sortAlbumArtists.Length)
                    {
                        sortAlbumArtist = sortAlbumArtists[i].Trim();
                    }

                    res.Add(new AlbumArtistResult(albumArtist, sortAlbumArtist));
                }
            }

            return res;
        }

        public List<SearchResult> GetSelectedTracks()
        {
            mbApi.Library_QueryFilesEx("domain=SelectedFiles", out string[] files);

            if (files == null || files.Length == 0)
                return new List<SearchResult>();

            var tracks = files.Select(filepath => new Track(filepath)).ToList();

            var results = new List<SearchResult>();
            var artistsInList = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Add track artists
            var trackArtists = tracks
                .Where(t => !string.IsNullOrWhiteSpace(t.Artists))
                .SelectMany(t => t.Artists.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()))
                .DistinctBy(a => a.ToLower());

            foreach (var artist in trackArtists)
            {
                results.Add(new ArtistResult(artist, artist));
                artistsInList.Add(artist);
            }

            // Add albums
            var albums = tracks
                .Where(t => !string.IsNullOrWhiteSpace(t.Album) && !string.IsNullOrWhiteSpace(t.AlbumArtist))
                .DistinctBy(t => $"{t.Album.ToLower()}|{t.AlbumArtist.ToLower()}");

            foreach (var track in albums)
            {
                results.Add(new AlbumResult(track.Album, track.AlbumArtist, track.SortAlbumArtist));
            }
            
            // Add album artists if they are not already in the list
            var albumArtists = tracks
                .Where(t => !string.IsNullOrWhiteSpace(t.AlbumArtist))
                .SelectMany(t =>
                {
                    var artists = t.AlbumArtist.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    var sortArtists = !string.IsNullOrEmpty(t.SortAlbumArtist) ? t.SortAlbumArtist.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) : null;
                    
                    return artists.Select((a, i) => new {
                        Artist = a.Trim(),
                        SortArtist = (sortArtists != null && i < sortArtists.Length) ? sortArtists[i].Trim() : null
                    });
                })
                .DistinctBy(x => x.Artist.ToLower());

            foreach (var albumArtistInfo in albumArtists)
            {
                if (!string.Equals(albumArtistInfo.Artist, "Various Artists", StringComparison.OrdinalIgnoreCase) &&
                    !artistsInList.Contains(albumArtistInfo.Artist))
                {
                    results.Add(new AlbumArtistResult(albumArtistInfo.Artist, albumArtistInfo.SortArtist));
                }
            }

            return results;
        }

        private List<(string Name, string Path)> GetAllPlaylists()
        {
            var res = new List<(string Name, string Path)>();
            if (mbApi.Playlist_QueryPlaylists())
            {
                var path = mbApi.Playlist_QueryGetNextPlaylist();
                while (!string.IsNullOrEmpty(path))
                {
                    var name = mbApi.Playlist_GetName(path);
                    if (!string.IsNullOrEmpty(name))
                    {
                        res.Add((name, path));
                    }
                    path = mbApi.Playlist_QueryGetNextPlaylist();
                }
            }
            return res;
        }

        private ResultType GetEnabledTypes()
        {
            ResultType types = 0;
            
            if (config.ArtistResultLimit > 0)
                types |= ResultType.Artist;
            if (config.AlbumResultLimit > 0)
                types |= ResultType.Album;
            if (config.SongResultLimit > 0)
                types |= ResultType.Song;
            if (config.PlaylistResultLimit > 0)
                types |= ResultType.Playlist;
        
            return types;
        }

        public List<SearchResult> SearchCommands(string commandQuery, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested) return new List<SearchResult>();

            var builtInCommands = ApplicationCommandHelper.GetValidBuiltinCommands(mbApi)
                .OrderBy(r => r.displayName).ToList();

            var combinedResults = builtInCommands.Select(x => CommandResult.CreateBuiltinResult(x.command, x.displayName)).ToList();

            try
            {
                var pluginCommands = MusicBeeHelpers.GetPluginCommands();
                if (pluginCommands != null)
                {
                    foreach (var kvp in pluginCommands)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        
                        // Exclude own commands
                        if (kvp.Key.StartsWith("Modern Search Bar:"))
                            continue;
                        
                        combinedResults.Add(CommandResult.CreatePluginResult(kvp.Key));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error fetching plugin commands", ex);
            }


            if (cancellationToken.IsCancellationRequested) return new List<SearchResult>();

            IEnumerable<SearchResult> finalFilteredResults;

            if (string.IsNullOrWhiteSpace(commandQuery))
            {
                finalFilteredResults = combinedResults.Cast<SearchResult>();
            }
            else
            {
                string normalizedQuery = Database.NormalizeString(commandQuery);
                string[] sortedQueryWords = normalizedQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Array.Sort(sortedQueryWords, (a, b) => b.Length.CompareTo(a.Length));

                if (sortedQueryWords.Length == 0)
                {
                    finalFilteredResults = combinedResults.Cast<SearchResult>();
                }
                else
                {
                    IEnumerable<CommandResult> query = combinedResults;
                    if (config.EnableContainsCheck)
                    {
                        query = query.Where(cr =>
                        {
                            if (cancellationToken.IsCancellationRequested) return false;

                            bool titleMatches = cr.DisplayTitle != null &&
                                                QueryMatchesWords(cr.DisplayTitle, sortedQueryWords, true);

                            if (titleMatches) return true;

                            // For built-in commands, also check the enum name if DisplayTitle didn't match
                            if (cr.Command.HasValue)
                            {
                                return QueryMatchesWords(cr.Command.Value.ToString(), sortedQueryWords, true);
                            }
                            return false;
                        });
                    }
                    finalFilteredResults = query.Cast<SearchResult>();
                }
            }

            return finalFilteredResults.OrderBy(r => r.DisplayDetail == "Plugin Command" ? 1 : 0)
                .ThenBy(r => r.DisplayTitle)
                .ToList();
        }
    }
}
