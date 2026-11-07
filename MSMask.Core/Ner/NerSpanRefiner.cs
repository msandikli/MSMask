using MSMask.Core.Patterns;

namespace MSMask.Core.Ner
{
    /// <summary>
    /// Cleans up an entity range found by the model before it becomes a detection.
    /// </summary>
    /// <remarks>
    /// The rules come from error analysis on the development set:
    /// <list type="bullet">
    /// <item><description>
    /// Joined entities are split at <c>/</c> and <c>,</c>: <c>"Gebze / Kocaeli"</c> becomes two places, so each mask
    /// fits its own word.
    /// </description></item>
    /// <item><description>
    /// Words of at most two letters are trimmed from both ends: avatar initials such as <c>"AY"</c> in
    /// <c>"AY Ayşe Yılmaz"</c>, or a stray <c>"Sn."</c>.
    /// </description></item>
    /// <item><description>
    /// A part is dropped when it has fewer than two letters or does not start with a capital letter: names are
    /// capitalized, and OCR noise usually is not.
    /// </description></item>
    /// <item><description>
    /// A single word in capitals is dropped: log levels, UI labels and key names such as <c>"WARN"</c>,
    /// <c>"GITHUB"</c> or <c>"PASSWORD"</c> are often mistaken for names, while a real name is rarely a single word in capitals.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class NerSpanRefiner
    {
        /// <summary>
        /// Refines one entity range of a text.
        /// </summary>
        /// <param name="text">The whole text (an OCR line).</param>
        /// <param name="start">The start of the entity in <paramref name="text"/>.</param>
        /// <param name="end">The end of the entity in <paramref name="text"/>.</param>
        /// <returns>The cleaned ranges; empty when nothing is left.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the range is outside <paramref name="text"/>.</exception>
        public static List<TextSpan> Refine(string text, int start, int end)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(end, text.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);

            var result = new List<TextSpan>();
            foreach (TextSpan part in Split(text, start, end, c => c is '/' or ','))
            {
                List<TextSpan> words = Split(text, part.Start, part.End, char.IsWhiteSpace);
                while (words.Count > 1 && Letters(text, words[0]) <= 2)
                {
                    words.RemoveAt(0);
                }
                while (words.Count > 1 && Letters(text, words[^1]) <= 2)
                {
                    words.RemoveAt(words.Count - 1);
                }
                if (words.Count == 0)
                {
                    continue;
                }

                var span = new TextSpan(words[0].Start, words[^1].End);
                if (Letters(text, span) < 2 || !char.IsUpper(text[span.Start]))
                {
                    continue;
                }
                if (words.Count == 1 && IsAllCapitals(text, span))
                {
                    continue;
                }
                result.Add(span);
            }
            return result;
        }

        /// <summary>
        /// Splits a range into the runs of characters between separators.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="start">The start of the range.</param>
        /// <param name="end">The end of the range.</param>
        /// <param name="isSeparator">Decides which characters separate runs.</param>
        /// <returns>The non-empty runs, in order.</returns>
        private static List<TextSpan> Split(string text, int start, int end, Func<char, bool> isSeparator)
        {
            var runs = new List<TextSpan>();
            int runStart = -1;
            for (int i = start; i < end; i++)
            {
                if (isSeparator(text[i]))
                {
                    if (runStart >= 0)
                    {
                        runs.Add(new TextSpan(runStart, i));
                        runStart = -1;
                    }
                }
                else if (runStart < 0)
                {
                    runStart = i;
                }
            }
            if (runStart >= 0)
            {
                runs.Add(new TextSpan(runStart, end));
            }
            return runs;
        }

        /// <summary>
        /// Counts the letters in a range.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="span">The range.</param>
        /// <returns>The number of letters.</returns>
        private static int Letters(string text, TextSpan span)
        {
            int count = 0;
            for (int i = span.Start; i < span.End; i++)
            {
                if (char.IsLetter(text[i]))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Determines whether a range has at least one letter and no lower-case letter.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="span">The range.</param>
        /// <returns><see langword="true"/> for text such as <c>"WARN"</c> or <c>"API2"</c>.</returns>
        private static bool IsAllCapitals(string text, TextSpan span)
        {
            bool hasLetter = false;
            for (int i = span.Start; i < span.End; i++)
            {
                if (char.IsLower(text[i]))
                {
                    return false;
                }
                hasLetter |= char.IsLetter(text[i]);
            }
            return hasLetter;
        }
    }
}