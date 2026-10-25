using System.Text;
using System.Text.RegularExpressions;
using MSMask.Core.Models;

namespace MSMask.Core.Patterns
{
    /// <summary>
    /// Finds Turkish IBANs (TR + 2 check digits + 22 digits) and tolerates common OCR mistakes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The digits may be grouped with single spaces (<c>TR33 0006 1005 ...</c>) or written together.
    /// There is no word boundary on the left or right, because OCR engines sometimes drop the spaces
    /// around the number (<c>OdemeTR33...hesabina</c>). The mod-97 check is strong enough to reject
    /// random digit strings, so the loose boundary does not cause false alarms.
    /// </para>
    /// <para>
    /// OCR often misreads the "T" as "1" and the check digits as look-alike letters
    /// (<c>TRO2</c>, <c>TR?8</c>). These are corrected before validation; see <see cref="IsValidOcrIban"/>.
    /// </para>
    /// </remarks>
    public sealed class TurkishIBANRule : RegexRule
    {
        /// <summary>
        /// "T" or "1", "R", optional space, two check characters (digits or look-alikes), then 22 digits
        /// that may be separated by single spaces. The last digit must not be followed by another digit.
        /// </summary>
        private static readonly Regex IbanRegex =
            CreateRegex(@"[T1]R\s?[0-9OoQDIl|ZASsB?]{2}(?:\s?[0-9]){22}(?![0-9])");

        /// <summary>
        /// Initializes a new instance of the <see cref="TurkishIBANRule"/> class with name <c>"iban_tr"</c> and priority 0.
        /// </summary>
        public TurkishIBANRule()
            : base("iban_tr", Kinds.Iban, 0, IbanRegex, IsValidOcrIban)
        {
        }

        /// <summary>
        /// Validates a Turkish IBAN as read by OCR, after fixing common misreadings.
        /// </summary>
        /// <remarks>
        /// Whitespace is removed, the first two characters are replaced with "TR" and look-alike
        /// characters in the two check digits are mapped back to digits
        /// (O/o/Q/D → 0, I/l/| → 1, Z → 2, A → 4, S/s → 5, B → 8, ? → 7).
        /// The result is then checked with <see cref="Validators.IbanValid"/>.
        /// </remarks>
        /// <param name="text">The candidate text, for example <c>"TRO2 0011 1064 9289 1127 7645 36"</c>.</param>
        /// <returns><see langword="true"/> if the corrected IBAN passes the mod-97 check; otherwise <see langword="false"/>.</returns>
        public static bool IsValidOcrIban(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (!char.IsWhiteSpace(c))
                {
                    sb.Append(c);
                }
            }
            string s = sb.ToString();
            if (s.Length < 4)
            {
                return false;
            }

            string fixedIban = "TR" + FixCheckDigit(s[2]) + FixCheckDigit(s[3]) + s[4..];
            return Validators.IbanValid(fixedIban);
        }

        /// <summary>
        /// Maps a character that OCR often reads in place of a digit back to that digit.
        /// </summary>
        /// <param name="c">A check-digit character as read by OCR.</param>
        /// <returns>The corrected digit, or <paramref name="c"/> unchanged when it is not a known look-alike.</returns>
        private static char FixCheckDigit(char c) => c switch
        {
            'O' or 'o' or 'Q' or 'D' => '0',
            'I' or 'l' or '|' => '1',
            'Z' => '2',
            'A' => '4',
            'S' or 's' => '5',
            'B' => '8',
            '?' => '7',
            _ => c,
        };
    }
}
