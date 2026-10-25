using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core
{
    /// <summary>
    /// Provides check-digit validators for Turkish identity numbers, IBANs and payment card numbers.
    /// </summary>
    /// <remarks>
    /// The pattern rules use these validators to reject numbers that look right but are not valid,
    /// such as 11-digit order numbers or 16-digit tracking numbers. Validation can be switched off
    /// for the checksum ablation experiment.
    /// </remarks>
    public static class Validators
    {
        /// <summary>
        /// Expected IBAN lengths for common countries. Countries not listed only need a length between 15 and 34.
        /// </summary>
        private static readonly Dictionary<string, int> IbanLengths = new()
        {
            ["TR"] = 26,
            ["DE"] = 22,
            ["GB"] = 22,
            ["FR"] = 27,
            ["NL"] = 18,
            ["IT"] = 27,
            ["ES"] = 24,
            ["BE"] = 16,
            ["AT"] = 20,
            ["CH"] = 21,
            ["SE"] = 24,
            ["PL"] = 28,
            ["AZ"] = 28,
            ["GR"] = 27,
        };

        /// <summary>
        /// Determines whether a character is an ASCII digit ('0'–'9').
        /// </summary>
        /// <remarks>
        /// <see cref="char.IsDigit(char)"/> is not used because it also accepts other Unicode digits
        /// such as Arabic-Indic digits.
        /// </remarks>
        /// <param name="c">The character to test.</param>
        /// <returns><see langword="true"/> if <paramref name="c"/> is between '0' and '9'; otherwise <see langword="false"/>.</returns>
        public static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

        /// <summary>
        /// Removes every character that is not an ASCII digit.
        /// </summary>
        /// <param name="text">The text to filter, for example <c>"0532 123 45 67"</c>.</param>
        /// <returns>Only the digits of <paramref name="text"/>, for example <c>"05321234567"</c>.</returns>
        public static string DigitsOf(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (IsAsciiDigit(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Validates a Turkish national identity number (T.C. Kimlik No).
        /// </summary>
        /// <remarks>
        /// Rules:
        /// <list type="number">
        /// <item><description>Exactly 11 digits; the first digit is not 0.</description></item>
        /// <item><description>10th digit = ((d1 + d3 + d5 + d7 + d9) * 7 - (d2 + d4 + d6 + d8)) mod 10.</description></item>
        /// <item><description>11th digit = (d1 + d2 + ... + d10) mod 10.</description></item>
        /// </list>
        /// Non-digit characters are ignored, so <c>"100 000 001 46"</c> is accepted.
        /// </remarks>
        /// <param name="text">The text that contains the number.</param>
        /// <returns><see langword="true"/> if the digits form a valid identity number; otherwise <see langword="false"/>.</returns>
        public static bool TcknValid(string text)
        {
            string s = DigitsOf(text);
            if (s.Length != 11 || s[0] == '0')
            {
                return false;
            }

            int[] d = new int[11];
            for (int i = 0; i < 11; i++)
            {
                d[i] = s[i] - '0';
            }

            int odd = d[0] + d[2] + d[4] + d[6] + d[8];
            int even = d[1] + d[3] + d[5] + d[7];
            // The difference can be negative. C#'s % keeps the sign (-29 % 10 == -9), while the rule
            // needs the mathematical modulo (Python: -29 % 10 == 1), hence the extra "+ 10) % 10".
            int tenth = ((odd * 7 - even) % 10 + 10) % 10;
            if (tenth != d[9])
            {
                return false;
            }

            int sum = 0;
            for (int i = 0; i < 10; i++)
            {
                sum += d[i];
            }
            return sum % 10 == d[10];
        }

        /// <summary>
        /// Validates an International Bank Account Number using the ISO 13616 mod-97 check.
        /// </summary>
        /// <remarks>
        /// Steps:
        /// <list type="number">
        /// <item><description>Drop spaces and punctuation; convert to upper case.</description></item>
        /// <item><description>Check the shape: two letters (country), two digits (check digits), total length 15–34,
        /// and the exact country length when known (26 for TR).</description></item>
        /// <item><description>Move the first four characters to the end.</description></item>
        /// <item><description>Replace each letter with two digits (A = 10, B = 11, ..., Z = 35).</description></item>
        /// <item><description>The resulting number mod 97 must equal 1.</description></item>
        /// </list>
        /// The number can be 30+ digits long, so the remainder is computed digit by digit instead of
        /// building a big integer.
        /// </remarks>
        /// <param name="text">The IBAN, with or without spaces, in any letter case.</param>
        /// <returns><see langword="true"/> if the IBAN passes the length and mod-97 checks; otherwise <see langword="false"/>.</returns>
        public static bool IbanValid(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (IsAsciiDigit(c) || c is >= 'A' and <= 'Z' || c is >= 'a' and <= 'z')
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
            }
            string s = sb.ToString();

            if (s.Length < 15 || s.Length > 34)
            {
                return false;
            }
            if (!char.IsAsciiLetterUpper(s[0]) || !char.IsAsciiLetterUpper(s[1]) || !IsAsciiDigit(s[2]) || !IsAsciiDigit(s[3]))
            {
                return false;
            }
            if (IbanLengths.TryGetValue(s[..2], out int expected) && s.Length != expected)
            {
                return false;
            }

            string rearranged = s[4..] + s[..4];
            int remainder = 0;
            foreach (char c in rearranged)
            {
                if (IsAsciiDigit(c))
                {
                    remainder = (remainder * 10 + (c - '0')) % 97;
                }
                else
                {
                    // A letter becomes a two-digit number (A = 10 ... Z = 35), so shift by 100.
                    remainder = (remainder * 100 + (c - 'A' + 10)) % 97;
                }
            }
            return remainder == 1;
        }

        /// <summary>
        /// Validates a payment card number using the Luhn algorithm.
        /// </summary>
        /// <remarks>
        /// Starting from the rightmost digit, every second digit is doubled; if the result is greater
        /// than 9, 9 is subtracted. The sum of all digits must be divisible by 10.
        /// Only numbers with 13 to 19 digits are accepted. Non-digit characters are ignored,
        /// so <c>"4111 1111 1111 1111"</c> and <c>"4111-1111-1111-1111"</c> are both accepted.
        /// </remarks>
        /// <param name="text">The text that contains the card number.</param>
        /// <returns><see langword="true"/> if the number has 13–19 digits and passes the Luhn check; otherwise <see langword="false"/>.</returns>
        public static bool LuhnValid(string text)
        {
            string s = DigitsOf(text);
            if (s.Length < 13 || s.Length > 19)
            {
                return false;
            }

            int total = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int n = s[s.Length - 1 - i] - '0';
                if (i % 2 == 1)
                {
                    n *= 2;
                    if (n > 9)
                    {
                        n -= 9;
                    }
                }
                total += n;
            }
            return total % 10 == 0;
        }
    }
}
