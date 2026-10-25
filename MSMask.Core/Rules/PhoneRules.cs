using MSMask.Core.Models;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// Builds the rules that find Turkish phone numbers (mobile and landline).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A Turkish number has a 3-digit area code starting with 2, 3, 4, 5 or 8, followed by 7 digits written
    /// as 3-2-2 or 3-4. Groups may be separated by a space, "." or "-".
    /// </para>
    /// <para>
    /// A number without a prefix (+90, 90 or 0) must have a separator or parentheses around the area code;
    /// otherwise every 10-digit order number would be reported as a phone number.
    /// </para>
    /// <para>
    /// OCR tolerances: "+" read as "4" or "*", and the leading "0" read as "O" or "o".
    /// </para>
    /// </remarks>
    public static class PhoneRules
    {
        /// <summary>
        /// An optional separator between digit groups.
        /// </summary>
        private const string Sep = "[ .\\-]?";

        /// <summary>
        /// The 3-digit area code (mobile 5xx, landline 2xx–4xx, 8xx).
        /// </summary>
        private const string Area = "[2-58][0-9]{2}";

        /// <summary>
        /// The 7-digit subscriber number written as 3-2-2 or 3-4, not followed by another digit.
        /// </summary>
        private const string Rest = "[0-9]{3}" + Sep + "(?:[0-9]{2}" + Sep + "[0-9]{2}|[0-9]{4})(?![0-9])";

        /// <summary>
        /// The prefix: "+90" (or "*90"), "490" (misread "+90", only before exactly ten digits), "90", "0",
        /// or "O"/"o" (misread "0", only before an area code).
        /// </summary>
        private const string Prefix =
            "(?:[+*]\\s?90" +
            "|(?<![0-9+])490(?=[2-58][0-9]{9}(?![0-9]))" +
            "|(?<![0-9+])90" +
            "|(?<![0-9+])0" +
            "|(?<![A-Za-z0-9+])[Oo](?=" + Sep + "\\(?\\s?[2-58]))";

        /// <summary>
        /// Creates the phone rules in the order they are run.
        /// </summary>
        /// <returns>A new list of three rules, all with priority 5.</returns>
        public static List<PatternRule> Create() =>
        [
            // 0532 123 45 67, +90 532 123 45 67, 0 (532) 123-45-67
            new RegexRule("phone_prefixed", Kinds.Telefon, 5, PatternRule.CreateRegex(
                Prefix + Sep + "(?:\\(\\s?" + Area + "\\s?\\)|" + Area + ")" + Sep + Rest)),
            // (0212) 345 67 89
            new RegexRule("phone_paren0", Kinds.Telefon, 5, PatternRule.CreateRegex(
                "(?<![0-9+])\\(\\s?[0O]\\s?" + Area + "\\s?\\)" + Sep + Rest)),
            // (532) 123 45 67 and 532 123 45 67; a separator after the area code is required.
            new RegexRule("phone_bare", Kinds.Telefon, 5, PatternRule.CreateRegex(
                "(?<![0-9+])(?:\\(\\s?" + Area + "\\s?\\)\\s?|" + Area + "[ .\\-])" + Rest)),
        ];
    }
}
