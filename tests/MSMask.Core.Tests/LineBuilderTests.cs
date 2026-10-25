using MSMask.Core.Lines;
using MSMask.Core.Models;
using MSMask.Core.Patterns;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="LineBuilder"/> and <see cref="TextLine"/>.
    /// </summary>
    public class LineBuilderTests
    {
        /// <summary>
        /// Turns a space-separated text into word tokens on one row, the way an OCR engine would report them.
        /// </summary>
        /// <param name="text">The words, separated by single spaces, for example <c>"0532 123 45 67"</c>.</param>
        /// <param name="x">The left edge of the first word.</param>
        /// <param name="y">The top edge of every word.</param>
        /// <param name="height">The height of every word box.</param>
        /// <param name="charWidth">The width of one character.</param>
        /// <param name="gap">The horizontal space between neighbouring words.</param>
        /// <returns>One token per word, from left to right.</returns>
        private static List<Token> Words(string text, double x = 10, double y = 10, double height = 20,
            double charWidth = 10, double gap = 10)
        {
            var tokens = new List<Token>();
            foreach (string word in text.Split(' '))
            {
                double width = charWidth * word.Length;
                tokens.Add(new Token(word, new Box(x, y, x + width, y + height)));
                x += width + gap;
            }
            return tokens;
        }

        /// <summary>
        /// A phone number read as four words maps back to the union of those four word boxes.
        /// </summary>
        [Fact]
        public void SpanBox_MultiWordPhone_ReturnsUnionOfWordBoxes()
        {
            List<Token> tokens = Words("Tel: 0532 123 45 67");
            List<TextLine> lines = LineBuilder.Build(tokens);

            TextLine line = Assert.Single(lines);
            PatternMatch match = Assert.Single(PatternMatcher.FindMatches(line.Text));
            Assert.Equal(Kinds.Telefon, match.Kind);
            Assert.Equal(Box.UnionAll(tokens.Skip(1).Select(t => t.Box)), line.SpanBox(match.Start, match.End));
        }

        /// <summary>
        /// A match inside a single word is cropped in proportion to its character count.
        /// </summary>
        [Fact]
        public void SpanBox_MatchInsideWord_IsCroppedProportionally()
        {
            // "Odeme" + IBAN + "x": OCR dropped the spaces, so the IBAN is in the middle of one word.
            const string word = "OdemeTR330006100519786457841326x";
            var token = new Token(word, new Box(100, 0, 100 + 10 * word.Length, 20));
            TextLine line = Assert.Single(LineBuilder.Build([token]));

            PatternMatch match = Assert.Single(PatternMatcher.FindMatches(line.Text));
            Assert.Equal(Kinds.Iban, match.Kind);
            Box? box = line.SpanBox(match.Start, match.End);
            Assert.NotNull(box);
            Assert.Equal(100 + 10 * 5, box.Value.X0, precision: 9);
            Assert.Equal(100 + 10 * (5 + 26), box.Value.X1, precision: 9);
            Assert.Equal(0, box.Value.Y0);
            Assert.Equal(20, box.Value.Y1);
        }

        /// <summary>
        /// Words that the OCR engine put into different blocks are joined when they sit on the same row.
        /// </summary>
        [Fact]
        public void Build_KeyAndValueFromDifferentOcrBlocks_AreJoined()
        {
            // Tesseract psm 11 can put "password" in block 1 and "=" and the value in block 7.
            var tokens = new List<Token>
            {
                new("password", new Box(10, 10, 90, 30), LineId: 1001),
                new("=", new Box(100, 12, 110, 28), LineId: 7001),
                new("hunter22", new Box(120, 11, 200, 31), LineId: 7001),
            };
            TextLine line = Assert.Single(LineBuilder.Build(tokens));
            Assert.Equal("password = hunter22", line.Text);

            PatternMatch match = Assert.Single(PatternMatcher.FindMatches(line.Text));
            Assert.Equal("hunter22", match.Text);
            Assert.Equal(new Box(120, 11, 200, 31), line.SpanBox(match.Start, match.End));
        }

        /// <summary>
        /// Columns on the same row with a wide gap between them stay separate lines.
        /// </summary>
        [Fact]
        public void Build_TableColumns_AreSeparateLines()
        {
            List<Token> left = Words("Ahmet", x: 10);
            List<Token> right = Words("0532 123 45 67", x: 10 + 50 + 200); // a 200 px gap, far more than 2 x 20
            List<TextLine> lines = LineBuilder.Build([.. left, .. right]);
            Assert.Equal(["Ahmet", "0532 123 45 67"], lines.Select(l => l.Text));
        }

        /// <summary>
        /// Rows are separated by their vertical position and returned from top to bottom.
        /// </summary>
        [Fact]
        public void Build_Rows_AreSortedFromTopToBottom()
        {
            List<Token> row1 = Words("TC 10000000146", y: 10);
            List<Token> row2 = Words("Tel 0532 123 45 67", y: 40);
            List<TextLine> lines = LineBuilder.Build([.. row2, .. row1]);
            Assert.Equal(["TC 10000000146", "Tel 0532 123 45 67"], lines.Select(l => l.Text));
        }

        /// <summary>
        /// A small vertical shift keeps a word on the same line (overlap 16 / 20 = 0.8).
        /// </summary>
        [Fact]
        public void Build_SmallVerticalShift_StaysOnSameLine()
        {
            var a = new Token("ayhan4", new Box(10, 10, 70, 30));
            var b = new Token("8@edu.tr", new Box(75, 14, 155, 34));
            TextLine line = Assert.Single(LineBuilder.Build([b, a]));
            Assert.Equal("ayhan4 8@edu.tr", line.Text);
        }

        /// <summary>
        /// Less than half vertical overlap starts a new line (overlap 8 / 20 = 0.4).
        /// </summary>
        [Fact]
        public void Build_LessThanHalfOverlap_StartsNewLine()
        {
            var a = new Token("abc", new Box(10, 10, 40, 30));
            var b = new Token("def", new Box(45, 22, 75, 42));
            Assert.Equal(2, LineBuilder.Build([a, b]).Count);
        }

        /// <summary>
        /// Line ids follow the output order and are copied into every token.
        /// </summary>
        [Fact]
        public void Build_AssignsLineIdsToLinesAndTokens()
        {
            List<TextLine> lines = LineBuilder.Build([.. Words("b c", y: 50), .. Words("a", y: 10)]);
            Assert.Equal([0, 1], lines.Select(l => l.LineId));
            Assert.All(lines, l => Assert.All(l.Tokens, t => Assert.Equal(l.LineId, t.LineId)));
        }

        /// <summary>
        /// Empty and whitespace-only words are ignored; no words give no lines.
        /// </summary>
        [Fact]
        public void Build_EmptyWords_AreIgnored()
        {
            Assert.Empty(LineBuilder.Build([]));
            Assert.Empty(LineBuilder.Build([new Token(" ", new Box(0, 0, 10, 10))]));

            var tokens = new List<Token> { new("", new Box(0, 0, 5, 20)), new("abc", new Box(10, 0, 40, 20)) };
            Assert.Equal("abc", Assert.Single(LineBuilder.Build(tokens)).Text);
        }

        /// <summary>
        /// Offsets give the position of each word inside the joined text.
        /// </summary>
        [Fact]
        public void TextLine_Offsets_PointToEachWord()
        {
            var line = new TextLine(0, Words("ab cde f"));
            Assert.Equal("ab cde f", line.Text);
            Assert.Equal([new TextSpan(0, 2), new TextSpan(3, 6), new TextSpan(7, 8)], line.Offsets);
        }

        /// <summary>
        /// A range that covers only the joining space between two words maps to no box.
        /// </summary>
        [Fact]
        public void SpanBox_OnlyJoiningSpace_ReturnsNull()
        {
            var line = new TextLine(0, Words("ab cd"));
            Assert.Null(line.SpanBox(2, 3));
        }

        /// <summary>
        /// A line without tokens cannot be created.
        /// </summary>
        [Fact]
        public void TextLine_NoTokens_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TextLine(0, []));
        }

        /// <summary>
        /// The median ignores zero-height boxes and averages the two middle values for an even count.
        /// </summary>
        [Fact]
        public void MedianHeight_EvenCountAndZeroHeights()
        {
            var tokens = new List<Token>
            {
                new("a", new Box(0, 0, 1, 10)),
                new("b", new Box(0, 0, 1, 30)),
                new("c", new Box(0, 0, 1, 20)),
                new("d", new Box(0, 0, 1, 40)),
                new("e", new Box(0, 5, 1, 5)), // zero height
            };
            Assert.Equal(25, LineBuilder.MedianHeight(tokens));
            Assert.Equal(0, LineBuilder.MedianHeight([]));
        }

        /// <summary>
        /// The vertical overlap is measured relative to the shorter box.
        /// </summary>
        [Fact]
        public void VerticalOverlapRatio_RelativeToShorterBox()
        {
            Assert.Equal(1.0, LineBuilder.VerticalOverlapRatio(new Box(0, 0, 10, 40), new Box(20, 10, 30, 20)));
            Assert.Equal(0.8, LineBuilder.VerticalOverlapRatio(new Box(10, 10, 70, 30), new Box(75, 14, 155, 34)), precision: 9);
            Assert.Equal(0.0, LineBuilder.VerticalOverlapRatio(new Box(0, 0, 10, 10), new Box(0, 10, 10, 20)));
        }
    }
}