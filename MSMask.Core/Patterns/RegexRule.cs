using System;
using System.Collections.Generic;
using System.Text;

using System.Text.RegularExpressions;

namespace MSMask.Core.Patterns
{
    /// <summary>
    /// A pattern rule driven by a regular expression, with an optional check-digit validator.
    /// </summary>
    /// <remarks>
    /// When the validator rejects a match, the search restarts one character after the start of that
    /// match instead of after its end. This way a valid candidate that begins inside a rejected one
    /// is still found.
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
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="name"/> or <paramref name="kind"/> is null or empty,
        /// or when <paramref name="regex"/> has no group named <paramref name="group"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="regex"/> is <see langword="null"/>.</exception>
        public RegexRule(string name, string kind, int priority, Regex regex,
            Func<string, bool>? validator = null, string? group = null)
            : base(name, kind, priority)
        {
            ArgumentNullException.ThrowIfNull(regex);
            if (group is not null && regex.GroupNumberFromName(group) < 0)
            {
                throw new ArgumentException($"The regular expression has no group named '{group}'.", nameof(group));
            }
            Regex = regex;
            Validator = validator;
            Group = group;
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

                bool ok = end > start && (validate is null || validate(text[start..end]));
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
    }
}
