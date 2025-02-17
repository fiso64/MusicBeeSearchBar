using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicBeePlugin.Utils
{
    public static class FuzzySearch
    {
        public static double FuzzyScoreNgram(string text, string query, string[] queryWords)
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

        // currently unused
        public static double FuzzyScoreSimple(string text, string query, string[] queryWords)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query)) return 0;

            double score = 0;

            foreach (string word in queryWords)
            {
                // Skip empty words
                if (string.IsNullOrEmpty(word)) continue;

                score += CalculateWordScore(text, word);
            }

            // Bonus for exact matches
            if (text == query)
            {
                score *= 2;
            }

            return score;
        }

        private static double CalculateWordScore(string text, string word)
        {
            int textLen = text.Length;
            int wordLen = word.Length;

            // Early exit if word is longer than text
            if (wordLen > textLen) return 0;

            int textIdx = 0;
            int wordIdx = 0;
            double score = 0;
            int lastMatchPos = -1;

            // Try to find consecutive characters of word in text
            while (textIdx < textLen && wordIdx < wordLen)
            {
                if (char.ToLower(text[textIdx]) == char.ToLower(word[wordIdx]))
                {
                    // Calculate score based on position and distance from last match
                    double posScore = 1.0 - (textIdx / (double)textLen);
                    double distanceScore = lastMatchPos == -1 ? 1.0 : 1.0 / (textIdx - lastMatchPos);

                    score += posScore * distanceScore;

                    lastMatchPos = textIdx;
                    wordIdx++;
                }
                textIdx++;
            }

            // Return 0 if not all characters were found
            if (wordIdx != wordLen) return 0;

            // Bonus for prefix matches
            if (text.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                score *= 2;
            }

            // Length penalty - prefer shorter matches
            score *= (1.0 / Math.Log(textLen + 2));

            return score;
        }
    }
}
