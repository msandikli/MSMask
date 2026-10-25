using MSMask.Core.Models;
using MSMask.Core.Patterns;
using System.Text.RegularExpressions;

namespace MSMask.Core.Rules
{
    /// <summary>
    /// Finds payment card numbers that are written in a real card layout and pass the Luhn check.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A plain regular expression is not enough here. About one in ten random 16-digit strings passes
    /// the Luhn check, so a log line such as <c>/orders/4070882026 200 863</c> (10 + 3 + 3 digits)
    /// could be reported as a card. Candidates must therefore also follow a card layout;
    /// see <see cref="IsCardLayout"/>.
    /// </para>
    /// <para>
    /// The rule first finds runs of digits separated by single spaces or dashes, splits each run into
    /// digit groups and then tries every sequence of up to five consecutive groups. A candidate always
    /// starts and ends on a group boundary, so a card number is never cut in the middle of a group.
    /// </para>
    /// </remarks>
    public sealed class CreditCardRule : PatternRule
    {
        /// <summary>
        /// The maximum number of digit groups in one card number (4-4-4-4-3 has five).
        /// </summary>
        private const int MaxGroups = 5;

        /// <summary>
        /// A run of digits where each digit may be separated from the next by one space or dash.
        /// </summary>
        private static readonly Regex DigitRun = CreateRegex(@"(?<![0-9])[0-9](?:[ \-]?[0-9])*");

        /// <summary>
        /// One group of consecutive digits inside a run.
        /// </summary>
        private static readonly Regex DigitGroup = CreateRegex(@"[0-9]+");

        /// <summary>
        /// Initializes a new instance of the <see cref="CreditCardRule"/> class with name <c>"card"</c> and priority 0.
        /// </summary>
        public CreditCardRule()
            : base("card", Kinds.KrediKarti, 0)
        {
        }

        /// <inheritdoc/>
        public override IEnumerable<TextSpan> FindSpans(string text, bool checksum)
        {
            foreach (Match run in DigitRun.Matches(text))
            {
                List<TextSpan> groups = [];
                foreach (Match g in DigitGroup.Matches(run.Value))
                {
                    groups.Add(new TextSpan(run.Index + g.Index, run.Index + g.Index + g.Length));
                }

                for (int i = 0; i < groups.Count; i++)
                {
                    for (int j = i; j < Math.Min(groups.Count, i + MaxGroups); j++)
                    {
                        List<int> lengths = groups.GetRange(i, j - i + 1).Select(g => g.Length).ToList();
                        if (!IsCardLayout(lengths))
                        {
                            continue;
                        }

                        int start = groups[i].Start;
                        int end = groups[j].End;
                        if (!checksum || Validators.LuhnValid(text[start..end]))
                        {
                            yield return new TextSpan(start, end);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether a sequence of digit-group lengths looks like a written card number.
        /// </summary>
        /// <remarks>
        /// Accepted layouts:
        /// <list type="bullet">
        /// <item><description>One group of 13–19 digits (written together).</description></item>
        /// <item><description>Groups of multiples of four that add up to 16 (4-4-4-4, and 4-12 or 8-8 when OCR drops a separator).</description></item>
        /// <item><description>4-4-4-4 followed by a group of 1–3 digits (17–19-digit cards).</description></item>
        /// <item><description>4-6-5 (American Express) and 4-6-4 (Diners Club).</description></item>
        /// </list>
        /// </remarks>
        /// <param name="lengths">The number of digits in each group, from left to right.</param>
        /// <returns><see langword="true"/> if the layout is one of the accepted card layouts; otherwise <see langword="false"/>.</returns>
        public static bool IsCardLayout(IReadOnlyList<int> lengths)
        {
            if (lengths.Count == 0)
            {
                return false;
            }
            if (lengths.Count == 1)
            {
                return lengths[0] is >= 13 and <= 19;
            }
            if (lengths.All(n => n % 4 == 0) && lengths.Sum() == 16)
            {
                return true;
            }
            if (lengths.Count == 5 && lengths[0] == 4 && lengths[1] == 4 && lengths[2] == 4 && lengths[3] == 4)
            {
                return lengths[4] is >= 1 and <= 3;
            }
            return lengths.SequenceEqual(new[] { 4, 6, 5 }) || lengths.SequenceEqual(new[] { 4, 6, 4 });
        }
    }
}