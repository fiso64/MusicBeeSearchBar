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

    public class ArtistEntity
    {
        // A representative name for the entity, mainly for grouping.
        public string CanonicalArtistName { get; }

        // Maps a normalized alias back to the original (Artist, SortArtist) pair it came from.
        // Key: Normalized alias (e.g., "artist mr"), Value: original ("Mr. Artist", "Artist, Mr.")
        public Dictionary<string, (string Artist, string SortArtist)> AliasMap { get; }

        public ArtistEntity(string canonicalArtistName)
        {
            CanonicalArtistName = canonicalArtistName;
            AliasMap = new Dictionary<string, (string Artist, string SortArtist)>();
        }
    }

    public class Track
    {
        public string TrackTitle;
        public string Artist; // artists with artist role, joined with ;
        public string Artists; // all artists joined with ; (artist, performer, remixer, etc)
        public string SortArtist;
        public string Album;
        public string AlbumArtist;
        public string SortAlbumArtist;
        public string Filepath;

        static MetaDataType[] fields = new MetaDataType[]
        {
            MetaDataType.TrackTitle,
            MetaDataType.ArtistsWithArtistRole,
            MetaDataType.Artists,
            MetaDataType.SortArtist,
            MetaDataType.Album,
            MetaDataType.AlbumArtist,
            MetaDataType.SortAlbumArtist,

            MetaDataType.ArtistsWithGuestRole,
            MetaDataType.ArtistsWithPerformerRole,
            MetaDataType.ArtistsWithRemixerRole,
        };

        public Track() { }

        public Track(string filepath)
        {
            Filepath = filepath;
            mbApi.Library_GetFileTags(filepath, fields, out string[] results);

            if (results == null || results.Length != fields.Length)
                return;

            TrackTitle = results[0];
            Artist = results[1];
            SortArtist = results[3];
            Album = results[4];
            AlbumArtist = results[5];
            SortAlbumArtist = results[6];

            Artists = string.Join("; ", new[] { Artist, results[7], RemoveCustomRoles(results[8]), results[9] }.Where(s => !string.IsNullOrEmpty(s)));

        }

        public Track(Track other)
        {
            TrackTitle = other.TrackTitle;
            Artist = other.Artist;
            Artists = other.Artists;
            SortArtist = other.SortArtist;
            Album = other.Album;
            AlbumArtist = other.AlbumArtist;
            SortAlbumArtist = other.SortAlbumArtist;
            Filepath = other.Filepath;
        }

        // Removes custom roles of the form "Artist (Role)"
        private string RemoveCustomRoles(string performers)
        {
            if (string.IsNullOrEmpty(performers))
                return performers;

            var performerArr = performers.Split(';');

            for (int i = 0; i < performerArr.Length; i++)
            {
                string cleaned = performerArr[i].Trim();
                if (cleaned.Length >= 5 && cleaned[cleaned.Length - 1] == ')')
                {
                    int parenIndex = cleaned.LastIndexOf('(');
                    if (parenIndex >= 2 && cleaned[parenIndex - 1] == ' ')
                    {
                        performerArr[i] = cleaned.Substring(0, parenIndex - 1);
                    }
                }
                else
                {
                    performerArr[i] = cleaned;
                }
            }

            return string.Join("; ", performerArr);
        }
    }

    public class Database
    {
        public List<ArtistEntity> Artists;
        public Dictionary<(string NormalizedAlbum, string NormalizedAlbumArtist), Track> Albums;
        public Dictionary<Track, (string NormalizedTitle, string NormalizedArtists)> Songs;

        public Database(IEnumerable<Track> tracks, ResultType enabledTypes)
        {
            // cache normalized strings, because NormalizeString is expensive.
            var normalizationCache = new Dictionary<string, string>();
            string getNormalized(string input)
            {
                if (string.IsNullOrEmpty(input))
                {
                    return string.Empty;
                }
                if (!normalizationCache.TryGetValue(input, out var normalized))
                {
                    normalized = SearchService.NormalizeString(input);
                    normalizationCache[input] = normalized;
                }
                return normalized;
            }

            Artists = new List<ArtistEntity>();
            Albums = new Dictionary<(string, string), Track>();
            Songs = new Dictionary<Track, (string, string)>();

            if ((enabledTypes & ResultType.Artist) != 0)
            {
                var tempArtistGroups = new Dictionary<string, HashSet<(string Artist, string SortArtist)>>();
                foreach (var track in tracks)
                {
                    PopulateArtistGroups(track.Artists, track.SortArtist, getNormalized, tempArtistGroups);
                    PopulateArtistGroups(track.AlbumArtist, track.SortAlbumArtist, getNormalized, tempArtistGroups);
                }

                foreach (var group in tempArtistGroups.Values)
                {
                    if (!group.Any()) continue;
                    
                    var canonicalArtistName = group.First().Artist;
                    var entity = new ArtistEntity(canonicalArtistName);

                    foreach (var pair in group)
                    {
                        if (!string.IsNullOrEmpty(pair.Artist))
                        {
                            entity.AliasMap[getNormalized(pair.Artist)] = pair;
                        }

                        if (!string.IsNullOrEmpty(pair.SortArtist))
                        {
                            var normalizedArtist = getNormalized(pair.Artist);
                            var normalizedSortArtist = getNormalized(pair.SortArtist);
                            if (normalizedArtist != normalizedSortArtist)
                            {
                                entity.AliasMap[normalizedSortArtist] = pair;
                            }
                        }
                    }
                    Artists.Add(entity);
                }
            }

            if ((enabledTypes & ResultType.Album) != 0 || (enabledTypes & ResultType.Song) != 0)
            {
                foreach (var track in tracks)
                {
                    if ((enabledTypes & ResultType.Album) != 0 && !string.IsNullOrEmpty(track.Album))
                    {
                        var key = (
                            NormalizedAlbum: getNormalized(track.Album),
                            NormalizedAlbumArtist: getNormalized(track.AlbumArtist)
                        );
                        if (!Albums.ContainsKey(key))
                        {
                            Albums[key] = track;
                        }
                    }

                    if ((enabledTypes & ResultType.Song) != 0 && !string.IsNullOrEmpty(track.TrackTitle))
                    {
                        var value = (
                            NormalizedTitle: getNormalized(track.TrackTitle),
                            NormalizedArtists: getNormalized(track.Artists)
                        );
                        Songs[track] = value;
                    }
                }
            }
        }


        private void PopulateArtistGroups(string artists, string sortArtists, Func<string, string> getNormalizedFunc, Dictionary<string, HashSet<(string Artist, string SortArtist)>> artistGroups)
        {
            if (string.IsNullOrEmpty(artists)) return;

            var splitArtists = artists.Split(';');

            var splitSortArtists = !string.IsNullOrEmpty(sortArtists) ? sortArtists.Split(';') : null;

            bool assignSortArtists = splitSortArtists != null && splitSortArtists.Length == splitArtists.Length;

            for (int i = 0; i < splitArtists.Length; i++)
            {
                string artist = splitArtists[i]?.Trim();
                if (string.IsNullOrEmpty(artist)) continue;

                string sortArtist = null;
                if (assignSortArtists)
                {
                    sortArtist = splitSortArtists[i]?.Trim();
                }

                string normalizedArtistKey = getNormalizedFunc(artist);

                if (!artistGroups.TryGetValue(normalizedArtistKey, out var group))
                {
                    group = new HashSet<(string Artist, string SortArtist)>();
                    artistGroups[normalizedArtistKey] = group;
                }
                
                group.Add((artist, sortArtist));
            }
        }
    }

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
                string normalizedQuery = NormalizeString(query);
                string[] queryWords = normalizedQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (enabledTypes.HasFlag(ResultType.Artist) && GetResultLimit(ResultType.Artist, out var limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var artistResults = SearchArtists(queryWords, normalizedQuery, limit);
                    results.AddRange(artistResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Album) && GetResultLimit(ResultType.Album, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var albumResults = SearchAlbums(queryWords, normalizedQuery, limit);
                    results.AddRange(albumResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Song) && GetResultLimit(ResultType.Song, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var songResults = SearchSongs(queryWords, normalizedQuery, limit);
                    results.AddRange(songResults);
                    onResultsUpdate?.Invoke(OrderResults(results, normalizedQuery));
                }

                if (enabledTypes.HasFlag(ResultType.Playlist) && GetResultLimit(ResultType.Playlist, out limit, resultLimits))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var playlistResults = SearchPlaylists(queryWords, normalizedQuery, limit);
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

        private List<ArtistResult> SearchArtists(string[] queryWords, string normalizedQuery, int limit)
        {
            var scoredArtists = new List<ArtistResult>();

            foreach (var entity in db.Artists)
            {
                double bestScore = 0;
                string winningAlias = null;

                foreach (var alias in entity.AliasMap.Keys)
                {
                    if (QueryMatchesWords(alias, queryWords, normalizeText: false))
                    {
                        double currentScore = CalculateGeneralItemScore(alias, normalizedQuery, queryWords, normalizeStrings: false);
                        if (currentScore > bestScore)
                        {
                            bestScore = currentScore;
                            winningAlias = alias;
                        }
                    }
                }

                if (bestScore > 0 && winningAlias != null)
                {
                    scoredArtists.Add(new ArtistResult(entity.AliasMap[winningAlias].Artist, entity.AliasMap[winningAlias].SortArtist, bestScore));
                }
            }

            return scoredArtists
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .ToList();
        }

        private List<AlbumResult> SearchAlbums(string[] queryWords, string normalizedQuery, int limit)
        {
            return db.Albums
                .Where(x => QueryMatchesWords(x.Key.NormalizedAlbumArtist + " " + x.Key.NormalizedAlbum, queryWords, normalizeText: false))
                .Select(x => new
                {
                    Track = x.Value,
                    Score = CalculateArtistAndTitleScore(x.Key.NormalizedAlbumArtist, x.Key.NormalizedAlbum, normalizedQuery, queryWords, normalizeStrings: false)
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new AlbumResult(x.Track.Album, x.Track.AlbumArtist, x.Track.SortAlbumArtist, x.Score))
                .ToList();
        }

        private List<SongResult> SearchSongs(string[] queryWords, string normalizedQuery, int limit)
        {
            return db.Songs
                .Where(x => QueryMatchesWords(x.Value.NormalizedArtists + " " + x.Value.NormalizedTitle, queryWords, normalizeText: false))
                .Select(x => new
                {
                    Track = x.Key,
                    Score = CalculateArtistAndTitleScore(x.Value.NormalizedArtists, x.Value.NormalizedTitle, normalizedQuery, queryWords, normalizeStrings: false)
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new SongResult(x.Track, x.Score))
                .ToList();
        }

        private List<PlaylistResult> SearchPlaylists(string[] queryWords, string normalizedQuery, int limit)
        {
            return GetAllPlaylists()
                .Where(p => !string.IsNullOrEmpty(p.Name) && QueryMatchesWords(p.Name, queryWords))
                .Select(p => new
                {
                    Playlist = p,
                    Score = CalculateGeneralItemScore(p.Name, normalizedQuery, queryWords)
                })
                .OrderByDescending(x => x.Score)
                .Take(limit)
                .Select(x => new PlaylistResult(x.Playlist.Name, x.Playlist.Path, x.Score))
                .ToList();
        }

        private bool QueryMatchesWords(string text, string[] queryWords, bool normalizeText = true) // TODO: Disallow matching a single text part multiple times
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (queryWords.Length == 0) return true;
            if (!config.EnableContainsCheck) return true;

            if (normalizeText)
                text = NormalizeString(text);

            string spacelessText = text.Replace(" ", "");

            return queryWords.All(word => spacelessText.Contains(word));
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
                query = NormalizeString(query);
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
                query = NormalizeString(query);
            }

            titleScore = string.IsNullOrEmpty(title) ? 0 : FuzzySearch.FuzzyScoreNgram(title, query, queryWords);
            artistScore = string.IsNullOrEmpty(artist) ? 0 : FuzzySearch.FuzzyScoreNgram(artist, query, queryWords);

            return titleScore + artistScore * 0.5;
        }

        static readonly HashSet<char> punctuation = new HashSet<char>
        {
            '!', '?', '(', ')', '.', ',', '-', ':', ';', '[', ']',
            '{', '}', '/', '\\', '+', '=', '*', '&', '#', '@', '$',
            '%', '^', '|', '~', '<', '>', '`', '"'
        };

        // Convert to lower, remove apostrophes, replace punctuation chars with space, remove consecutive spaces, trim. 
        public static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            char[] inputChars = input.ToCharArray();
            char[] outputChars = new char[input.Length];
            int outputIndex = 0;
            bool previousIsSpace = true;

            for (int i = 0; i < inputChars.Length; i++)
            {
                char c = inputChars[i];

                if (c == '\'')
                    continue;

                char current = char.ToLower(c);

                if (punctuation.Contains(current) || current == ' ')
                {
                    if (!previousIsSpace)
                    {
                        outputChars[outputIndex++] = ' ';
                        previousIsSpace = true;
                    }
                }
                else
                {
                    outputChars[outputIndex++] = current;
                    previousIsSpace = false;
                }
            }

            if (outputIndex > 0 && outputChars[outputIndex - 1] == ' ')
                outputIndex--;

            return new string(outputChars, 0, outputIndex);
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
                    res.Add((name, path));
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
                string normalizedQuery = NormalizeString(commandQuery);
                string[] queryWords = normalizedQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (queryWords.Length == 0)
                {
                    finalFilteredResults = combinedResults.Cast<SearchResult>();
                }
                else
                {
                    finalFilteredResults = combinedResults.Where(cr =>
                    {
                        if (cancellationToken.IsCancellationRequested) return false;

                        bool titleMatches = cr.DisplayTitle != null &&
                                            QueryMatchesWords(cr.DisplayTitle, queryWords, true);

                        if (titleMatches) return true;

                        // For built-in commands, also check the enum name if DisplayTitle didn't match
                        if (cr.Command.HasValue)
                        {
                            return QueryMatchesWords(cr.Command.Value.ToString(), queryWords, true);
                        }
                        return false;
                    }).Cast<SearchResult>();
                }
            }

            return finalFilteredResults.OrderBy(r => r.DisplayDetail == "Plugin Command" ? 1 : 0)
                .ThenBy(r => r.DisplayTitle)
                .ToList();
        }
    }
}
