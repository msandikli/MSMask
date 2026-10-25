using System.Text.RegularExpressions;
using MSMask.Core.Models;

namespace MSMask.Core.Patterns
{
    /// <summary>
    /// Finds Turkish national identity numbers: 11 digits, not starting with 0, with valid check digits.
    /// </summary>
    /// <remarks>
    /// The number must not touch another digit or an underscore on either side. A number with a dash
    /// on both sides is skipped because it is part of a token (<c>xoxb-557•••••••••••••...</c>).
    /// A dash on one side only is allowed, because OCR can read <c>tckn=...</c> as <c>tckn-...</c>.
    /// </remarks>
    public sealed class TCKNRule : PatternRule
    {
        /// <summary>
        /// A digit from 1 to 9 followed by ten digits, with no digit or underscore on either side.
        /// </summary>
        private static readonly Regex TcknRegex = CreateRegex(@"(?<![0-9_])[1-9][0-9]{10}(?![0-9_])");

        /// <summary>
        /// Initializes a new instance of the <see cref="TCKNRule"/> class with name <c>"tckn"</c> and priority 1.
        /// </summary>
        public TCKNRule()
            : base("tckn", Kinds.Tckn, 1)
        {
        }

        /// <inheritdoc/>
        public override IEnumerable<TextSpan> FindSpans(string text, bool checksum)
        {
            foreach (Match m in TcknRegex.Matches(text))
            {
                int start = m.Index;
                int end = m.Index + m.Length;
                bool insideToken = start > 0 && end < text.Length && text[start - 1] == '-' && text[end] == '-';
                if (insideToken)
                {
                    continue;
                }
                if (!checksum || Validators.TcknValid(m.Value))
                {
                    yield return new TextSpan(start, end);
                }
            }
        }
    }
}
