using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MusicBeePlugin.Utils;
using static MusicBeePlugin.Plugin;

namespace MusicBeePlugin.Services
{
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

    public class ArtistEntry
    {
        // A representative name for the entity, mainly for grouping.
        public string CanonicalArtistName { get; }

        // Maps a search-normalized alias back to the original (Artist, SortArtist) pair it came from.
        public Dictionary<string, (string Artist, string SortArtist)> AliasMap { get; }

        public ArtistEntry(string canonicalArtistName)
        {
            CanonicalArtistName = canonicalArtistName;
            AliasMap = new Dictionary<string, (string Artist, string SortArtist)>();
        }
    }

    public struct AlbumEntry
    {
        public Track Track;
        public string NormalizedAlbumName;
        public string NormalizedAlbumArtist;

        public AlbumEntry(Track track, string normAlbum, string normAlbumArtist)
        {
            Track = track;
            NormalizedAlbumName = normAlbum;
            NormalizedAlbumArtist = normAlbumArtist;
        }
    }

    public struct SongEntry
    {
        public Track Track;
        public string NormalizedTitle;
        public string NormalizedArtists;

        public SongEntry(Track track, string normTitle, string normArtists)
        {
            Track = track;
            NormalizedTitle = normTitle;
            NormalizedArtists = normArtists;
        }
    }

    public class Database
    {
        public List<ArtistEntry> Artists;
        public List<AlbumEntry> Albums;
        public List<SongEntry> Songs;

        // Caches used only during construction to avoid re-processing repeated strings (like "The Beatles")
        private Dictionary<string, string> _idKeyCache;
        private Dictionary<string, string> _searchKeyCache;

        public Database(IEnumerable<Track> tracks, ResultType enabledTypes)
        {
            Artists = new List<ArtistEntry>();
            Albums = new List<AlbumEntry>();
            Songs = new List<SongEntry>();

            _idKeyCache = new Dictionary<string, string>();
            _searchKeyCache = new Dictionary<string, string>();

            if ((enabledTypes & ResultType.Artist) != 0)
            {
                // Identity Key (lower) -> Group of (Artist, SortArtist)
                var tempArtistGroups = new Dictionary<string, HashSet<(string Artist, string SortArtist)>>();
                
                foreach (var track in tracks)
                {
                    PopulateArtistGroups(track.Artists, track.SortArtist, tempArtistGroups);
                    PopulateArtistGroups(track.AlbumArtist, track.SortAlbumArtist, tempArtistGroups);
                }

                foreach (var group in tempArtistGroups.Values)
                {
                    if (!group.Any()) continue;

                    var canonicalArtistName = group.First().Artist;
                    var entity = new ArtistEntry(canonicalArtistName);

                    foreach (var pair in group)
                    {
                        if (!string.IsNullOrEmpty(pair.Artist))
                        {
                            var idKey = GetIdKey(pair.Artist);
                            var searchKey = GetSearchKeyFromId(idKey);
                            entity.AliasMap[searchKey] = pair;
                        }

                        if (!string.IsNullOrEmpty(pair.SortArtist))
                        {
                            var idKey = GetIdKey(pair.SortArtist);
                            var searchKey = GetSearchKeyFromId(idKey);
                            // If sort artist normalizes to something different than the artist
                            if (!entity.AliasMap.ContainsKey(searchKey))
                            {
                                entity.AliasMap[searchKey] = pair;
                            }
                        }
                    }
                    Artists.Add(entity);
                }
            }

            if ((enabledTypes & ResultType.Album) != 0)
            {
                var seenAlbums = new HashSet<(string, string)>();

                foreach (var track in tracks)
                {
                    if (string.IsNullOrEmpty(track.Album)) continue;

                    var albumId = GetIdKey(track.Album);
                    var artistId = GetIdKey(track.AlbumArtist);
                    
                    // Deduplicate by Identity Key (Case insensitive, but punctuation sensitive)
                    if (seenAlbums.Add((albumId, artistId)))
                    {
                        Albums.Add(new AlbumEntry(
                            track,
                            GetSearchKeyFromId(albumId),
                            GetSearchKeyFromId(artistId)
                        ));
                    }
                }
            }

            if ((enabledTypes & ResultType.Song) != 0)
            {
                foreach (var track in tracks)
                {
                    if (string.IsNullOrEmpty(track.TrackTitle)) continue;

                    // Deduplication for songs is handled by the input list (unique tracks)
                    Songs.Add(new SongEntry(
                        track,
                        GetSearchKeyFromId(GetIdKey(track.TrackTitle)),
                        GetSearchKeyFromId(GetIdKey(track.Artists))
                    ));
                }
            }

            // Clear caches to free memory
            _idKeyCache = null;
            _searchKeyCache = null;
        }

        private void PopulateArtistGroups(string artists, string sortArtists, Dictionary<string, HashSet<(string Artist, string SortArtist)>> artistGroups)
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

                // Identity Key: Trimmed and Lowercase
                string idKey = GetIdKey(artist);

                if (!artistGroups.TryGetValue(idKey, out var group))
                {
                    group = new HashSet<(string Artist, string SortArtist)>();
                    artistGroups[idKey] = group;
                }

                group.Add((artist, sortArtist));
            }
        }

        private string GetIdKey(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            if (_idKeyCache.TryGetValue(input, out var cached)) return cached;

            var result = input.Trim().ToLowerInvariant();
            _idKeyCache[input] = result;
            return result;
        }

        private string GetSearchKeyFromId(string idKey)
        {
            if (string.IsNullOrEmpty(idKey)) return string.Empty;
            if (_searchKeyCache.TryGetValue(idKey, out var cached)) return cached;

            // Optimization: idKey is already lower case, just strip punctuation
            var result = RemovePunctuation(idKey);
            _searchKeyCache[idKey] = result;
            return result;
        }

        static readonly HashSet<char> punctuation = new HashSet<char>
        {
            '!', '?', '(', ')', '.', ',', '-', ':', ';', '[', ']',
            '{', '}', '/', '\\', '+', '=', '*', '&', '#', '@', '$',
            '%', '^', '|', '~', '<', '>', '`', '"'
        };

        // Assumes input is already lower case
        private string RemovePunctuation(string input)
        {
            char[] outputChars = new char[input.Length];
            int outputIndex = 0;
            bool previousIsSpace = true;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '\'') continue;

                if (punctuation.Contains(c) || c == ' ')
                {
                    if (!previousIsSpace)
                    {
                        outputChars[outputIndex++] = ' ';
                        previousIsSpace = true;
                    }
                }
                else
                {
                    outputChars[outputIndex++] = c;
                    previousIsSpace = false;
                }
            }

            if (outputIndex > 0 && outputChars[outputIndex - 1] == ' ')
                outputIndex--;

            return new string(outputChars, 0, outputIndex);
        }

        // Helper for external use (like query string normalization)
        public static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            
            // Replicate the logic: ToLower -> RemovePunctuation
            // We implement the combined loop here to avoid allocating the intermediate string
            
            char[] outputChars = new char[input.Length];
            int outputIndex = 0;
            bool previousIsSpace = true;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '\'') continue;

                char current = char.ToLowerInvariant(c);

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
    }
}