using System.Text.RegularExpressions;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// Extends a secret match with the chunks that OCR split off with single spaces.
    /// </summary>
    /// <remarks>
    /// OCR engines often insert a space inside long random strings: <c>sk-proj-oL LcZUUFgbJ1T8</c> or
    /// <c>password: elma 723</c>. Without extension only the first chunk would be masked and the rest
    /// of the secret would stay visible. A chunk is added only when it looks random, so ordinary words
    /// after a secret (<c>bunu kimseyle paylaşma</c>) are not swallowed.
    /// </remarks>
    public static class ChunkExtender
    {
        /// <summary>
        /// Characters that open a quotation or bracket. A chunk that starts with one of them is not part of a secret.
        /// </summary>
        public const string Openers = "\"'“”‘’(<[{";

        /// <summary>
        /// Punctuation that may follow a secret, for example a sentence-ending period or a closing quote.
        /// </summary>
        public const string TrailingPunctuation = ".,;:!?\"'“”’)>]}";

        /// <summary>
        /// The default maximum number of chunks added to one match.
        /// </summary>
        public const int DefaultMaxChunks = 6;

        /// <summary>
        /// A single space followed by a chunk of non-space characters, anchored at the search position.
        /// </summary>
        private static readonly Regex NextChunk = PatternRule.CreateRegex(@"\G ([^\s]+)");

        /// <summary>
        /// Determines whether a chunk looks like a piece of a key or password rather than an ordinary word.
        /// </summary>
        /// <remarks>
        /// A chunk looks random when, after removing trailing punctuation, it contains a digit or an upper-case
        /// letter right after a lower-case letter (<c>LcZUUF</c>). Chunks that start with a quote or bracket and
        /// chunks that contain <c>://</c> (URLs) never look random.
        /// </remarks>
        /// <param name="chunk">The chunk to test, without surrounding spaces.</param>
        /// <returns><see langword="true"/> if the chunk looks random; otherwise <see langword="false"/>.</returns>
        public static bool LooksRandom(string chunk)
        {
            if (chunk.Length == 0 || Openers.Contains(chunk[0]) || chunk.Contains("://", StringComparison.Ordinal))
            {
                return false;
            }

            string c = chunk.TrimEnd(TrailingPunctuation.ToCharArray());
            if (c.Length == 0)
            {
                return false;
            }
            if (c.Any(Validators.IsAsciiDigit))
            {
                return true;
            }
            for (int i = 1; i < c.Length; i++)
            {
                if (char.IsUpper(c[i]) && char.IsLower(c[i - 1]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Moves the end of a match to the right over random-looking chunks separated by single spaces.
        /// </summary>
        /// <param name="text">The line of text that contains the match.</param>
        /// <param name="end">The current end index of the match.</param>
        /// <param name="maxChunks">The maximum number of chunks to add.</param>
        /// <returns>The new end index; equal to <paramref name="end"/> when no chunk was added.</returns>
        public static int ExtendSpan(string text, int end, int maxChunks = DefaultMaxChunks)
        {
            for (int i = 0; i < maxChunks; i++)
            {
                Match m = NextChunk.Match(text, end);
                if (!m.Success || !LooksRandom(m.Groups[1].Value))
                {
                    break;
                }
                end = m.Groups[1].Index + m.Groups[1].Length;
            }
            return end;
        }
    }
}