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

        private static readonly MetaDataType[] fields = new MetaDataType[]
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

    public readonly struct ArtistEntry
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

    public readonly struct AlbumEntry
    {
        public Track Track { get; }
        public string NormalizedAlbumName { get; }
        public string NormalizedAlbumArtist { get; }

        public AlbumEntry(Track track, string normAlbum, string normAlbumArtist)
        {
            Track = track;
            NormalizedAlbumName = normAlbum;
            NormalizedAlbumArtist = normAlbumArtist;
        }
    }

    public readonly struct SongEntry
    {
        public Track Track { get; }
        public string NormalizedTitle { get; }
        public string NormalizedArtists { get; }

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

        // Caches used only during construction to avoid re-processing repeated strings
        private readonly Dictionary<string, string> _idKeyCache;
        private readonly Dictionary<string, string> _searchKeyCache;

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
                            var searchKey = GetSearchKey(idKey);
                            entity.AliasMap[searchKey] = pair;
                        }

                        if (!string.IsNullOrEmpty(pair.SortArtist))
                        {
                            var idKey = GetIdKey(pair.SortArtist);
                            var searchKey = GetSearchKey(idKey);
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
                            GetSearchKey(albumId),
                            GetSearchKey(artistId)
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
                        GetSearchKey(track.TrackTitle, bypassCache: true), // song titles are unlikely to benefit from caching
                        GetSearchKey(GetIdKey(track.Artists)) // use the id as input for the caching in GetSearchKey to work
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

            // Identity key is just trimmed and lowercased. 
            // We KEEP diacritics here so "Björk" and "Bjork" are grouped as distinct entities in the UI.
            var result = input.Trim().ToLowerInvariant();
            _idKeyCache[input] = result;
            return result;
        }

        // Note: Doesn't strictly require it, but it's better to call with id (lowercased+trimmed) inputs, for caching.
        private string GetSearchKey(string idKey, bool bypassCache = false)
        {
            if (string.IsNullOrEmpty(idKey)) return string.Empty;
            
            if (!bypassCache && _searchKeyCache.TryGetValue(idKey, out var cached))
                return cached;

            // OPTIMIZATION:
            // We use NormalizeString here. Even though idKey is already lowercase,
            // NormalizeString applies the CharMap which handles:
            // 1. Diacritic removal (ä -> a)
            // 2. Punctuation removal
            // 3. Space normalization
            // All in one single pass.
            var result = NormalizeString(idKey);

            if (!bypassCache)
                _searchKeyCache[idKey] = result;
            
            return result;
        }

        public static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Performance: Cache references to the static arrays to avoid field access overhead in the loop
            char[] map = MusicBeePlugin.Utils.CharMap.Map;
            bool[] isPunc = MusicBeePlugin.Utils.CharMap.IsPunctuation; // note: also includes space

            int len = input.Length;
            char[] outputChars = new char[len];
            int outputIndex = 0;
            bool previousIsSpace = true; // Start true to auto-trim leading symbols

            for (int i = 0; i < len; i++)
            {
                char c = input[i];

                // 1. Special handling: Apostrophes are skipped entirely (O'Connor -> oconnor)
                if (c == '\'') continue;

                // 2. Check Punctuation (Array Lookup)
                if (isPunc[c])
                {
                    if (!previousIsSpace)
                    {
                        outputChars[outputIndex++] = ' ';
                        previousIsSpace = true;
                    }
                }
                else
                {
                    // 3. Normalization (Array Lookup)
                    // This converts Upper->Lower AND Diacritic->ASCII in one instruction
                    outputChars[outputIndex++] = map[c];
                    previousIsSpace = false;
                }
            }

            // Trim trailing space if exists
            if (outputIndex > 0 && outputChars[outputIndex - 1] == ' ')
                outputIndex--;

            return new string(outputChars, 0, outputIndex);
        }
    }
}