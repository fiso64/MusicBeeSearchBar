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

            // 1. Phrase Match Bonus
            // If the text contains the full query phrase (e.g. "time tra" inside "time traveller"),
            // this is a very strong signal.
            if (text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 2.0;
            }

            foreach (string word in queryWords)
            {
                if (string.IsNullOrEmpty(word)) continue;

                // Handle short words with direct containment check
                if (word.Length < NGRAM_SIZE)
                {
                    if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        score += 1.0;
                    }
                    continue;
                }

                // OPTIMIZATION: 
                // Instead of creating a HashSet for the target 'text' (which allocates memory for every single track),
                // we calculate the n-grams for the 'query word' and scan the text for them.
                // This is mathematically equivalent to (QueryNgrams Intersect TextNgrams) / QueryNgrams.Count,
                // but avoids thousands of string allocations per search.

                int wordBigramCount = word.Length - NGRAM_SIZE + 1;
                int matchCount = 0;

                // We use a small HashSet just for the query word to ensure we don't double-count 
                // if the query has repeating bigrams (e.g. "baba" -> "ba", "ab", "ba").
                // Since query words are short, this struct-based allocation is negligible.
                var uniqueQueryBigrams = new HashSet<string>();

                for (int i = 0; i < wordBigramCount; i++)
                {
                    uniqueQueryBigrams.Add(word.Substring(i, NGRAM_SIZE));
                }

                foreach (var bigram in uniqueQueryBigrams)
                {
                    // Check if the text contains this bigram
                    if (text.IndexOf(bigram, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        matchCount++;
                    }
                }

                if (matchCount > 0)
                {
                    // Overlap Coefficient
                    // Calculates percentage of the QUERY WORD found in the TEXT.
                    double matchQuality = (double)matchCount / uniqueQueryBigrams.Count;

                    if (matchQuality > 1.0) matchQuality = 1.0;
                    score += matchQuality;

                    // Word Boundary Bonus
                    // Check if the word starts at index 0 OR is preceded by a space
                    int index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
                    if (index > -1)
                    {
                        // If it starts the string or starts a word within the string
                        if (index == 0 || text[index - 1] == ' ')
                        {
                            score += 0.5;
                        }
                    }
                }
            }

            // Bonus for exact matches
            if (text.Length == query.Length && text.Equals(query, StringComparison.OrdinalIgnoreCase))
            {
                score *= 2;
            }

            // Apply a small penalty for longer text to prioritize concise matches
            // We use Log to ensure longer titles aren't penalized too heavily, just enough to break ties
            score *= (1.0 / (1.0 + 0.1 * Math.Log(text.Length + 1)));

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
