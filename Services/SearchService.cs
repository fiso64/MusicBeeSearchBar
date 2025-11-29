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
                    var artistResults = SearchArtists(sortedQueryWords, normalizedQuery, limit);
                    results.AddRange(artistResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Album) && GetResultLimit(ResultType.Album, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var albumResults = SearchAlbums(sortedQueryWords, normalizedQuery, limit);
                    results.AddRange(albumResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Song) && GetResultLimit(ResultType.Song, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var songResults = SearchSongs(sortedQueryWords, normalizedQuery, limit);
                    results.AddRange(songResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Playlist) && GetResultLimit(ResultType.Playlist, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var playlistResults = SearchPlaylists(sortedQueryWords, normalizedQuery, limit);
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

        private List<ArtistResult> SearchArtists(string[] sortedQueryWords, string normalizedQuery, int limit)
        {
            var scoredArtists = new List<ArtistResult>();

            foreach (var entity in db.Artists)
            {
                double bestScore = 0;
                string winningAlias = null;

                var aliasesToScore = entity.AliasMap.Keys.AsEnumerable();
                if (config.EnableContainsCheck)
                    aliasesToScore = aliasesToScore.Where(alias => QueryMatchesWords(alias, sortedQueryWords, normalizeText: false));

                foreach (var alias in aliasesToScore)
                {
                    double currentScore = CalculateGeneralItemScore(alias, normalizedQuery, sortedQueryWords, normalizeStrings: false);
                    if (currentScore > bestScore)
                    {
                        bestScore = currentScore;
                        winningAlias = alias;
                    }
                }

                if (bestScore > 0 && winningAlias != null)
                {
                    if (config.ArtistScoreMultiplier != 1.0)
                        bestScore *= config.ArtistScoreMultiplier;

                    scoredArtists.Add(new ArtistResult(entity.AliasMap[winningAlias].Artist, entity.AliasMap[winningAlias].SortArtist, bestScore));
                }
            }

            return scoredArtists
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
        }

        private List<AlbumResult> SearchAlbums(string[] sortedQueryWords, string normalizedQuery, int limit)
        {
            var items = db.Albums.AsEnumerable();
            double multiplier = config.AlbumScoreMultiplier;

            if (config.EnableContainsCheck)
                items = items.Where(x => QueryMatchesWords(x.NormalizedAlbumArtist + " " + x.NormalizedAlbumName, sortedQueryWords, normalizeText: false));

            return items
                .Select(x => new
                {
                    Entry = x,
                    Score = CalculateArtistAndTitleScore(x.NormalizedAlbumArtist, x.NormalizedAlbumName, normalizedQuery, sortedQueryWords, normalizeStrings: false) * (multiplier != 1.0 ? multiplier : 1.0)
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new AlbumResult(x.Entry.Track.Album, x.Entry.Track.AlbumArtist, x.Entry.Track.SortAlbumArtist, x.Score))
                .ToList();
        }

        private List<SongResult> SearchSongs(string[] sortedQueryWords, string normalizedQuery, int limit)
        {
            var items = db.Songs.AsEnumerable();
            double multiplier = config.SongScoreMultiplier;

            if (config.EnableContainsCheck)
                items = items.Where(x => QueryMatchesWords(x.NormalizedArtists + " " + x.NormalizedTitle, sortedQueryWords, normalizeText: false));

            return items
                .Select(x => new
                {
                    Entry = x,
                    Score = CalculateArtistAndTitleScore(x.NormalizedArtists, x.NormalizedTitle, normalizedQuery, sortedQueryWords, normalizeStrings: false) * (multiplier != 1.0 ? multiplier : 1.0)
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new SongResult(x.Entry.Track, x.Score))
                .ToList();
        }

        private List<PlaylistResult> SearchPlaylists(string[] sortedQueryWords, string normalizedQuery, int limit)
        {
            var items = GetAllPlaylists().AsEnumerable();
            double multiplier = config.PlaylistScoreMultiplier;

            if (config.EnableContainsCheck)
                items = items.Where(p => QueryMatchesWords(p.Name, sortedQueryWords));

            return items
                .Select(p => new
                {
                    Playlist = p,
                    Score = CalculateGeneralItemScore(p.Name, normalizedQuery, sortedQueryWords) * (multiplier != 1.0 ? multiplier : 1.0)
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
                Debug.WriteLine($"Error fetching plugin commands: {ex.Message}");
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
