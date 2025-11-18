using MusicBeePlugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MusicBeePlugin.Tests
{
    public static class SyntheticDataTests
    {
        private static readonly string[] WordList = new[]
        {
            "love", "heart", "time", "night", "day", "dream", "world", "light", "dark", "moon",
            "sun", "star", "sky", "ocean", "river", "mountain", "forest", "city", "road", "home",
            "fire", "ice", "wind", "rain", "storm", "shadow", "angel", "demon", "ghost", "soul",
            "mind", "body", "spirit", "life", "death", "eternity", "moment", "memory", "future",
            "past", "present", "beginning", "end", "journey", "destiny", "fate", "chance", "luck",
            "fortune", "misfortune", "joy", "sorrow", "pain", "pleasure", "desire", "fear", "hope",
            "despair", "courage", "cowardice", "strength", "weakness", "power", "freedom", "bondage",
            "truth", "lie", "illusion", "reality", "fantasy", "myth", "legend", "history", "future",
            "present", "past", "beginning", "end", "journey", "destiny", "fate", "chance", "luck",
            "blue", "red", "green", "yellow", "black", "white", "gold", "silver", "bronze", "crystal",
            "diamond", "emerald", "ruby", "sapphire", "pearl", "opal", "amethyst", "topaz", "jade",
            "steel", "iron", "copper", "brass", "stone", "wood", "glass", "plastic", "paper", "cloth",
            "velvet", "silk", "leather", "fur", "feather", "bone", "ash", "dust", "smoke", "mist",
            "fog", "cloud", "rainbow", "thunder", "lightning", "earth", "water", "fire", "air", "void",
            "space", "galaxy", "universe", "planet", "comet", "meteor", "asteroid", "nebula", "quasar",
            "blackhole", "wormhole", "dimension", "realm", "kingdom", "empire", "nation", "country",
            "city", "town", "village", "hamlet", "castle", "tower", "fortress", "temple", "shrine",
            "altar", "sanctuary", "haven", "refuge", "asylum", "paradise", "utopia", "dystopia", "hell",
            "heaven", "purgatory", "limbo", "nirvana", "valhalla", "elysium", "arcadia", "eden", "zion",
            "mother", "father", "brother", "sister", "friend"
        };

        private static readonly string[] NamePatterns = new[]
        {
            "The {0}", "{0} of {1}", "{0} in {1}", "{0} and {1}", "{0} vs {1}", "{0} with {1}",
            "{0} without {1}", "{0} from {1}", "{0} to {1}", "{0} for {1}", "{0} against {1}",
            "{0} beyond {1}", "{0} within {1}", "{0} through {1}", "{0} across {1}", "{0} about {1}",
            "My {0}", "Your {0}", "Our {0}", "Their {0}", "His {0}", "Her {0}", "Its {0}", "Hello {0}",
            "Goodbye {0}", "Bye {0}", "Dear {0}",
        };

        private static readonly char[] Punctuation = new[] { '.', '!', '?', ',', ';', ':' };

        private static readonly Random random = new Random();
        
        public static string GenerateRandomName(int wordCount)
        {
            var words = Enumerable.Range(0, wordCount)
                .Select(_ => {
                    string word = WordList[random.Next(WordList.Length)];
                    
                    // Randomly capitalize first letter (50% chance)
                    if (random.Next(2) == 0) {
                        word = char.ToUpper(word[0]) + word.Substring(1);
                    }
                    
                    // Randomly add punctuation (5% chance)
                    if (random.Next(20) == 0) {
                        word += Punctuation[random.Next(Punctuation.Length)];
                    }
                    
                    return word;
                })
                .ToArray();

            // Use a name pattern 20% of the time if we have enough words
            if (wordCount >= 2 && random.Next(5) == 0) {
                return string.Format(NamePatterns[random.Next(NamePatterns.Length)], words[0], words[1]);
            }
            
            return string.Join(" ", words);
        }

        public static string GenerateArtistName() => GenerateRandomName(random.Next(1, 3));
        public static string GenerateAlbumName() => GenerateRandomName(random.Next(1, 4));
        public static string GenerateSongTitle() => GenerateRandomName(random.Next(1, 5));

        public static async Task<List<Track>> GenerateSyntheticDatabase(int totalSongs)
        {
            return await Task.Run(() => {
                var database = new List<Track>();
                int songsAdded = 0;

                while (songsAdded < totalSongs)
                {
                    string artist = GenerateArtistName();
                    string albumArtist = artist;
                    string album = GenerateAlbumName();
                    int albumSize = random.Next(1, 31); // 1-30 songs per album

                    for (int i = 0; i < albumSize && songsAdded < totalSongs; i++)
                    {
                        string title = GenerateSongTitle();
                        string filepath = $"/fake/path/{artist}/{album}/{title}.mp3";
                        var track = new Track()
                        {
                            TrackTitle = title,
                            Artist = artist,
                            Artists = artist,
                            SortArtist = artist,
                            Album = album,
                            AlbumArtist = albumArtist,
                            SortAlbumArtist = albumArtist
                        };
                        database.Add(track);
                        songsAdded++;
                    }
                }

                return database;
            });
        }
    }
}
