using MSMask.Core.Models;

namespace MSMask.Core.Lines
{
    /// <summary>
    /// Groups OCR words into lines using only their geometry.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Line and block numbers reported by OCR engines are not trusted. In sparse-text mode (psm 11)
    /// Tesseract can put <c>password</c> and its value into different blocks, and a rule that needs both
    /// would then miss the secret.
    /// </para>
    /// <para>
    /// Words are visited from left to right. A word joins an existing line when its vertical overlap with the
    /// line is at least <c>minOverlap</c> and the horizontal gap to the line's last word is less than
    /// <c>gapFactor</c> times the median word height. A wide gap starts a new line, so table columns stay
    /// separate. When several lines qualify, the one with the larger overlap wins, then the one with the
    /// smaller gap.
    /// </para>
    /// </remarks>
    public static class LineBuilder
    {
        /// <summary>
        /// The default minimum vertical overlap ratio for a word to join a line.
        /// </summary>
        public const double DefaultMinOverlap = 0.5;

        /// <summary>
        /// The default maximum horizontal gap, as a multiple of the median word height.
        /// </summary>
        public const double DefaultGapFactor = 2.0;

        /// <summary>
        /// Groups words into lines.
        /// </summary>
        /// <param name="tokens">The words recognized by OCR, in any order. Words with empty or whitespace-only text are ignored.</param>
        /// <param name="minOverlap">The minimum vertical overlap ratio (see <see cref="VerticalOverlapRatio"/>) for a word to join a line.</param>
        /// <param name="gapFactor">The maximum horizontal gap between neighbouring words, as a multiple of the median word height.</param>
        /// <returns>
        /// The lines sorted from top to bottom (and from left to right for lines that start at the same height).
        /// <see cref="TextLine.LineId"/> is the index in this list, and every token is a copy with
        /// <see cref="Token.LineId"/> set to the same value.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokens"/> is <see langword="null"/>.</exception>
        public static List<TextLine> Build(IEnumerable<Token> tokens,
            double minOverlap = DefaultMinOverlap, double gapFactor = DefaultGapFactor)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            List<Token> words = tokens.Where(t => !string.IsNullOrWhiteSpace(t.Text)).ToList();
            if (words.Count == 0)
            {
                return [];
            }

            double medianHeight = MedianHeight(words);
            if (medianHeight == 0)
            {
                medianHeight = 1.0;
            }
            double maxGap = gapFactor * medianHeight;

            var groups = new List<List<Token>>();
            foreach (Token word in words.OrderBy(t => t.Box.X0).ThenBy(t => t.Box.Y0))
            {
                List<Token>? best = null;
                double bestOverlap = 0;
                double bestGap = 0;
                foreach (List<Token> group in groups)
                {
                    Token last = group[^1];
                    double gap = word.Box.X0 - last.Box.X1;
                    if (gap >= maxGap)
                    {
                        continue;
                    }

                    // Compare with the last word; for small words such as punctuation, use the whole line band instead.
                    Box reference = last.Box.Height >= 0.5 * medianHeight
                        ? last.Box
                        : Box.UnionAll(group.Select(t => t.Box));
                    double overlap = VerticalOverlapRatio(reference, word.Box);
                    if (overlap < minOverlap)
                    {
                        continue;
                    }

                    bool better = best is null
                        || overlap > bestOverlap
                        || (overlap == bestOverlap && Math.Abs(gap) < bestGap);
                    if (better)
                    {
                        best = group;
                        bestOverlap = overlap;
                        bestGap = Math.Abs(gap);
                    }
                }

                if (best is null)
                {
                    groups.Add([word]);
                }
                else
                {
                    best.Add(word);
                }
            }

            List<List<Token>> ordered = groups
                .OrderBy(g => g.Min(t => t.Box.Y0))
                .ThenBy(g => g[0].Box.X0)
                .ToList();

            var lines = new List<TextLine>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                int lineId = i;
                lines.Add(new TextLine(lineId, ordered[i].Select(t => t with { LineId = lineId }).ToList()));
            }
            return lines;
        }

        /// <summary>
        /// Computes how much two boxes overlap vertically, relative to the shorter box.
        /// </summary>
        /// <param name="a">The first box.</param>
        /// <param name="b">The second box.</param>
        /// <returns>
        /// The height of the shared vertical band divided by the height of the shorter box:
        /// 1 when the shorter box lies fully inside the other one's band, 0 when they do not overlap
        /// or one of them has no height.
        /// </returns>
        public static double VerticalOverlapRatio(Box a, Box b)
        {
            double shared = Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0);
            double height = Math.Min(a.Height, b.Height);
            return height > 0 && shared > 0 ? shared / height : 0.0;
        }

        /// <summary>
        /// Computes the median height of the word boxes.
        /// </summary>
        /// <remarks>
        /// The median is used instead of the mean so that a few very tall or very short boxes
        /// (a logo, a dot) do not change the line-building thresholds. With an even number of boxes the
        /// median is the mean of the two middle values.
        /// </remarks>
        /// <param name="tokens">The words to measure. Boxes with zero height are ignored.</param>
        /// <returns>The median height, or 0 when no box has a positive height.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokens"/> is <see langword="null"/>.</exception>
        public static double MedianHeight(IEnumerable<Token> tokens)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            List<double> heights = tokens.Select(t => t.Box.Height).Where(h => h > 0).Order().ToList();
            if (heights.Count == 0)
            {
                return 0.0;
            }
            int mid = heights.Count / 2;
            return heights.Count % 2 == 1 ? heights[mid] : (heights[mid - 1] + heights[mid]) / 2.0;
        }
    }
}
