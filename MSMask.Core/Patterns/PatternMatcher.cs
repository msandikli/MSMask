using MSMask.Core.Rules;

namespace MSMask.Core.Patterns
{
    /// <summary>
    /// Runs the pattern rules on a line of text and resolves overlapping matches.
    /// </summary>
    /// <remarks>
    /// Every rule reports its own candidates independently. The same digits can be claimed by several
    /// rules (an IBAN contains groups that look like a card number), so <see cref="ResolveOverlaps"/>
    /// keeps only one winner for each region of the text.
    /// </remarks>
    public static class PatternMatcher
    {
        /// <summary>
        /// Gets the default rules, in the order they are run.
        /// </summary>
        /// <value>
        /// IBAN, credit card and TCKN first, then API keys, e-mail and phone rules.
        /// The order does not decide overlaps (priority does), but it keeps error reports easy to read.
        /// </value>
        public static IReadOnlyList<PatternRule> DefaultRules { get; } =
        [
            new TurkishIBANRule(),
            new CreditCardRule(),
            new TCKNRule(),
            .. ApiKeyRules.Create(),
            .. EmailRules.Create(),
            .. PhoneRules.Create(),
        ];

        /// <summary>
        /// Runs every rule on a line of text and collects all candidates without resolving overlaps.
        /// </summary>
        /// <param name="text">The line of text to search.</param>
        /// <param name="checksum"><see langword="true"/> to validate check digits; <see langword="false"/> to skip validation.</param>
        /// <param name="rules">The rules to run, or <see langword="null"/> to use <see cref="DefaultRules"/>.</param>
        /// <returns>Every candidate from every rule. Candidates may overlap.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
        public static List<PatternMatch> FindCandidates(string text, bool checksum = true, IReadOnlyList<PatternRule>? rules = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            List<PatternMatch> candidates = [];
            foreach (PatternRule rule in rules ?? DefaultRules)
            {
                foreach (TextSpan span in rule.FindSpans(text, checksum))
                {
                    candidates.Add(new PatternMatch(rule.Kind, span.Start, span.End, text[span.Start..span.End], rule.Priority, rule.Name));
                }
            }
            return candidates;
        }

        /// <summary>
        /// Keeps one winner for every overlapping group of candidates.
        /// </summary>
        /// <remarks>
        /// Candidates are taken in this order: lowest <see cref="PatternMatch.Priority"/> first; among equal
        /// priorities the longest first; among equal lengths the leftmost first. A candidate is kept only if
        /// it does not overlap any candidate kept before it. Candidates that only touch (one ends where the
        /// other starts) do not overlap.
        /// </remarks>
        /// <param name="candidates">The candidates to resolve.</param>
        /// <returns>The kept candidates, sorted from left to right.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="candidates"/> is <see langword="null"/>.</exception>
        public static List<PatternMatch> ResolveOverlaps(IEnumerable<PatternMatch> candidates)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            List<PatternMatch> chosen = [];
            IEnumerable<PatternMatch> ordered = candidates
                .OrderBy(c => c.Priority)
                .ThenByDescending(c => c.Length)
                .ThenBy(c => c.Start);
            foreach (PatternMatch candidate in ordered)
            {
                if (chosen.All(o => candidate.End <= o.Start || candidate.Start >= o.End))
                {
                    chosen.Add(candidate);
                }
            }
            return chosen.OrderBy(c => c.Start).ToList();
        }

        /// <summary>
        /// Finds the sensitive data in a line of text.
        /// </summary>
        /// <param name="text">The line of text to search.</param>
        /// <param name="checksum"><see langword="true"/> to validate check digits; <see langword="false"/> to skip validation.</param>
        /// <param name="rules">The rules to run, or <see langword="null"/> to use <see cref="DefaultRules"/>.</param>
        /// <returns>The non-overlapping matches, sorted from left to right.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
        public static List<PatternMatch> FindMatches(string text, bool checksum = true, IReadOnlyList<PatternRule>? rules = null) =>
            ResolveOverlaps(FindCandidates(text, checksum, rules));
    }
}
