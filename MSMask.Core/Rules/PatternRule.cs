using MSMask.Core.Patterns;
using System;
using System.Collections.Generic;
using System.Text;

using System.Text.RegularExpressions;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// Base class for every rule that finds one kind of sensitive data in a line of text.
    /// </summary>
    /// <remarks>
    /// A rule only reports candidate ranges. <see cref="PatternMatcher"/> runs every rule on the same
    /// line and then uses <see cref="Priority"/> to decide which candidate wins when two of them overlap.
    /// </remarks>
    public abstract class PatternRule
    {
        /// <summary>
        /// The maximum time a single regular expression match may take.
        /// </summary>
        /// <remarks>
        /// A badly written pattern can backtrack for minutes on unlucky input. The timeout turns such a
        /// bug into a <see cref="RegexMatchTimeoutException"/> instead of a frozen application.
        /// </remarks>
        public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternRule"/> class.
        /// </summary>
        /// <param name="name">A short unique name for the rule, for example <c>"iban_tr"</c>.</param>
        /// <param name="kind">The kind of data the rule finds; one of the constants in <see cref="Models.Kinds"/>.</param>
        /// <param name="priority">The rule priority. Lower values win overlaps.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or <paramref name="kind"/> is null or empty.</exception>
        protected PatternRule(string name, string kind, int priority)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentException.ThrowIfNullOrEmpty(kind);
            Name = name;
            Kind = kind;
            Priority = priority;
        }

        /// <summary>
        /// Gets the short unique name of the rule.
        /// </summary>
        /// <value>For example <c>"iban_tr"</c>, <c>"card"</c> or <c>"tckn"</c>.</value>
        public string Name { get; }

        /// <summary>
        /// Gets the kind of data the rule finds.
        /// </summary>
        /// <value>One of the constants in <see cref="Models.Kinds"/>.</value>
        public string Kind { get; }

        /// <summary>
        /// Gets the rule priority.
        /// </summary>
        /// <value>
        /// Lower values win when two candidates overlap:
        /// IBAN and card (0), TCKN (1), API keys (2–4), e-mail (4), phone (5).
        /// </value>
        public int Priority { get; }

        /// <summary>
        /// Finds every candidate range in a line of text.
        /// </summary>
        /// <param name="text">The line of text to search.</param>
        /// <param name="checksum">
        /// <see langword="true"/> to reject candidates whose check digits are wrong;
        /// <see langword="false"/> to skip check-digit validation (used by the checksum ablation experiment).
        /// </param>
        /// <returns>The candidate ranges, in the order they are found. Ranges may overlap.</returns>
        public abstract IEnumerable<TextSpan> FindSpans(string text, bool checksum);

        /// <summary>
        /// Creates a regular expression with the options every rule must use.
        /// </summary>
        /// <remarks>
        /// <see cref="RegexOptions.CultureInvariant"/> is required because the machine culture can be
        /// Turkish: with <see cref="RegexOptions.IgnoreCase"/>, "i" and "I" would not match each other
        /// under the tr-TR culture. <see cref="RegexOptions.Compiled"/> makes matching faster at the cost
        /// of a slower first use. Every expression also gets <see cref="MatchTimeout"/>.
        /// </remarks>
        /// <param name="pattern">The regular expression pattern. Use <c>[0-9]</c> instead of <c>\d</c>, which also matches non-ASCII digits.</param>
        /// <param name="options">Extra options, for example <see cref="RegexOptions.IgnoreCase"/>.</param>
        /// <returns>The compiled regular expression.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is not a valid regular expression.</exception>
        protected internal static Regex CreateRegex(string pattern, RegexOptions options = RegexOptions.None) =>
            new(pattern, options | RegexOptions.CultureInvariant | RegexOptions.Compiled, MatchTimeout);
    }
}
