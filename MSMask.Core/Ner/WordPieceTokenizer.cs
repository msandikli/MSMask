using System.Globalization;

namespace MSMask.Core.Ner
{
    /// <summary>
    /// Splits text into the pieces a BERT model reads, keeping the position of every piece in the original text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BERT does not read words but pieces from a fixed vocabulary of about 32,000 entries. Tokenization has two stages:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Basic split</b>: control characters are dropped, the text is split at whitespace, and every punctuation
    /// mark and every CJK ideograph becomes a word of its own, so <c>"Kadıköy'de"</c> becomes <c>Kadıköy</c>,
    /// <c>'</c>, <c>de</c>. Letters are kept as they are, because the model is cased (<c>"Ayşe"</c> and
    /// <c>"ayşe"</c> are different).
    /// </description></item>
    /// <item><description>
    /// <b>WordPiece</b>: every word is cut greedily into the longest pieces found in the vocabulary. Pieces after the
    /// first carry the <c>"##"</c> prefix, so <c>"Kimden"</c> may become <c>Kim</c>, <c>##den</c>.
    /// A word that cannot be built from vocabulary pieces becomes <see cref="UnknownToken"/>.
    /// </description></item>
    /// </list>
    /// <para>
    /// The positions are what make the result useful here: an entity found by the model can be traced back to a
    /// character range of the OCR line, and from there to a box in the image.
    /// </para>
    /// <para>
    /// One simplification: an invisible dropped character inside a word (for example a zero-width space) ends the
    /// word here, while the standard BERT tokenizer joins the two halves. OCR output never contains such characters.
    /// </para>
    /// </remarks>
    public sealed class WordPieceTokenizer
    {
        /// <summary>
        /// The piece used for a word that cannot be built from the vocabulary.
        /// </summary>
        public const string UnknownToken = "[UNK]";

        /// <summary>
        /// The piece placed before the text.
        /// </summary>
        public const string ClassToken = "[CLS]";

        /// <summary>
        /// The piece placed after the text.
        /// </summary>
        public const string SeparatorToken = "[SEP]";

        /// <summary>
        /// The piece used to fill shorter texts up to the length of the longest text in a batch.
        /// </summary>
        public const string PaddingToken = "[PAD]";

        /// <summary>
        /// The prefix of a piece that continues a word.
        /// </summary>
        public const string ContinuationPrefix = "##";

        /// <summary>
        /// Words longer than this many characters become <see cref="UnknownToken"/> without being cut.
        /// </summary>
        public const int MaxWordLength = 100;

        /// <summary>
        /// Maps vocabulary entries to their ids.
        /// </summary>
        private readonly Dictionary<string, int> _vocab;

        /// <summary>
        /// Initializes a new instance of the <see cref="WordPieceTokenizer"/> class.
        /// </summary>
        /// <param name="vocabulary">The vocabulary entries in id order: the entry at index <c>i</c> has id <c>i</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vocabulary"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when a special token is missing from the vocabulary.</exception>
        public WordPieceTokenizer(IReadOnlyList<string> vocabulary)
        {
            ArgumentNullException.ThrowIfNull(vocabulary);
            _vocab = new Dictionary<string, int>(vocabulary.Count, StringComparer.Ordinal);
            for (int i = 0; i < vocabulary.Count; i++)
            {
                // The first occurrence wins, like in the original implementation.
                _vocab.TryAdd(vocabulary[i], i);
            }

            UnknownId = IdOf(UnknownToken);
            ClassId = IdOf(ClassToken);
            SeparatorId = IdOf(SeparatorToken);
            PaddingId = IdOf(PaddingToken);
        }

        /// <summary>
        /// Gets the id of <see cref="UnknownToken"/>.
        /// </summary>
        public int UnknownId { get; }

        /// <summary>
        /// Gets the id of <see cref="ClassToken"/>.
        /// </summary>
        public int ClassId { get; }

