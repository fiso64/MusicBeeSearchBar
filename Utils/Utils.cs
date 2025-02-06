using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MusicBeePlugin.Utils
{
    public static class Utils
    {
        public static (string trackNo, string trackCount, string discNo, string discCount) ParseTrackAndDisc(string trackPos, string discPos)
        {
            var trackNo = trackPos ?? string.Empty;
            var trackCount = string.Empty;
            var discNo = discPos ?? string.Empty;
            var discCount = string.Empty;
            
            if (trackNo.Contains('/'))
            {
                var parts = trackNo.Split('/');
                trackNo = parts[0];
                trackCount = parts[1];
            }

            if (discNo.Contains('/'))
            {
                var parts = discNo.Split('/');
                discNo = parts[0];
                discCount = parts[1];
            }

            if (trackNo.Contains('-'))
            {
                var parts = trackNo.Split('-');
                discNo = parts[0];
                trackNo = parts[1];
            }

            return (trackNo, trackCount, discNo, discCount);
        }

        public static bool Contains(this string source, string toCheck, StringComparison comp)
        {
            return source?.IndexOf(toCheck, comp) >= 0;
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
        {
            if (dictionary.TryGetValue(key, out TValue value))
            {
                return value;
            }
            return defaultValue;
        }
    }
}
