using MSMask.Core.Models;
using MSMask.Core.Patterns;

namespace MSMask.Core.Lines
{
    /// <summary>
    /// Represents one visual line of text: OCR words that sit next to each other on the same row.
    /// </summary>
    /// <remarks>
    /// The pattern rules work on plain strings, but masking needs pixel boxes. A line keeps both:
    /// <see cref="Text"/> is the words joined with single spaces, and <see cref="Offsets"/> records where each
    /// word sits inside that string. <see cref="SpanBox"/> uses the offsets to turn a character range found
    /// by a rule back into a box on the image.
    /// </remarks>
    public sealed class TextLine
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextLine"/> class.
        /// </summary>
        /// <param name="lineId">The index of the line, counted from the top of the image.</param>
        /// <param name="tokens">The words of the line, from left to right.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokens"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="tokens"/> is empty.</exception>
        public TextLine(int lineId, IReadOnlyList<Token> tokens)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            if (tokens.Count == 0)
            {
                throw new ArgumentException("A line must contain at least one token.", nameof(tokens));
            }

            LineId = lineId;
            Tokens = tokens;

            var offsets = new List<TextSpan>(tokens.Count);
            int pos = 0;
            foreach (Token token in tokens)
            {
                if (offsets.Count > 0)
                {
                    pos++; // the joining space
                }
                offsets.Add(new TextSpan(pos, pos + token.Text.Length));
                pos += token.Text.Length;
            }
            Offsets = offsets;
            Text = string.Join(' ', tokens.Select(t => t.Text));
        }

        /// <summary>
        /// Gets the index of the line, counted from the top of the image.
        /// </summary>
        public int LineId { get; }

        /// <summary>
        /// Gets the words of the line, from left to right.
        /// </summary>
        public IReadOnlyList<Token> Tokens { get; }

        /// <summary>
        /// Gets the text of the line: the words joined with single spaces.
        /// </summary>
        /// <value>For example <c>"Tel: 0532 123 45 67"</c>.</value>
        public string Text { get; }

        /// <summary>
        /// Gets the character range of each word inside <see cref="Text"/>.
        /// </summary>
        /// <value>One range per token, in the same order as <see cref="Tokens"/>.</value>
        public IReadOnlyList<TextSpan> Offsets { get; }

        /// <summary>
        /// Gets the bounding box of the whole line.
        /// </summary>
        /// <value>The union of all word boxes.</value>
        public Box Box => Box.UnionAll(Tokens.Select(t => t.Box));

        /// <summary>
        /// Converts a character range of <see cref="Text"/> into a box on the image.
        /// </summary>
        /// <remarks>
        /// Every word that overlaps the range contributes a box. A word that is fully inside the range
        /// contributes its whole box. A word that is only partly inside (for example the IBAN inside
        /// <c>OdemeTR33...hesabina</c>, where OCR dropped the spaces) is cropped horizontally in proportion
        /// to its character count, assuming every character has the same width.
        /// </remarks>
        /// <param name="start">The index of the first character in the range.</param>
        /// <param name="end">The index just after the last character in the range.</param>
        /// <returns>
        /// The union of the contributing word boxes, or <see langword="null"/> when no word overlaps the range
        /// (for example when the range covers only a joining space).
        /// </returns>
        public Box? SpanBox(int start, int end)
        {
            var boxes = new List<Box>();
            for (int i = 0; i < Tokens.Count; i++)
            {
                Box box = Tokens[i].Box;
                int a = Offsets[i].Start;
                int b = Offsets[i].End;
                int s = Math.Max(a, start);
                int e = Math.Min(b, end);
                if (e <= s)
                {
                    continue;
                }

                if (s == a && e == b)
                {
                    boxes.Add(box);
                }
                else
                {
                    double n = b - a;
                    double w = box.Width;
                    boxes.Add(new Box(box.X0 + w * (s - a) / n, box.Y0, box.X0 + w * (e - a) / n, box.Y1));
                }
            }
            return boxes.Count > 0 ? Box.UnionAll(boxes) : null;
        }
    }
}
