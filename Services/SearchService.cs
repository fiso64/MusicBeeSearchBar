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
    }

    public class SongResult : SearchResult
    {
        public string TrackTitle => Track.TrackTitle;
        public string Artist => Track.Artist;
        public string SortArtist => Track.SortArtist;
        public string Filepath => Track.Filepath;

        public Track Track;

        public SongResult(Track track)
        {
            Track = track;
            DisplayTitle = TrackTitle;
            DisplayDetail = Artist;
            Type = ResultType.Song;
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

        public AlbumResult(string album, string albumArtist, string sortAlbumArtist)
        {
            Album = album;
            AlbumArtist = albumArtist;
            DisplayTitle = album;
            DisplayDetail = albumArtist;
            SortAlbumArtist = sortAlbumArtist;
            Type = ResultType.Album;
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

        public ArtistResult(string artist, string sortArtist)
        {
            Artist = artist;
            SortArtist = sortArtist;
            Type = ResultType.Artist;
            DisplayTitle = Artist;

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

    public class PlaylistResult : SearchResult
    {
        public string PlaylistName;
        public string PlaylistPath;

        public PlaylistResult(string playlistName, string playlistPath)
        {
            PlaylistName = playlistName;
            PlaylistPath = playlistPath;
            Type = ResultType.Playlist;
            DisplayTitle = PlaylistName;
            DisplayDetail = "Playlist";
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
        public Dictionary<string, (string Artist, string SortArtist)> Artists; // NormalizedArtist: (Artist, SortArtist)
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

            Artists = new Dictionary<string, (string, string)>();
            Albums = new Dictionary<(string, string), Track>();
            Songs = new Dictionary<Track, (string, string)>();

            foreach (var track in tracks)
            {
                if ((enabledTypes & ResultType.Artist) != 0)
                {
                    SplitAndAddArtists(track.Artists, track.SortArtist, getNormalized);
                    SplitAndAddArtists(track.AlbumArtist, track.SortAlbumArtist, getNormalized);
                }

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

        void SplitAndAddArtists(string artists, string sortArtists, Func<string, string> getNormalizedFunc)
        {
            if (string.IsNullOrEmpty(artists))
                return;

            var splitArtists = artists.Split(';');
            var splitSortArtist = !string.IsNullOrEmpty(sortArtists) ? sortArtists.Split(';') : null;

            for (int i = 0; i < splitArtists.Length; i++)
            {
                var artist = splitArtists[i];
                if (string.IsNullOrWhiteSpace(artist))
                    continue;

                string trimmedArtist = artist.Trim();
                string normalizedArtist = getNormalizedFunc(trimmedArtist);

                if (!Artists.ContainsKey(normalizedArtist))
                {
                    if (splitSortArtist != null && i < splitSortArtist.Length)
                        Artists[normalizedArtist] = (artist.Trim(), splitSortArtist[i].Trim());
                    else
                        Artists[normalizedArtist] = (artist.Trim(), null);
                }
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
                Debug.WriteLine($"Tracks constructed in {sw.ElapsedMilliseconds}ms");
                sw.Restart();

                db = new Database(tracks, GetEnabledTypes());
                IsLoaded = true;

                sw.Stop();
                Debug.WriteLine($"Database loaded in {sw.ElapsedMilliseconds}ms");
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
            return results.OrderByDescending(r => GetResultTypePriority(r.Type))
                .ThenByDescending(r => CalculateOverallScore(r, normalizedQuery))
                .ToList();
        }

        private List<ArtistResult> SearchArtists(string[] queryWords, string normalizedQuery, int limit)
        {
            return db.Artists
                .Where(x => QueryMatchesWords(x.Key, queryWords, normalizeText: false))
                .OrderByDescending(x => CalculateGeneralItemScore(x.Key, normalizedQuery, queryWords, normalizeStrings: false))
                .Take(limit)
                .Select(x => new ArtistResult(x.Value.Artist, x.Value.SortArtist))
                .ToList();
        }

        private List<AlbumResult> SearchAlbums(string[] queryWords, string normalizedQuery, int limit)
        {
            return db.Albums
                .Where(x => QueryMatchesWords(x.Key.NormalizedAlbumArtist + " " + x.Key.NormalizedAlbum, queryWords, normalizeText: false))
                .OrderByDescending(x => CalculateArtistAndTitleScore(x.Key.NormalizedAlbumArtist, x.Key.NormalizedAlbum, normalizedQuery, queryWords, normalizeStrings: false))
                .Take(limit)
                .Select(x => new AlbumResult(x.Value.Album, x.Value.AlbumArtist, x.Value.SortAlbumArtist))
                .ToList();
        }

        private List<SongResult> SearchSongs(string[] queryWords, string normalizedQuery, int limit)
        {
            return db.Songs
                .Where(x => QueryMatchesWords(x.Value.NormalizedArtists + " " + x.Value.NormalizedTitle, queryWords, normalizeText: false))
                .OrderByDescending(x => CalculateArtistAndTitleScore(x.Value.NormalizedArtists, x.Value.NormalizedTitle, normalizedQuery, queryWords, normalizeStrings: false))
                .Take(limit)
                .Select(x => new SongResult(x.Key))
                .ToList();
        }

        private List<PlaylistResult> SearchPlaylists(string[] queryWords, string normalizedQuery, int limit)
        {
            return GetAllPlaylists()
                .Where(p => !string.IsNullOrEmpty(p.Name) && QueryMatchesWords(p.Name, queryWords))
                .OrderByDescending(p => CalculateGeneralItemScore(p.Name, normalizedQuery, queryWords))
                .Take(limit)
                .Select(p => new PlaylistResult(p.Name, p.Path))
                .ToList();
        }

        private bool QueryMatchesWords(string text, string[] queryWords, bool normalizeText = true) // TODO: Disallow matching a single text part multiple times
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (queryWords.Length == 0) return true;
            if (!config.EnableContainsCheck) return true;

            if (normalizeText)
                text = NormalizeString(text);

            return queryWords.All(word => text.Contains(word));
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

        private double CalculateOverallScore(SearchResult result, string query)
        {
            // This method is primarily for ordering DB search results.
            // Command results are typically filtered directly and may not use this complex scoring.
            switch (result.Type)
            {
                case ResultType.Artist: return CalculateGeneralItemScore(result.DisplayTitle, query, query.Split(' '));
                case ResultType.Album: return CalculateArtistAndTitleScore(((AlbumResult)result).AlbumArtist, ((AlbumResult)result).Album, query, query.Split(' '));
                case ResultType.Song: return CalculateArtistAndTitleScore(((SongResult)result).Artist, ((SongResult)result).TrackTitle, query, query.Split(' '));
                case ResultType.Command: return CalculateGeneralItemScore(result.DisplayTitle, query, query.Split(' '), normalizeStrings: false); // For commands, direct match on DisplayTitle
                default: return CalculateGeneralItemScore(result.DisplayTitle, query, query.Split(' '));
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

            if (!string.IsNullOrWhiteSpace(track.Artists))
            {
                var artists = track.Artists.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .DistinctBy(x => x.ToLower());

                foreach (var artist in artists)
                    res.Add(new ArtistResult(artist, artist));
            }

            if (!string.IsNullOrWhiteSpace(track.Album))
                res.Add(new AlbumResult(track.Album, track.AlbumArtist, track.SortAlbumArtist));

            return res;
        }

        public List<SearchResult> GetSelectedTracks()
        {
            mbApi.Library_QueryFilesEx("domain=SelectedFiles", out string[] files);

            if (files == null || files.Length == 0)
                return new List<SearchResult>();

            var tracks = files.Select(filepath => new Track(filepath)).ToList();

            var results = new List<SearchResult>();

            var artists = tracks.Where(t => !string.IsNullOrWhiteSpace(t.Artists))
                .SelectMany(t => t.Artists.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => new { Artist = x.Trim(), Track = t }))
                .DistinctBy(x => x.Artist.ToLower());

            foreach (var item in artists)
                results.Add(new ArtistResult(item.Artist, item.Artist));

            var albums = tracks.Where(t => !string.IsNullOrWhiteSpace(t.Album))
                .Select(t => new { Album = t.Album.Trim(), Track = t })
                .DistinctBy(x => x.Album.ToLower());

            foreach (var item in albums)
                results.Add(new AlbumResult(item.Track.Album, item.Track.AlbumArtist, item.Track.SortAlbumArtist));

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
