using MSMask.Core.Models;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// Builds the rules that find e-mail addresses, including common OCR misreadings.
    /// </summary>
    /// <remarks>
    /// OCR tolerances seen in the development set:
    /// <list type="bullet">
    /// <item><description>Spaces around "@" and "." (<c>ali @ firma.com</c>, <c>ali@firma. com. tr</c>).</description></item>
    /// <item><description>A user name split inside a digit group (<c>ayhan4 8@edu.tr</c>).</description></item>
    /// <item><description>"@" read as "©", "®", "&amp;" or "(©" (<c>thayrioglu&amp;gmail.com</c>).</description></item>
    /// </list>
    /// </remarks>
    public static class EmailRules
    {
        /// <summary>
        /// One character of the local part (the part before "@").
        /// </summary>
        private const string LocalChar = "[A-Za-z0-9._%+\\-]";

        /// <summary>
        /// The local part. The first alternative accepts a user name that OCR split inside a digit group:
        /// the first piece contains a letter and ends with a digit, the second piece starts with a digit and is
        /// short, and "@" (or a misread "@") follows. Otherwise the word before an address would be joined to it
        /// (<c>Tel 2 ali@x.com</c>).
        /// </summary>
        private const string LocalPart =
            "(?:" +
            "[A-Za-z0-9._%+\\-]*[A-Za-z][A-Za-z0-9._%+\\-]*[0-9] [0-9][A-Za-z0-9._%+\\-]{0,3}(?=\\s?(?:@|\\(?[©®&]))" +
            "|" + LocalChar + "+" +
            ")";

        /// <summary>
        /// One domain label, for example <c>firma</c> or <c>my-site</c>.
        /// </summary>
        private const string Label = "[A-Za-z0-9](?:[A-Za-z0-9\\-]*[A-Za-z0-9])?";

        /// <summary>
        /// Well-known domain parts. Spaces around "." are accepted only before these, so a sentence that
        /// continues after an address (<c>ayse@x.com. Yarın</c>) is not joined to it.
        /// </summary>
        private const string KnownParts =
            "com|net|org|edu|gov|mil|info|biz|name|io|co|tr|k12|bel|av|gen|web|tv|me|dev|ai|app|" +
            "uk|de|eu|us|ru|fr|nl|az|pol|tsk|tel|bbs";

        /// <summary>
        /// A "." with optional spaces around it, followed by a known domain part.
        /// </summary>
        private const string SpacedKnown = "\\s?\\.\\s?(?:" + KnownParts + ")(?![a-z0-9])";

        /// <summary>
        /// Any domain. It must end with a known part or a lower-case top-level domain of 2–24 letters.
        /// </summary>
        private const string AnyDomain =
            Label + "(?:\\." + Label + "|" + SpacedKnown + ")*?" +
            "(?:" + SpacedKnown + "|\\.[a-z]{2,24}(?![a-z0-9]))" +
            "(?:" + SpacedKnown + ")*";

        /// <summary>
        /// A domain that ends with a known part. Used when "@" was misread, where more evidence is needed.
        /// </summary>
        private const string KnownDomain =
            Label + "(?:\\." + Label + "|" + SpacedKnown + ")*?" + SpacedKnown + "(?:" + SpacedKnown + ")*";

        /// <summary>
        /// The address must not start in the middle of another address or word.
        /// </summary>
        private const string NoLocalCharBefore = "(?<![A-Za-z0-9._%+\\-])";

        /// <summary>
        /// Creates the e-mail rules in the order they are run.
        /// </summary>
        /// <returns>A new list with the normal rule and the rule for a misread "@", both with priority 4.</returns>
        public static List<PatternRule> Create() =>
        [
            new RegexRule("email", Kinds.Eposta, 4,
                PatternRule.CreateRegex(NoLocalCharBefore + LocalPart + "\\s?@\\s?" + AnyDomain)),
            new RegexRule("email_ocr", Kinds.Eposta, 4,
                PatternRule.CreateRegex(NoLocalCharBefore + LocalPart + "\\s?\\(?[©®&]\\s?" + KnownDomain)),
        ];
    }
}