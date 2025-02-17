using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static MusicBeePlugin.Plugin;
using System.Threading;

namespace MusicBeePlugin.Services
{
    [Flags]
    public enum ResultType
    {
        Song = 1,
        Album = 2,
        Artist = 4,
        Playlist = 8,
        All = 15,
    }

    public class SearchResult
    {
        public string DisplayTitle;
        public string DisplayDetail;
        public ResultType Type;
    }

    public class SongResult : SearchResult
    {
        public string TrackTitle;
        public string Artist;
        public string SortArtist;
        public string Filepath;

        static MetaDataType[] fields = new MetaDataType[]
        {
            MetaDataType.TrackTitle,
            MetaDataType.Artist,
            MetaDataType.SortArtist,
        };

        public SongResult(string trackTitle, string artist, string sortArtist, string filepath)
        {
            TrackTitle = trackTitle;
            Artist = artist;
            SortArtist = sortArtist;
            Filepath = filepath;
            DisplayTitle = TrackTitle;
            DisplayDetail = Artist;
            Type = ResultType.Song;
        }

        public SongResult(string filepath)
        {
            Filepath = filepath;
            mbApi.Library_GetFileTags(filepath, fields, out string[] results);

            if (results != null)
            {
                TrackTitle = results[0];
                Artist = results[1];
                SortArtist = results[2];
            }

            DisplayTitle = TrackTitle;
            DisplayDetail = Artist;
            Type = ResultType.Song;
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

    public class Track
    {
        public string TrackTitle;
        public string Artist; // artist tag
        public string Artists; // artist tag + multi-artists (performer, remixer, etc)
        public string SortArtist;
        public string Album;
        public string AlbumArtist;
        public string SortAlbumArtist;
        public string Filepath;

        static MetaDataType[] fields = new MetaDataType[]
        {
            MetaDataType.TrackTitle,
            MetaDataType.Artist,
            MetaDataType.Artists,
            MetaDataType.SortArtist,
            MetaDataType.Album,
            MetaDataType.AlbumArtist,
            MetaDataType.SortAlbumArtist,
        };

        public Track() { }

        public Track(string filepath)
        {
            Filepath = filepath;
            mbApi.Library_GetFileTags(filepath, fields, out string[] results);
            
            if (results != null && results.Length == fields.Length)
            {
                TrackTitle = results[0];
                Artist = results[1];
                Artists = results[2];
                SortArtist = results[3];
                Album = results[4];
                AlbumArtist = results[5];
                SortAlbumArtist = results[6];
            }
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
    }

    public class Database
    {
        public Dictionary<string, (string Artist, string SortArtist)> Artists; // NormalizedArtist: Artist
        public Dictionary<(string NormalizedAlbum, string NormalizedAlbumArtist), Track> Albums;
        public Dictionary<Track, (string NormalizedTitle, string NormalizedArtists)> Songs;

        public Database(IEnumerable<Track> tracks)
        {
            Artists = new Dictionary<string, (string, string)>();
            Albums = new Dictionary<(string, string), Track>();
            Songs = new Dictionary<Track, (string, string)>();

            foreach (var track in tracks)
            {
                if (!string.IsNullOrEmpty(track.Artists))
                {
                    var trackArtists = track.Artists.Split(';');
                    var sortArtists = !string.IsNullOrEmpty(track.SortArtist) ? track.SortArtist.Split(';') : null;

                    for (int i = 0; i < trackArtists.Length; i++)
                    {
                        var artist = trackArtists[i];
                        if (string.IsNullOrWhiteSpace(artist)) 
                            continue;
                        
                        string normalizedArtist = SearchService.NormalizeString(artist);

                        if (!Artists.ContainsKey(normalizedArtist))
                        {
                            if (sortArtists != null && i < sortArtists.Length)
                                Artists[normalizedArtist] = (artist.Trim(), sortArtists[i].Trim());
                            else
                                Artists[normalizedArtist] = (artist.Trim(), null);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(track.Album))
                {
                    var key = (
                        NormalizedAlbum: SearchService.NormalizeString(track.Album),
                        NormalizedAlbumArtist: SearchService.NormalizeString(track.AlbumArtist)
                    );
                    if (!Albums.ContainsKey(key))
                    {
                        Albums[key] = track;
                    }
                }

                if (!string.IsNullOrEmpty(track.TrackTitle))
                {
                    var value = (
                        NormalizedTitle: SearchService.NormalizeString(track.TrackTitle),
                        NormalizedArtists: SearchService.NormalizeString(track.Artists)
                    );
                    Songs[track] = value;
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
                var tracks = Tests.SyntheticDataTests.GenerateSyntheticDatabase(1000000).Result;
                //mbApi.Library_QueryFilesEx("", out string[] files);
                //var tracks = files.Select(filepath => new Track(filepath));

                var sw = Stopwatch.StartNew();
                Debug.WriteLine("Starting database load...");

                db = new Database(tracks);
                IsLoaded = true;

                sw.Stop();
                Debug.WriteLine($"Database loaded in {sw.ElapsedMilliseconds}ms");
            });
        }

        public async Task SearchIncrementalAsync(string query, ResultType enabledTypes, CancellationToken cancellationToken, Action<List<SearchResult>> onResultsUpdate)
        {
            if (!IsLoaded) 
            {
                onResultsUpdate(new List<SearchResult>());
                return;
            }

            await Task.Run(async () => {
                var results = new List<SearchResult>();
                string normalizedQuery = NormalizeString(query);
                string[] queryWords = normalizedQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if ((enabledTypes & ResultType.Artist) == ResultType.Artist)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var artistResults = SearchArtists(queryWords, normalizedQuery);
                    results.AddRange(artistResults);
                    onResultsUpdate(OrderResults(results, normalizedQuery));
                }

                if ((enabledTypes & ResultType.Album) == ResultType.Album)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var albumResults = SearchAlbums(queryWords, normalizedQuery);
                    results.AddRange(albumResults);
                    onResultsUpdate(OrderResults(results, normalizedQuery));
                }

                if ((enabledTypes & ResultType.Song) == ResultType.Song)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var songResults = SearchSongs(queryWords, normalizedQuery);
                    results.AddRange(songResults);
                    onResultsUpdate(OrderResults(results, normalizedQuery));
                }

                if ((enabledTypes & ResultType.Playlist) == ResultType.Playlist)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var playlistResults = SearchPlaylists(queryWords, normalizedQuery);
                    results.AddRange(playlistResults);
                    onResultsUpdate(OrderResults(results, normalizedQuery));
                }
            }, cancellationToken);
        }

        private List<SearchResult> OrderResults(List<SearchResult> results, string normalizedQuery)
        {
            return results.OrderByDescending(r => GetResultTypePriority(r.Type))
                .ThenByDescending(r => CalculateOverallScore(r, normalizedQuery))
                .ToList();
        }

        private List<ArtistResult> SearchArtists(string[] queryWords, string normalizedQuery)
        {
            return db.Artists
                .Where(x => QueryMatchesWords(x.Key, queryWords, normalizeText: false))
                .OrderByDescending(x => CalculateGeneralItemScore(x.Key, normalizedQuery, queryWords, normalizeStrings: false))
                .Take(config.ArtistResultLimit)
                .Select(x => new ArtistResult(x.Value.Artist, x.Value.SortArtist))
                .ToList();
        }

        private List<AlbumResult> SearchAlbums(string[] queryWords, string normalizedQuery)
        {
            return db.Albums
                .Where(x => QueryMatchesWords(x.Key.NormalizedAlbumArtist + " " + x.Key.NormalizedAlbum, queryWords, normalizeText: false))
                .OrderByDescending(x => CalculateArtistAndTitleScore(x.Key.NormalizedAlbumArtist, x.Key.NormalizedAlbum, normalizedQuery, queryWords, normalizeStrings: false))
                .Take(config.AlbumResultLimit)
                .Select(x => new AlbumResult(x.Value.Album, x.Value.AlbumArtist, x.Value.SortAlbumArtist))
                .ToList();
        }

        private List<SongResult> SearchSongs(string[] queryWords, string normalizedQuery)
        {
            return db.Songs
                .Where(x => QueryMatchesWords(x.Value.NormalizedArtists + " " + x.Value.NormalizedTitle, queryWords, normalizeText: false))
                .OrderByDescending(x => CalculateArtistAndTitleScore(x.Value.NormalizedArtists, x.Value.NormalizedTitle, normalizedQuery, queryWords, normalizeStrings: false))
                .Take(config.SongResultLimit)
                .Select(x => new SongResult(x.Key.TrackTitle, x.Key.Artist, x.Key.SortArtist, x.Key.Filepath))
                .ToList();
        }

        private List<PlaylistResult> SearchPlaylists(string[] queryWords, string normalizedQuery)
        {
            return GetAllPlaylists()
                .Where(p => !string.IsNullOrEmpty(p.Name) && QueryMatchesWords(p.Name, queryWords))
                .OrderByDescending(p => CalculateGeneralItemScore(p.Name, normalizedQuery, queryWords))
                .Take(config.PlaylistResultLimit)
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

        private int GetResultTypePriority(ResultType type)
        {
            if (!config.GroupResultsByType) return 0;
            
            switch (type)
            {
                case ResultType.Artist: return 3;
                case ResultType.Album: return 2;
                case ResultType.Song: return 1;
                case ResultType.Playlist: return 0;
                default: return 0;
            }
        }

        private double CalculateOverallScore(SearchResult result, string query)
        {
            switch (result.Type)
            {
                case ResultType.Artist: return CalculateGeneralItemScore(result.DisplayTitle, query, query.Split(' '));
                case ResultType.Album: return CalculateArtistAndTitleScore(((AlbumResult)result).AlbumArtist, ((AlbumResult)result).Album, query, query.Split(' '));
                case ResultType.Song: return CalculateArtistAndTitleScore(((SongResult)result).Artist, ((SongResult)result).TrackTitle, query, query.Split(' '));
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

        // Convert to lower, remove ' , replace punctuation chars with space, remove consecutive spaces and trim. 
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
    }
}