        /// <summary>
        /// Gets the id of <see cref="SeparatorToken"/>.
        /// </summary>
        public int SeparatorId { get; }

        /// <summary>
        /// Gets the id of <see cref="PaddingToken"/>.
        /// </summary>
        public int PaddingId { get; }

        /// <summary>
        /// Gets the number of vocabulary entries.
        /// </summary>
        public int VocabularySize => _vocab.Count;

        /// <summary>
        /// Reads a <c>vocab.txt</c> file with one vocabulary entry per line.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The tokenizer.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
        /// <exception cref="ArgumentException">Thrown when a special token is missing from the vocabulary.</exception>
        public static WordPieceTokenizer FromVocabularyFile(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("The vocabulary file was not found.", path);
            }
            return new WordPieceTokenizer(File.ReadAllLines(path));
        }

        /// <summary>
        /// Splits a text into vocabulary pieces.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>The pieces in reading order, without <see cref="ClassToken"/> and <see cref="SeparatorToken"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
        public List<WordPiece> Tokenize(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            var pieces = new List<WordPiece>();
            foreach ((int start, int end) in SplitWords(text))
            {
                AddWordPieces(text, start, end, pieces);
            }
            return pieces;
        }

        /// <summary>
        /// Splits a text into words: at whitespace, around punctuation and CJK ideographs, and without control characters.
        /// </summary>
        /// <param name="text">The text to split.</param>
        /// <returns>The <c>(start, end)</c> character range of every word.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
        public static List<(int Start, int End)> SplitWords(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            var words = new List<(int, int)>();
            int wordStart = -1;
            int i = 0;
            while (i < text.Length)
            {
                int length = char.IsSurrogatePair(text, i) ? 2 : 1;
                // A lone surrogate half is broken text; it is dropped like a control character.
                int codePoint = char.IsSurrogate(text[i]) && length == 1 ? 0 : char.ConvertToUtf32(text, i);

                if (IsIgnored(codePoint) || IsWhitespace(codePoint))
                {
                    // A dropped character ends the current word, like a space.
                    CloseWord(ref wordStart, i, words);
                }
                else if (IsPunctuation(codePoint) || IsCjk(codePoint))
                {
                    CloseWord(ref wordStart, i, words);
                    words.Add((i, i + length));
                }
                else if (wordStart < 0)
                {
                    wordStart = i;
                }
                i += length;
            }
            CloseWord(ref wordStart, text.Length, words);
            return words;
        }

        /// <summary>
        /// Gets the id of a vocabulary entry.
        /// </summary>
        /// <param name="token">The entry.</param>
        /// <returns>The id.</returns>
        /// <exception cref="ArgumentException">Thrown when the entry is not in the vocabulary.</exception>
        public int IdOf(string token) =>
            _vocab.TryGetValue(token, out int id) ? id : throw new ArgumentException($"The vocabulary has no '{token}' entry.", nameof(token));

        /// <summary>
        /// Cuts one word into the longest vocabulary pieces, from left to right.
        /// </summary>
        /// <param name="text">The whole text.</param>
        /// <param name="start">The start of the word.</param>
        /// <param name="end">The end of the word.</param>
        /// <param name="pieces">The list the pieces are added to.</param>
        private void AddWordPieces(string text, int start, int end, List<WordPiece> pieces)
        {
            if (CodePointCount(text, start, end) > MaxWordLength)
            {
                pieces.Add(new WordPiece(UnknownId, UnknownToken, start, end));
                return;
            }

            var wordPieces = new List<WordPiece>();
            int pieceStart = start;
            while (pieceStart < end)
            {
                WordPiece? found = null;
                // Try the longest candidate first and shorten it one character at a time.
                for (int pieceEnd = end; pieceEnd > pieceStart; pieceEnd--)
                {
                    if (pieceEnd < end && char.IsLowSurrogate(text[pieceEnd]))
                    {
                        // Never cut between the two halves of a surrogate pair.
                        continue;
                    }
                    string candidate = text[pieceStart..pieceEnd];
                    if (pieceStart > start)
                    {
                        candidate = ContinuationPrefix + candidate;
                    }
                    if (_vocab.TryGetValue(candidate, out int id))
                    {
                        found = new WordPiece(id, candidate, pieceStart, pieceEnd);
                        break;
                    }
                }

                if (found is null)
                {
                    // One unknown part makes the whole word unknown.
                    pieces.Add(new WordPiece(UnknownId, UnknownToken, start, end));
                    return;
                }
                wordPieces.Add(found);
                pieceStart = found.End;
            }
            pieces.AddRange(wordPieces);
        }

