using MSMask.Core.Patterns;
using MSMask.Core.Rules;
using System.Text.RegularExpressions;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// A pattern rule driven by a regular expression, with optional validation, extension and trimming steps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each regular expression match the rule:
    /// </para>
    /// <list type="number">
    /// <item><description>Takes the whole match, or only the named <see cref="Group"/>.</description></item>
    /// <item><description>When <see cref="Extend"/> is set, adds random-looking chunks that OCR split off
    /// with single spaces (see <see cref="ChunkExtender.ExtendSpan"/>).</description></item>
    /// <item><description>Removes the characters in <see cref="TrimEnd"/> from the end.</description></item>
    /// <item><description>Checks <see cref="MinLength"/>, then <see cref="Filter"/>, then <see cref="Validator"/>.</description></item>
    /// </list>
    /// <para>
    /// When a candidate is rejected, the search restarts one character after the start of that match
    /// instead of after its end. This way a valid candidate that begins inside a rejected one is still found.
    /// </para>
    /// </remarks>
    public class RegexRule : PatternRule
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RegexRule"/> class.
        /// </summary>
        /// <param name="name">A short unique name for the rule.</param>
        /// <param name="kind">The kind of data the rule finds; one of the constants in <see cref="Models.Kinds"/>.</param>
        /// <param name="priority">The rule priority. Lower values win overlaps.</param>
        /// <param name="regex">The regular expression that finds candidates.</param>
        /// <param name="validator">
        /// An optional check-digit validator that receives the candidate text. It is skipped when
        /// <c>checksum</c> is <see langword="false"/>.
        /// </param>
        /// <param name="group">
        /// The name of a capture group to report instead of the whole match, or <see langword="null"/>
        /// to report the whole match. For example, in <c>password=abc123</c> only the value is masked.
        /// </param>
        /// <param name="extend">
        /// <see langword="true"/> to add random-looking chunks that follow the match after a single space.
        /// </param>
        /// <param name="minLength">
        /// The minimum number of non-space characters a candidate must have after extension and trimming.
        /// </param>
        /// <param name="filter">
        /// An optional filter that receives the candidate text. Unlike <paramref name="validator"/>,
        /// it is always applied, even when <c>checksum</c> is <see langword="false"/>.
        /// </param>
        /// <param name="trimEnd">
        /// Characters removed from the end of a candidate, for example a sentence-ending period.
        /// An empty string disables trimming.
        /// </param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> or <paramref name="kind"/> is null or empty,
        /// or when <paramref name="regex"/> has no group named <paramref name="group"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> or <paramref name="trimEnd"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minLength"/> is negative.</exception>
        public RegexRule(string name, string kind, int priority, Regex regex,
            Func<string, bool>? validator = null, string? group = null,
            bool extend = false, int minLength = 0, Func<string, bool>? filter = null, string trimEnd = "")
            : base(name, kind, priority)
        {
            ArgumentNullException.ThrowIfNull(regex);
            ArgumentNullException.ThrowIfNull(trimEnd);
            ArgumentOutOfRangeException.ThrowIfNegative(minLength);
            if (group is not null && regex.GroupNumberFromName(group) < 0)
            {
                throw new ArgumentException($"The regular expression has no group named '{group}'.", nameof(group));
            }
            Regex = regex;
            Validator = validator;
            Group = group;
            Extend = extend;
            MinLength = minLength;
            Filter = filter;
            TrimEnd = trimEnd;
        }

        /// <summary>
        /// Gets the regular expression that finds candidates.
        /// </summary>
        public Regex Regex { get; }

        /// <summary>
        /// Gets the optional check-digit validator.
        /// </summary>
        /// <value>A function that returns <see langword="true"/> for valid candidate text, or <see langword="null"/> when the rule has no validator.</value>
        public Func<string, bool>? Validator { get; }

        /// <summary>
        /// Gets the name of the capture group that is reported instead of the whole match.
        /// </summary>
        /// <value>The group name, or <see langword="null"/> when the whole match is reported.</value>
        public string? Group { get; }

        /// <summary>
        /// Gets a value indicating whether random-looking chunks after the match are added to it.
        /// </summary>
        /// <value>
        /// <see langword="true"/> for secrets that OCR may split with spaces, such as
        /// <c>sk-proj-oL LcZUUFgbJ1T8...</c>; otherwise <see langword="false"/>.
        /// </value>
        public bool Extend { get; }

        /// <summary>
        /// Gets the minimum number of non-space characters in a candidate.
        /// </summary>
        /// <value>0 when there is no minimum.</value>
        public int MinLength { get; }

        /// <summary>
        /// Gets the optional filter that is applied whether or not check digits are validated.
        /// </summary>
        /// <value>A function that returns <see langword="true"/> for acceptable candidate text, or <see langword="null"/> when the rule has no filter.</value>
        public Func<string, bool>? Filter { get; }

        /// <summary>
        /// Gets the characters removed from the end of a candidate.
        /// </summary>
        /// <value>For example <c>".,!?"</c>; an empty string when nothing is trimmed.</value>
        public string TrimEnd { get; }

        /// <inheritdoc/>
        public override IEnumerable<TextSpan> FindSpans(string text, bool checksum)
        {
            Func<string, bool>? validate = checksum ? Validator : null;
            int pos = 0;
            while (pos <= text.Length)
            {
                // Match(text, pos) still lets look-behinds see the characters before pos.
                Match m = Regex.Match(text, pos);
                if (!m.Success)
                {
                    yield break;
                }

                int start = m.Index;
                int end = m.Index + m.Length;
                if (Group is not null)
                {
                    // An optional group that did not take part in the match gives an empty range,
                    // which is rejected below.
                    Group g = m.Groups[Group];
                    start = g.Success ? g.Index : m.Index;
                    end = g.Success ? g.Index + g.Length : m.Index;
                }

                if (Extend)
                {
                    int extended = ChunkExtender.ExtendSpan(text, end);
                    if (extended > end)
                    {
                        // Only the punctuation at the end of the added chunks is removed
                        // ("elma 723." becomes "elma 723"); "!" and "?" are kept because passwords use them.
                        while (extended > end && ChunkExtender.TrailingPunctuation.Contains(text[extended - 1])
                            && text[extended - 1] != '!' && text[extended - 1] != '?')
                        {
                            extended--;
                        }
                        end = extended;
                    }
                }

                while (TrimEnd.Length > 0 && end > start && TrimEnd.Contains(text[end - 1]))
                {
                    end--;
                }

                bool ok = end > start && CountNonSpace(text, start, end) >= MinLength;
                ok = ok && (Filter is null || Filter(text[start..end]));
                ok = ok && (validate is null || validate(text[start..end]));
                if (ok)
                {
                    yield return new TextSpan(start, end);
                    pos = Math.Max(Math.Max(m.Index + m.Length, end), m.Index + 1);
                }
                else
                {
                    pos = m.Index + 1;
                }
            }
        }

        /// <summary>
        /// Counts the characters in a range that are not the space character.
        /// </summary>
        /// <param name="text">The text that contains the range.</param>
        /// <param name="start">The index of the first character in the range.</param>
        /// <param name="end">The index just after the last character in the range.</param>
        /// <returns>The number of characters in <c>text[start..end]</c> other than <c>' '</c>.</returns>
        private static int CountNonSpace(string text, int start, int end)
        {
            int count = 0;
            for (int i = start; i < end; i++)
            {
                if (text[i] != ' ')
                {
                    count++;
                }
            }
            return count;
        }
    }
}