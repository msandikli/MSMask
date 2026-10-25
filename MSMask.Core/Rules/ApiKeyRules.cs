using MSMask.Core.Models;
using System.Text.RegularExpressions;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// Builds the rules that find API keys, access tokens, passwords and connection-string secrets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rules come in four groups, from strongest to weakest evidence:
    /// </para>
    /// <list type="number">
    /// <item><description>Priority 2: keys with a well-known prefix (<c>AKIA</c>, <c>ghp_</c>, <c>AIza</c>, <c>sk-</c>, <c>xoxb-</c>, <c>eyJ</c> ...).</description></item>
    /// <item><description>Priority 3: values after a key name (<c>password=...</c>, <c>api_key: ...</c>, <c>Bearer ...</c>)
    /// and passwords in connection strings (<c>postgres://user:PASSWORD@host</c>). Only the value is masked.</description></item>
    /// <item><description>Priority 4: a random-looking token with no context, for example a key alone in a table cell.</description></item>
    /// </list>
    /// <para>
    /// OCR tolerances seen in the development set: single spaces inside keys, "_" read as " " or ",",
    /// "=" read as "-", "AIza" read as "Alza", and Turkish letters (İ, ı, ö ...) inserted by the Turkish OCR model.
    /// </para>
    /// </remarks>
    public static class ApiKeyRules
    {
        /// <summary>
        /// Turkish letters that the Turkish OCR model sometimes puts into keys (I → İ, i → ı, o → ö ...).
        /// </summary>
        private const string TurkishLetters = "İıÖöÜüŞşÇçĞğ";

        /// <summary>
        /// One key character for prefixed keys of variable length: anything except whitespace,
        /// quotes and sentence separators.
        /// </summary>
        private const string LooseKeyChar = "[^\\s\"'“”‘’<>(,;:]";

        /// <summary>
        /// The match must not start in the middle of a word.
        /// </summary>
        private const string NoWordBefore = "(?<![A-Za-z0-9])";

        /// <summary>
        /// The letter "i" in a key name, also accepting the Turkish "ı" and "İ" (<c>apı_key</c>).
        /// </summary>
        /// <remarks>
        /// With <see cref="RegexOptions.IgnoreCase"/> and <see cref="RegexOptions.CultureInvariant"/>, .NET
        /// matches "i" only with "I". The Turkish OCR model can read "i" as "ı" or "İ" inside key names,
        /// so these letters are listed explicitly.
        /// </remarks>
        private const string LetterI = "[iıİ]";

        /// <summary>
        /// The core key names whose value is a secret. Matched case-insensitively.
        /// </summary>
        private const string KeyNameCore =
            "password|passwd|pwd|pass|parola|[sşSŞ][iİıI]fre|token|secret|" +
            "ap" + LetterI + "[_\\-\\s]?key|ap" + LetterI + "key|access[_\\-\\s]?key|pr" + LetterI + "vate[_\\-\\s]?key";

        /// <summary>
        /// A key name: a core name, any name that ends with "_key" or "-key", or an upper-case name ending
        /// with " KEY" (OCR dropped the underscores: <c>GOOGLE MAPS KEY</c>). Prefixes such as <c>DB_</c> are
        /// not matched on purpose: only the value is masked, and optional nested prefixes cause
        /// catastrophic backtracking.
        /// </summary>
        private const string KeyName =
            "(?:(?:" + KeyNameCore + ")" +
            "|(?:[A-Za-z0-9ıİ]+[_\\-])+key" +
            "|(?-i:(?:[A-Z0-9]+[_\\-.,]?\\s)+KEY))";

        /// <summary>
        /// The value after a key name: an optional opening quote and at least three characters, captured as "v".
        /// </summary>
        private const string KeyValue = "[\"'“”]?(?<v>[^\\s\"'“”,;]{3,})";

        /// <summary>
        /// Characters of a context-free random token. "=" is allowed only as trailing base64 padding.
        /// </summary>
        private const string TokenChars = "A-Za-z0-9_\\-+/";

        /// <summary>
        /// <c>password=VALUE</c>, <c>api_key: VALUE</c>, <c>"token": "VALUE"</c> and similar.
        /// </summary>
        private static readonly Regex KeyValueRegex = PatternRule.CreateRegex(
            NoWordBefore + KeyName + "[\"'“”]?\\s?[:=]\\s?" + KeyValue, RegexOptions.IgnoreCase);

        /// <summary>
        /// Like <see cref="KeyValueRegex"/> but with a dash, because OCR can read "=" as "-".
        /// </summary>
        private static readonly Regex KeyDashValueRegex = PatternRule.CreateRegex(
            NoWordBefore + KeyName + "[\"'“”]?\\s?[\\-—–]{1,2}\\s?" + KeyValue, RegexOptions.IgnoreCase);

        /// <summary>
        /// An HTTP bearer token: <c>Authorization: Bearer VALUE</c>.
        /// </summary>
        private static readonly Regex BearerRegex = PatternRule.CreateRegex(
            NoWordBefore + "Bearer\\s+(?<v>[A-Za-z0-9._\\-]{8,})", RegexOptions.IgnoreCase);

        /// <summary>
        /// The password in a connection string: <c>scheme://user:PASSWORD@host</c>.
        /// Placeholders such as <c>&lt;db_password&gt;</c> do not match because "&lt;" is not allowed.
        /// </summary>
        private static readonly Regex UriCredentialsRegex = PatternRule.CreateRegex(
            "(?<![A-Za-z0-9])[A-Za-z][A-Za-z0-9+.\\-]*://[^\\s:/@]*:(?<v>[^\\s@/<>]+)@");

        /// <summary>
        /// A run of at least 12 token characters with up to two "=" at the end, not touching other token characters.
        /// </summary>
        private static readonly Regex RandomTokenRegex = PatternRule.CreateRegex(
            "(?<![" + TokenChars + "=])[" + TokenChars + "]{12,}={0,2}(?![" + TokenChars + "=])");

        /// <summary>
        /// A run of lower-case ASCII letters.
        /// </summary>
        private static readonly Regex LowerRun = PatternRule.CreateRegex("[a-z]+");

        /// <summary>
        /// An upper-case letter that is not followed by a lower-case letter, so it does not start a word ("NXGQM", "R4").
        /// </summary>
        private static readonly Regex UpperNotWordStart = PatternRule.CreateRegex("[A-Z](?![a-z])");

        /// <summary>
        /// Creates the API key rules in the order they are run.
        /// </summary>
        /// <returns>A new list of rules: prefixed keys, key-value pairs, bearer tokens, connection strings and random tokens.</returns>
        public static List<PatternRule> Create()
        {
            const string trim = ".,!?";
            return
            [
                // Fixed-length keys with a well-known prefix.
                new RegexRule("aws", Kinds.ApiAnahtari, 2, FixedLengthKey("(?:AK[IT1l]A|ASIA)", "0-9A-Za-z@", 15, 17)),
                new RegexRule("github", Kinds.ApiAnahtari, 2, FixedLengthKey("gh[pousr][_ .]", "A-Za-z0-9", 30, 37)),
                new RegexRule("google", Kinds.ApiAnahtari, 2, FixedLengthKey("A[Il1T|]za", "0-9A-Za-z_\\-", 33, 36)),

                // Variable-length keys: prefix + body, extended with space-separated random chunks.
                // The prefix is strong evidence, so OCR leftovers in the body ("!" for I, ")" for J) are accepted.
                new RegexRule("github_pat", Kinds.ApiAnahtari, 2,
                    PatternRule.CreateRegex(NoWordBefore + "github_pat_" + LooseKeyChar + "{4,}"),
                    extend: true, minLength: 20, trimEnd: trim),
                new RegexRule("slack", Kinds.ApiAnahtari, 2,
                    PatternRule.CreateRegex(NoWordBefore + "xox[abprs]\\s?-" + LooseKeyChar + "*"),
                    extend: true, minLength: 15, trimEnd: trim),
                new RegexRule("jwt", Kinds.ApiAnahtari, 2,
                    PatternRule.CreateRegex(NoWordBefore + "eyJ" + LooseKeyChar + "{6,}(?:\\s?\\.\\s?" + LooseKeyChar + "{2,}){0,2}"),
                    extend: true, minLength: 20, trimEnd: trim),
                new RegexRule("openai", Kinds.ApiAnahtari, 2,
                    PatternRule.CreateRegex(NoWordBefore + "sk-(?:proj-|ant-)?" + LooseKeyChar + "{4,}"),
                    extend: true, minLength: 20, trimEnd: trim),
                new RegexRule("stripe", Kinds.ApiAnahtari, 2,
                    PatternRule.CreateRegex(NoWordBefore + "[sr]k_(?:live|test)_[A-Za-z0-9]{4,}"),
                    extend: true, minLength: 20, trimEnd: trim),

                // Values after a key name; only the value is masked.
                new RegexRule("kv_secret", Kinds.ApiAnahtari, 3, KeyValueRegex, group: "v", extend: true),
                // "token-based" and "pass-through" are ordinary words, so the dash form needs a random-looking value.
                new RegexRule("kv_secret_dash", Kinds.ApiAnahtari, 3, KeyDashValueRegex, group: "v", extend: true,
                    filter: ChunkExtender.LooksRandom),
                new RegexRule("bearer", Kinds.ApiAnahtari, 3, BearerRegex, group: "v", extend: true),
                new RegexRule("uri_credentials", Kinds.ApiAnahtari, 3, UriCredentialsRegex, group: "v"),

                // A token with no context gets priority 4 so that the rules with context above win overlaps.
                new RegexRule("random_token", Kinds.ApiAnahtari, 4, RandomTokenRegex, filter: IsRandomToken),
            ];
        }

        /// <summary>
        /// Determines whether a context-free token looks like a random key rather than a name, product code or id.
        /// </summary>
        /// <remarks>
        /// <para>A token is accepted when all of these hold:</para>
        /// <list type="bullet">
        /// <item><description>It has at least two upper-case letters, at least one lower-case letter and at least one digit.</description></item>
        /// <item><description>At least one upper-case letter does not start a word (it is not followed by a lower-case letter).</description></item>
        /// <item><description>Runs of lower-case letters are short: their average length is below 2.5.</description></item>
        /// </list>
        /// <para>
        /// In a random string over 62 characters the expected length of a lower-case run is about 1.7, while
        /// camelCase names (<c>getUserBalance2</c>) have longer runs. In product names (<c>iPhone15ProMax</c>)
        /// every upper-case letter starts a word. Lower-case hex ids, UUIDs and upper-case invoice numbers
        /// have no mixed case and are rejected too.
        /// </para>
        /// </remarks>
        /// <param name="token">The candidate token.</param>
        /// <returns><see langword="true"/> if the token looks random; otherwise <see langword="false"/>.</returns>
        public static bool IsRandomToken(string token)
        {
            if (token.Count(char.IsUpper) < 2 || !token.Any(Validators.IsAsciiDigit))
            {
                return false;
            }
            if (!UpperNotWordStart.IsMatch(token))
            {
                return false;
            }

            MatchCollection runs = LowerRun.Matches(token);
            if (runs.Count == 0)
            {
                return false;
            }
            double averageRun = runs.Sum(r => r.Length) / (double)runs.Count;
            return averageRun < 2.5;
        }

        /// <summary>
        /// Builds the expression for a fixed-length key with a known prefix.
        /// </summary>
        /// <remarks>
        /// Single spaces inserted by OCR are allowed between body characters: <c>(?:\s?X){n}</c>. The last
        /// character must be attached to the key and must not be followed by another body character, so the
        /// first letter of the next word is not swallowed. Turkish letters are added to the body characters.
        /// </remarks>
        /// <param name="prefix">The expression for the key prefix, for example <c>(?:AK[IT1l]A|ASIA)</c>.</param>
        /// <param name="bodyChars">The body characters as the inside of a character class, for example <c>0-9A-Za-z</c>.</param>
        /// <param name="minBody">The minimum number of body characters after the prefix.</param>
        /// <param name="maxBody">The maximum number of body characters after the prefix.</param>
        /// <returns>The compiled regular expression.</returns>
        private static Regex FixedLengthKey(string prefix, string bodyChars, int minBody, int maxBody)
        {
            string cls = "[" + bodyChars + TurkishLetters + "]";
            string pattern = NoWordBefore + prefix + "(?:\\s?" + cls + "){" + (minBody - 1) + "," + (maxBody - 1) + "}" + cls + "(?!" + cls + ")";
            return PatternRule.CreateRegex(pattern);
        }
    }
}