        /// <summary>
        /// Adds the current word, if any, and resets the word start.
        /// </summary>
        /// <param name="wordStart">The start of the current word, or -1 when there is none.</param>
        /// <param name="end">The end of the current word.</param>
        /// <param name="words">The list the word is added to.</param>
        private static void CloseWord(ref int wordStart, int end, List<(int, int)> words)
        {
            if (wordStart >= 0)
            {
                words.Add((wordStart, end));
                wordStart = -1;
            }
        }

        /// <summary>
        /// Counts the characters of a range, counting a surrogate pair as one character.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="start">The start of the range.</param>
        /// <param name="end">The end of the range.</param>
        /// <returns>The number of Unicode code points in the range.</returns>
        private static int CodePointCount(string text, int start, int end)
        {
            int count = 0;
            for (int i = start; i < end; i++)
            {
                if (!char.IsLowSurrogate(text[i]))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Determines whether a character is dropped before splitting: NUL, the replacement character and control
        /// or format characters other than tab, line feed and carriage return.
        /// </summary>
        /// <param name="codePoint">The character.</param>
        /// <returns><see langword="true"/> if the character is dropped.</returns>
        private static bool IsIgnored(int codePoint)
        {
            if (codePoint is 0 or 0xFFFD)
            {
                return true;
            }
            if (codePoint is '\t' or '\n' or '\r')
            {
                return false;
            }
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
            return category is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
                or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned;
        }

        /// <summary>
        /// Determines whether a character separates words.
        /// </summary>
        /// <param name="codePoint">The character.</param>
        /// <returns><see langword="true"/> for whitespace.</returns>
        private static bool IsWhitespace(int codePoint) =>
            codePoint is ' ' or '\t' or '\n' or '\r'
            || (codePoint <= char.MaxValue && char.IsWhiteSpace((char)codePoint));

        /// <summary>
        /// Determines whether a character is punctuation: every ASCII symbol such as <c>$</c>, <c>+</c> or <c>@</c>,
        /// and every Unicode punctuation character.
        /// </summary>
        /// <param name="codePoint">The character.</param>
        /// <returns><see langword="true"/> for punctuation.</returns>
        private static bool IsPunctuation(int codePoint)
        {
            if (codePoint is >= 33 and <= 47 or >= 58 and <= 64 or >= 91 and <= 96 or >= 123 and <= 126)
            {
                return true;
            }
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(codePoint);
            return category is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
                or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
                or UnicodeCategory.OtherPunctuation;
        }

        /// <summary>
        /// Determines whether a character is a CJK ideograph, which BERT treats as a word of its own.
        /// </summary>
        /// <param name="codePoint">The character.</param>
        /// <returns><see langword="true"/> for a CJK ideograph.</returns>
        private static bool IsCjk(int codePoint) =>
            codePoint is >= 0x4E00 and <= 0x9FFF
                or >= 0x3400 and <= 0x4DBF
                or >= 0x20000 and <= 0x2A6DF
                or >= 0x2A700 and <= 0x2B73F
                or >= 0x2B740 and <= 0x2B81F
                or >= 0x2B820 and <= 0x2CEAF
                or >= 0xF900 and <= 0xFAFF
                or >= 0x2F800 and <= 0x2FA1F;
    }
}