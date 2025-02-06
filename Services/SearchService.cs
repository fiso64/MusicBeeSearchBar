using System;
using System.Collections.Generic;
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
        All = Song | Album | Artist
    }

    public class SearchResult
    {
        public string Title { get; set; }
        public string Detail { get; set; }
        public ResultType Type;
        public Track Values;
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
    }

    public class SearchService
    {
        public List<Track> database;
        private MusicBeeApiInterface mbApi;
        private bool groupResultsByType = true;

        public SearchService(MusicBeeApiInterface mbApi, bool groupResultsByType = true)
        {
            this.mbApi = mbApi;
            this.groupResultsByType = groupResultsByType;
        }

        public void LoadTracks()
        {
            mbApi.Library_QueryFilesEx("", out string[] files);
            var fields = new MetaDataType[] 
            { 
                MetaDataType.TrackTitle,
                MetaDataType.Artists,
                MetaDataType.Artists,
                MetaDataType.SortArtist,
                MetaDataType.Album, 
                MetaDataType.AlbumArtist,
                MetaDataType.SortAlbumArtist,
            };
            database = files.Select(filepath =>
            {
                mbApi.Library_GetFileTags(filepath, fields, out string[] results);
                return new Track() 
                { 
                    TrackTitle = results[0],
                    Artist = results[1],
                    Artists = results[2], 
                    SortArtist = results[3],
                    Album = results[4], 
                    AlbumArtist = results[5], 
                    SortAlbumArtist = results[6],
                    Filepath = filepath
                };
            }).ToList();
        }

        public List<SearchResult> Search(string query, ResultType enabledTypes)
        {
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

            var res = results.OrderByDescending(r => groupResultsByType ? GetResultTypePriority(r.Type) : 0)
                .ThenByDescending(r => CalculateOverallScore(r, normalizedQuery))
                .ToList();

            return res;
        }

        private List<SearchResult> SearchArtists(string[] queryWords, string normalizedQuery, int limit = 5)
        {
            return database
                .SelectMany(t => t.Artists?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(artist => artist.Trim())
                    .Distinct()
                    .Select(artist => new { Artist = artist, Track = t }) 
                    ?? Enumerable.Empty<(string Artist, Track Track)>().Select(x => new { Artist = x.Artist, Track = x.Track }))
                .GroupBy(x => x.Artist.ToLower())
                .Select(group => group.First())
                .Where(x => !string.IsNullOrEmpty(x.Artist) && QueryMatchesWords(NormalizeString(x.Artist), queryWords))
                .OrderByDescending(x => CalculateArtistScore(x.Artist, normalizedQuery, queryWords))
                .Take(limit)
                .Select(x => new SearchResult
                {
                    Title = x.Artist,
                    Detail = "",
                    Type = ResultType.Artist,
                    Values = x.Track
                }).ToList();
        }

        private List<SearchResult> SearchAlbums(string[] queryWords, string normalizedQuery, int limit = 5)
        {
            return database
                .GroupBy(t => new { t.Album, t.AlbumArtist })
                .Select(group => group.First())
                .Where(track => !string.IsNullOrEmpty(track.Album) && QueryMatchesWords(NormalizeString(track.AlbumArtist + " " + track.Album), queryWords))
                .OrderByDescending(track => CalculateAlbumScore(track.Album, normalizedQuery, queryWords))
                .Take(limit)
                .Select(track => new SearchResult
                {
                    Title = track.Album,
                    Detail = track.AlbumArtist,
                    Type = ResultType.Album,
                    Values = track
                }).ToList();
        }

        private List<SearchResult> SearchSongs(string[] queryWords, string normalizedQuery, int limit = 10)
        {
            return database
                .Where(track =>
                    !string.IsNullOrEmpty(track.TrackTitle) && QueryMatchesWords(NormalizeString(track.Artists + " " + track.TrackTitle), queryWords)
                //|| (!string.IsNullOrEmpty(track.Artist) && QueryMatchesAllWords(NormalizeString(track.Artist), queryWords))
                )
                .OrderByDescending(track => CalculateSongScore(track, normalizedQuery, queryWords))
                .Take(limit)
                .Select(track => new SearchResult
                {
                    Title = track.TrackTitle,
                    Detail = track.Artists,
                    Type = ResultType.Song,
                    Values = track
                }).ToList();
        }

        private bool QueryMatchesWords(string text, string[] queryWords) // TODO: Disallow matching a single text part multiple times
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (queryWords.Length == 0) return true;

            string normalizedText = NormalizeString(text);
     
            return queryWords.All(word => normalizedText.Contains(word));
        }


        private int GetResultTypePriority(ResultType type)
        {
            switch (type)
            {
                case ResultType.Artist: return 3;
                case ResultType.Album: return 2;
                case ResultType.Song: return 1;
                default: return 0;
            }
        }

        private double CalculateOverallScore(SearchResult result, string query)
        {
            switch (result.Type)
            {
                case ResultType.Artist: return CalculateArtistScore(result.Title, query, query.Split(' '));
                case ResultType.Album: return CalculateAlbumScore(result.Title, query, query.Split(' '));
                case ResultType.Song: return CalculateSongScore(result.Values, query, query.Split(' '));
                default: return 0;
            }
        }

        private double CalculateArtistScore(string artist, string query, string[] queryWords)
        {
            if (string.IsNullOrEmpty(artist)) return 0;
            return CalculateFuzzyScore(NormalizeString(artist), NormalizeString(query), queryWords);
        }

        private double CalculateAlbumScore(string album, string query, string[] queryWords)
        {
            if (string.IsNullOrEmpty(album)) return 0;
            return CalculateFuzzyScore(NormalizeString(album), NormalizeString(query), queryWords);
        }

        private double CalculateSongScore(Track track, string query, string[] queryWords)
        {
            double titleScore = string.IsNullOrEmpty(track.TrackTitle) ? 0 : CalculateFuzzyScore(NormalizeString(track.TrackTitle), NormalizeString(query), queryWords);
            double artistScore = string.IsNullOrEmpty(track.Artists) ? 0 : CalculateFuzzyScore(NormalizeString(track.Artists), NormalizeString(query), queryWords);
            return titleScore + artistScore * 0.5;
        }

        //private double CalculateFuzzyScore(string text, string query, string[] queryWords) // TODO: Use a better fuzzy score algorithm
        //{
        //    if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return 0;

        //    double score = 0;
        //    int matchedWordCount = 0;
        //    string normalizedText = NormalizeString(text);

        //    foreach (string word in queryWords)
        //    {
        //        if (normalizedText.Contains(word))
        //        {
        //            score += (double)word.Length / normalizedText.Length; // Base score for word presence
        //            matchedWordCount++;
        //        }
        //    }

        //    if (matchedWordCount == queryWords.Length && queryWords.Length > 0)
        //    {
        //        score += 0.5; // Bonus for matching all words
        //    }

        //    return score;
        //}

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
    }

}