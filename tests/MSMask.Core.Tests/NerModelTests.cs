using MSMask.Core.Ner;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for the model side of NER: <see cref="WordPieceTokenizer"/>, <see cref="EntityAggregator"/>
    /// and <see cref="BertEntityRecognizer"/>.
    /// </summary>
    public class NerModelTests
    {
        /// <summary>
        /// A tiny vocabulary: the special tokens, then a few words and word pieces.
        /// </summary>
        private static readonly string[] Vocabulary =
        [
            "[PAD]", "[UNK]", "[CLS]", "[SEP]", "[MASK]",
            "Ay", "##şe", "Yılmaz", "Kadıköy", "'", "de", ":", "😀", "Kim", "##den",
        ];

        /// <summary>
        /// Creates a tokenizer over <see cref="Vocabulary"/>.
        /// </summary>
        /// <returns>The tokenizer.</returns>
        private static WordPieceTokenizer Tokenizer() => new(Vocabulary);

        /// <summary>
        /// Shortens a piece list to <c>"text@start-end"</c> strings for easy comparison.
        /// </summary>
        /// <param name="pieces">The pieces.</param>
        /// <returns>One string per piece.</returns>
        private static string[] Describe(IEnumerable<WordPiece> pieces) =>
            pieces.Select(p => $"{p.Text}@{p.Start}-{p.End}").ToArray();

        /// <summary>
        /// Words are cut into the longest vocabulary pieces, continuation pieces get "##",
        /// and every piece keeps its position in the original text.
        /// </summary>
        [Fact]
        public void Tokenize_SplitsWordsIntoPiecesWithPositions()
        {
            List<WordPiece> pieces = Tokenizer().Tokenize("Kimden: Ayşe Yılmaz");

            Assert.Equal(["Kim@0-3", "##den@3-6", ":@6-7", "Ay@8-10", "##şe@10-12", "Yılmaz@13-19"], Describe(pieces));
            Assert.Equal([13, 14, 11, 5, 6, 7], pieces.Select(p => p.Id));
        }

        /// <summary>
        /// Punctuation is a word of its own, so a suffix after an apostrophe is a separate word.
        /// </summary>
        [Fact]
        public void Tokenize_SplitsAtPunctuation()
        {
            List<WordPiece> pieces = Tokenizer().Tokenize("Kadıköy'de");

            Assert.Equal(["Kadıköy@0-7", "'@7-8", "de@8-10"], Describe(pieces));
        }

        /// <summary>
        /// A word that cannot be built from the vocabulary becomes one unknown piece over the whole word.
        /// </summary>
        [Fact]
        public void Tokenize_UnknownWord_BecomesUnknownPiece()
        {
            List<WordPiece> pieces = Tokenizer().Tokenize("Ay Ayx");

            Assert.Equal(["Ay@0-2", "[UNK]@3-6"], Describe(pieces));
            Assert.Equal(1, pieces[1].Id);
        }

        /// <summary>
        /// A character made of two UTF-16 units (an emoji) is never cut in half and keeps both units in its range.
        /// </summary>
        [Fact]
        public void Tokenize_SurrogatePair_StaysWhole()
        {
            List<WordPiece> pieces = Tokenizer().Tokenize("Ay 😀");

            Assert.Equal(["Ay@0-2", "😀@3-5"], Describe(pieces));
        }

        /// <summary>
        /// Control characters are dropped and tabs or line breaks separate words like spaces.
        /// </summary>
        [Fact]
        public void SplitWords_DropsControlCharactersAndSplitsAtWhitespace()
        {
            List<(int, int)> words = WordPieceTokenizer.SplitWords("Ay\tde\u0001Kim");

            Assert.Equal([(0, 2), (3, 5), (6, 9)], words);
        }

        /// <summary>
        /// A vocabulary without the special tokens cannot be used with a BERT model.
        /// </summary>
        [Fact]
        public void Constructor_MissingSpecialToken_Throws()
        {
            Assert.Throws<ArgumentException>(() => new WordPieceTokenizer(["[PAD]", "[UNK]", "[CLS]"]));
        }

        /// <summary>
        /// Every piece of a word takes the word's first label, so a name whose pieces are all labeled B-PER
        /// stays one name, and the next word with I-PER continues it.
        /// </summary>
        [Fact]
        public void Aggregate_JoinsPiecesIntoWordsBeforeGrouping()
        {
            // "Eriş Aksu ile" read as Er ##iş Aksu ile.
            TokenPrediction[] pieces =
            [
                new("B-PER", 1.0, 0, 2),
                new("B-PER", 0.9, 2, 4, IsSubword: true),
                new("I-PER", 0.8, 5, 9),
                new("O", 1.0, 10, 13),
            ];

            NerEntity entity = Assert.Single(EntityAggregator.Aggregate(pieces));

            Assert.Equal(new NerEntity("PER", 0, 9, 0.9), entity with { Score = Math.Round(entity.Score, 9) });
        }

        /// <summary>
        /// Neighbouring words of the same type form one entity even when both start with B-; a change of type or
        /// an outside word ends the entity, and outside words are dropped.
        /// </summary>
        [Fact]
        public void Aggregate_JoinsSameTypeAndSplitsAtTypeChange()
        {
            TokenPrediction[] pieces =
            [
                new("B-PER", 0.9, 0, 4),
                new("B-PER", 0.9, 5, 9),
                new("I-LOC", 0.8, 10, 15),
                new("O", 1.0, 16, 19),
                new("B-LOC", 0.7, 20, 25),
                new("I-LOC", 0.9, 26, 30),
            ];

            List<NerEntity> entities = EntityAggregator.Aggregate(pieces);

            Assert.Equal(["PER 0-9", "LOC 10-15", "LOC 20-30"], entities.Select(e => $"{e.Label} {e.Start}-{e.End}"));
            Assert.Equal(0.8, entities[2].Score, 9);
        }

        /// <summary>
        /// Softmax gives probabilities that sum to 1, keeps the order of the scores and does not overflow
        /// on large scores.
        /// </summary>
        [Fact]
        public void Softmax_IsStableAndSumsToOne()
        {
            double[] p = BertEntityRecognizer.Softmax([1000f, 999f, 0f]);

            Assert.Equal(1.0, p.Sum(), 9);
            Assert.True(p[0] > p[1] && p[1] > p[2]);
            Assert.Equal(1 / (1 + Math.Exp(-1)), p[0], 6);
        }

        /// <summary>
        /// A missing model folder gives a clear error.
        /// </summary>
        [Fact]
        public void Recognizer_FolderWithoutModel_Throws()
        {
            string empty = Path.Combine(Path.GetTempPath(), "msmask-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                Assert.Throws<FileNotFoundException>(() => new BertEntityRecognizer(empty));
            }
            finally
            {
                Directory.Delete(empty);
            }
        }

        /// <summary>
        /// The real model finds a person name and a place in a Turkish sentence.
        /// </summary>
        /// <returns>A task that completes when the test is done.</returns>
        [NerModelFact]
        public async Task Recognizer_RealModel_FindsPersonAndPlace()
        {
            using var recognizer = new BertEntityRecognizer();
            const string text = "Ayşe Yılmaz ile Kadıköy'de buluştuk.";

            IReadOnlyList<IReadOnlyList<NerEntity>> result = await recognizer.RecognizeAsync([text]);

            Assert.Contains(result[0], e => e.Label == "PER" && text[e.Start..e.End] == "Ayşe Yılmaz");
            Assert.Contains(result[0], e => e.Label == "LOC" && text[e.Start..e.End] == "Kadıköy");
        }
    }
}