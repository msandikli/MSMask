using MSMask.Core.Models;
using MSMask.Core.Ocr;
using SkiaSharp;

namespace MSMask.Core.Tests.Fakes
{
    /// <summary>
    /// An OCR engine for tests that ignores the image and always returns the same words.
    /// </summary>
    /// <remarks>
    /// It lets the pipeline be tested without a real OCR engine: the test decides exactly which words are
    /// "read", so any failure is in the pipeline and not in the recognition.
    /// </remarks>
    public sealed class FakeOcrEngine : IOCREngine
    {
        /// <summary>
        /// The words returned by every call.
        /// </summary>
        private readonly List<Token> _tokens;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeOcrEngine"/> class.
        /// </summary>
        /// <param name="tokens">The words to return from every call.</param>
        public FakeOcrEngine(IEnumerable<Token> tokens)
        {
            _tokens = tokens.ToList();
        }

        /// <inheritdoc/>
        public string Name => "fake";

        /// <summary>
        /// Gets the number of times <see cref="ReadAsync"/> was called.
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc/>
        public Task<IReadOnlyList<Token>> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult<IReadOnlyList<Token>>(_tokens.ToList());
        }

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
        public static List<Token> Words(string text, double x = 10, double y = 10, double height = 20,
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
    }
}