﻿using MusicBeePlugin.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            if (!Artist.Equals(SortArtist, StringComparison.OrdinalIgnoreCase))
                DisplayDetail = SortArtist;
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

    public class SearchService
    {
        public List<Track> database;
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
                mbApi.Library_QueryFilesEx("", out string[] files);
                database = files.Select(filepath => new Track(filepath)).ToList();
                IsLoaded = true;
            });
        }

        public List<SearchResult> Search(string query, ResultType enabledTypes)
        {
            if (!IsLoaded) return new List<SearchResult>();
            var results = new List<SearchResult>();
            string normalizedQuery = NormalizeString(query);
            string[] queryWords = normalizedQuery.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if ((enabledTypes & ResultType.Artist) == ResultType.Artist)
            {
                var artistResults = SearchArtists(queryWords, normalizedQuery);
                results.AddRange(artistResults);
            }

            if ((enabledTypes & ResultType.Album) == ResultType.Album)
            {
                var albumResults = SearchAlbums(queryWords, normalizedQuery);
                results.AddRange(albumResults);
            }

            if ((enabledTypes & ResultType.Song) == ResultType.Song)
            {
                var songResults = SearchSongs(queryWords, normalizedQuery);
                results.AddRange(songResults);
            }

            if ((enabledTypes & ResultType.Playlist) == ResultType.Playlist)
            {
                var playlistResults = SearchPlaylists(queryWords, normalizedQuery);
                results.AddRange(playlistResults);
            }

            var res = results.OrderByDescending(r => GetResultTypePriority(r.Type))
                .ThenByDescending(r => CalculateOverallScore(r, normalizedQuery))
                .ToList();

            return res;
        }

        private List<ArtistResult> SearchArtists(string[] queryWords, string normalizedQuery)
        {
            return database
                .SelectMany(t => 
                    (t.Artist?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Concat(t.SortArtist?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        .Where(a => !string.IsNullOrEmpty(a))
                        .Select(artist => artist.Trim())
                        .Distinct()
                        .Select(artist => new { Artist = artist, Track = t }))
                    ?? Enumerable.Empty<(string Artist, Track Track)>().Select(x => new { x.Artist, x.Track }))
                .DistinctBy(x => x.Artist.ToLower())
                .Where(x => !string.IsNullOrEmpty(x.Artist) && QueryMatchesWords(NormalizeString(x.Artist), queryWords))
                .OrderByDescending(x => CalculateGeneralItemScore(x.Artist, normalizedQuery, queryWords))
                .Take(config.ArtistResultLimit)
                .Select(x => new ArtistResult(x.Artist, x.Track.SortArtist))
                .ToList();
        }

        private List<AlbumResult> SearchAlbums(string[] queryWords, string normalizedQuery)
        {
            return database
                .GroupBy(t => new { t.Album, t.AlbumArtist })
                .Select(group => group.First())
                .Where(track => !string.IsNullOrEmpty(track.Album) && QueryMatchesWords(NormalizeString(track.AlbumArtist + " " + track.Album), queryWords))
                .OrderByDescending(track => CalculateGeneralItemScore(track.Album, normalizedQuery, queryWords))
                .Take(config.AlbumResultLimit)
                .Select(track => new AlbumResult(track.Album, track.AlbumArtist, track.SortAlbumArtist))
                .ToList();
        }

        private List<SongResult> SearchSongs(string[] queryWords, string normalizedQuery)
        {
            return database
                .Where(track =>
                    !string.IsNullOrEmpty(track.TrackTitle) && QueryMatchesWords(NormalizeString(track.Artists + " " + track.TrackTitle), queryWords)
                )
                .OrderByDescending(track => CalculateSongScore(track.Artist, track.TrackTitle, normalizedQuery, queryWords))
                .Take(config.SongResultLimit)
                .Select(track => new SongResult(track.Filepath))
                .ToList();
        }

        private List<PlaylistResult> SearchPlaylists(string[] queryWords, string normalizedQuery)
        {
            return GetAllPlaylists()
                .Where(p => !string.IsNullOrEmpty(p.Name) && QueryMatchesWords(NormalizeString(p.Name), queryWords))
                .OrderByDescending(p => CalculateGeneralItemScore(p.Name, normalizedQuery, queryWords))
                .Take(config.PlaylistResultLimit)
                .Select(p => new PlaylistResult(p.Name, p.Path))
                .ToList();
        }

        private bool QueryMatchesWords(string text, string[] queryWords) // TODO: Disallow matching a single text part multiple times
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (queryWords.Length == 0) return true;
            if (!config.EnableContainsCheck) return true;

            string normalizedText = NormalizeString(text);
            return queryWords.All(word => normalizedText.Contains(word));
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
                case ResultType.Album: return CalculateGeneralItemScore(result.DisplayTitle, query, query.Split(' '));
                case ResultType.Song: return CalculateSongScore(((SongResult)result).Artist, ((SongResult)result).TrackTitle, query, query.Split(' '));
                default: return 0;
            }
        }

        private double CalculateGeneralItemScore(string item, string query, string[] queryWords)
        {
            if (string.IsNullOrEmpty(item)) return 0;
            return CalculateFuzzyScore(NormalizeString(item), NormalizeString(query), queryWords);
        }

        private double CalculateSongScore(string artist, string title, string query, string[] queryWords)
        {
            double titleScore = string.IsNullOrEmpty(title) ? 0 : CalculateFuzzyScore(NormalizeString(title), NormalizeString(query), queryWords);
            double artistScore = string.IsNullOrEmpty(artist) ? 0 : CalculateFuzzyScore(NormalizeString(artist), NormalizeString(query), queryWords);
            return titleScore + artistScore * 0.5;
        }

        private double CalculateFuzzyScore(string text, string query, string[] queryWords)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return 0;

            const int NGRAM_SIZE = 2; // Use bigrams
            double score = 0;
            
            // Create n-grams for the text
            var textNgrams = new HashSet<string>();
            for (int i = 0; i < text.Length - NGRAM_SIZE + 1; i++)
            {
                textNgrams.Add(text.Substring(i, NGRAM_SIZE));
            }

            foreach (string word in queryWords)
            {
                if (word.Length < NGRAM_SIZE) 
                {
                    // Handle short words with direct containment check
                    if (text.Contains(word))
                    {
                        score += 1.0;
                    }
                    continue;
                }

                // Create n-grams for the query word
                var wordNgrams = new HashSet<string>();
                for (int i = 0; i < word.Length - NGRAM_SIZE + 1; i++)
                {
                    wordNgrams.Add(word.Substring(i, NGRAM_SIZE));
                }

                // Calculate Dice coefficient
                var intersectionCount = textNgrams.Intersect(wordNgrams).Count();
                if (intersectionCount > 0)
                {
                    var diceCoefficient = (2.0 * intersectionCount) / (textNgrams.Count + wordNgrams.Count);
                    score += diceCoefficient;

                    // Bonus for prefix matches (helps with partial word matches)
                    if (text.StartsWith(word))
                    {
                        score += 0.5;
                    }
                }
            }

            // Bonus for exact matches
            if (text == query)
            {
                score *= 2;
            }

            return score;
        }

        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return string.Join(" ", input.ToLower()
                .Replace("'", "")
                .Replace("`", "")
                .Replace("\"", "")
                .Split(new[] { '!', '?', '(', ')', '.', ',', '-', ':', ';', '[', ']', 
                    '{', '}', '/', '\\', '+', '=', '*', '&', '#', '@',
                    '$', '%', '^', '|', '~', '<', '>' }, 
                StringSplitOptions.RemoveEmptyEntries));
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
