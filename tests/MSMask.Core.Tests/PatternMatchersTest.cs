using System.Text.RegularExpressions;
using MSMask.Core.Models;
using MSMask.Core.Patterns;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for the IBAN, card and TCKN rules and for <see cref="PatternMatcher"/>.
    /// Cases are ported from the Python reference (tests/test_patterns.py).
    /// </summary>
    public class PatternMatcherTests
    {
        /// <summary>
        /// Runs the default rules and returns (kind, text) pairs, which are easy to compare in assertions.
        /// </summary>
        /// <param name="text">The line of text to search.</param>
        /// <param name="checksum">Whether to validate check digits.</param>
        /// <returns>The resolved matches as (kind, text) pairs, from left to right.</returns>
        private static List<(string Kind, string Text)> KindsTexts(string text, bool checksum = true) =>
            PatternMatcher.FindMatches(text, checksum).Select(m => (m.Kind, m.Text)).ToList();

        /// <summary>
        /// Each line contains exactly one piece of sensitive data, and only that piece is reported.
        /// </summary>
        [Theory]
        [InlineData("TC: 10000000146", Kinds.Tckn, "10000000146")]
        [InlineData("IBAN: TR33 0006 1005 1978 6457 8413 26", Kinds.Iban, "TR33 0006 1005 1978 6457 8413 26")]
        [InlineData("IBAN TR330006100519786457841326", Kinds.Iban, "TR330006100519786457841326")]
        [InlineData("OdemeTR330006100519786457841326hesabina", Kinds.Iban, "TR330006100519786457841326")] // OCR dropped the spaces
        [InlineData("Kart: 4111 1111 1111 1111", Kinds.KrediKarti, "4111 1111 1111 1111")]
        [InlineData("Kart: 5555-5555-5555-4444 SKT 12/27", Kinds.KrediKarti, "5555-5555-5555-4444")]
        [InlineData("Amex 3782 822463 10005", Kinds.KrediKarti, "3782 822463 10005")] // 4-6-5 layout
        public void FindMatches_SingleItem_ReturnsOnlyThatItem(string text, string kind, string value)
        {
            Assert.Equal([(kind, value)], KindsTexts(text));
        }

        /// <summary>
        /// Numbers with wrong check digits and ordinary numbers are not reported.
        /// </summary>
        [Theory]
        [InlineData("Sipariş no: 5321234567")] // 10 digits
        [InlineData("Sipariş no: 10000000147")] // TCKN check digit is wrong
        [InlineData("Kart: 4111 1111 1111 1112")] // Luhn fails
        [InlineData("IBAN TR34 0006 1005 1978 6457 8413 26")] // mod-97 fails
        [InlineData("Fatura 2024 toplam 1500 TL")]
        [InlineData("Saat 14:30, 12.05.2024")]
        public void FindMatches_NoSensitiveData_ReturnsEmpty(string text)
        {
            Assert.Empty(KindsTexts(text));
        }

        /// <summary>
        /// The IBAN contains digit groups that also look like a card number. Both rules have priority 0,
        /// so the longer IBAN match wins.
        /// </summary>
        [Fact]
        public void FindMatches_IbanContainsCardLikeGroups_IbanWins()
        {
            Assert.Equal([(Kinds.Iban, "TR33 0006 1005 1978 6457 8413 26")], KindsTexts("TR33 0006 1005 1978 6457 8413 26"));
        }

        /// <summary>
        /// Several different items on one line are all reported, from left to right.
        /// </summary>
        [Fact]
        public void FindMatches_SeveralItems_ReturnsAllFromLeftToRight()
        {
            var expected = new List<(string, string)>
            {
                (Kinds.Tckn, "10000000146"),
                (Kinds.Iban, "TR33 0006 1005 1978 6457 8413 26"),
                (Kinds.KrediKarti, "4111 1111 1111 1111"),
            };
            Assert.Equal(expected, KindsTexts("TC 10000000146 IBAN TR33 0006 1005 1978 6457 8413 26 kart 4111 1111 1111 1111"));
        }

        /// <summary>
        /// With check digits validated, only the real card number is reported. Without validation, a
        /// longer 4-4-4-4-3 candidate that reaches into the following phone number wins instead.
        /// This shows why the checksum matters.
        /// </summary>
        [Fact]
        public void FindMatches_CardFollowedByDigits_ChecksumPicksRealCard()
        {
            const string text = "Kart 4111 1111 1111 1111 0532 123 45 67";
            Assert.Equal([(Kinds.KrediKarti, "4111 1111 1111 1111")], KindsTexts(text, checksum: true));
            Assert.Equal([(Kinds.KrediKarti, "1111 1111 1111 0532 123")], KindsTexts(text, checksum: false));
        }

        /// <summary>
        /// With validation switched off, numbers with wrong check digits are reported.
        /// </summary>
        [Fact]
        public void FindMatches_ChecksumOff_ReportsInvalidNumbers()
        {
            Assert.Empty(KindsTexts("Sipariş no: 10000000147", checksum: true));
            Assert.Equal([(Kinds.Tckn, "10000000147")], KindsTexts("Sipariş no: 10000000147", checksum: false));

            Assert.Empty(KindsTexts("Kart: 4111 1111 1111 1112", checksum: true));
            Assert.Equal([(Kinds.KrediKarti, "4111 1111 1111 1112")], KindsTexts("Kart: 4111 1111 1111 1112", checksum: false));

            const string badIban = "TR34 0006 1005 1978 6457 8413 26";
            Assert.Equal([(Kinds.Iban, badIban)], KindsTexts(badIban, checksum: false));

            // A 10-digit number is too short for every rule, with or without validation.
            Assert.Empty(KindsTexts("5321234567", checksum: false));
        }

        /// <summary>
        /// 10 + 3 + 3 digits add up to 16 and happen to pass Luhn, but this is not a card layout.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FindMatches_SixteenDigitsWithoutCardLayout_ReturnsEmpty(bool checksum)
        {
            Assert.Empty(KindsTexts("GET /api/orders/4070882026 200 863ms", checksum));
        }

        /// <summary>
        /// An 11-digit group with a dash on both sides is part of a token, not an identity number.
        /// </summary>
        [Fact]
        public void FindMatches_TcknInsideToken_ReturnsEmpty()
        {
            Assert.Empty(KindsTexts("SLACK_BOT_TOKEN=xoxb-557••••••••••••••••••••••••••••••••••"));
        }

        /// <summary>
        /// Mistakes seen in real OCR output of the development set are tolerated.
        /// </summary>
        [Theory]
        [InlineData("TRO2 0011 1064 9289 1127 7645 36", Kinds.Iban, "TRO2 0011 1064 9289 1127 7645 36")] // O instead of 0
        [InlineData("TR?8 0011 1061 4341 5272 3304 88", Kinds.Iban, "TR?8 0011 1061 4341 5272 3304 88")] // ? instead of 7
        [InlineData("iban=1R940006707151920085019285 amount", Kinds.Iban, "1R940006707151920085019285")] // 1 instead of T
        [InlineData("DEBUG customer tckn-31230779350 phone=+905528615088", Kinds.Tckn, "31230779350")] // - instead of =
        [InlineData("5507 103360374101", Kinds.KrediKarti, "5507 103360374101")] // OCR dropped two spaces (4-12)
        public void FindMatches_OcrMistakes_AreTolerated(string text, string kind, string value)
        {
            Assert.Contains((kind, value), KindsTexts(text));
        }

        /// <summary>
        /// Look-alike characters in the check digits are corrected before the mod-97 check.
        /// </summary>
        [Theory]
        [InlineData("TR33 0006 1005 1978 6457 8413 26", true)]
        [InlineData("TRO2 0011 1064 9289 1127 7645 36", true)]
        [InlineData("1R940006707151920085019285", true)]
        [InlineData("TR34 0006 1005 1978 6457 8413 26", false)]
        [InlineData("TR", false)]
        public void IsValidOcrIban_ReturnsExpected(string text, bool expected)
        {
            Assert.Equal(expected, TurkishIBANRule.IsValidOcrIban(text));
        }

        /// <summary>
        /// Only real credit card layouts are accepted.
        /// </summary>
        [Theory]
        [InlineData(new[] { 16 }, true)]
        [InlineData(new[] { 13 }, true)]
        [InlineData(new[] { 19 }, true)]
        [InlineData(new[] { 4, 4, 4, 4 }, true)]
        [InlineData(new[] { 4, 12 }, true)]
        [InlineData(new[] { 8, 8 }, true)]
        [InlineData(new[] { 4, 4, 4, 4, 3 }, true)]
        [InlineData(new[] { 4, 6, 5 }, true)]
        [InlineData(new[] { 4, 6, 4 }, true)]
        [InlineData(new[] { 12 }, false)]
        [InlineData(new[] { 20 }, false)]
        [InlineData(new[] { 10, 3, 3 }, false)]
        [InlineData(new[] { 4, 4, 4, 4, 4 }, false)]
        [InlineData(new int[0], false)]
        public void IsCardLayout_ReturnsExpected(int[] lengths, bool expected)
        {
            Assert.Equal(expected, CreditCardRule.IsCardLayout(lengths));
        }

        /// <summary>
        /// A lower priority value wins even when the other candidate is longer.
        /// </summary>
        [Fact]
        public void ResolveOverlaps_LowerPriorityWins()
        {
            var phone = new PatternMatch(Kinds.Telefon, 0, 20, "", 5, "phone");
            var tckn = new PatternMatch(Kinds.Tckn, 5, 16, "", 1, "tckn");
            Assert.Equal([tckn], PatternMatcher.ResolveOverlaps([phone, tckn]));
        }

        /// <summary>
        /// With equal priorities the longer candidate wins; with equal lengths the leftmost one wins.
        /// </summary>
        [Fact]
        public void ResolveOverlaps_EqualPriority_LongerThenLeftmostWins()
        {
            var shortOne = new PatternMatch(Kinds.KrediKarti, 0, 10, "", 0, "card");
            var longOne = new PatternMatch(Kinds.Iban, 2, 30, "", 0, "iban_tr");
            Assert.Equal([longOne], PatternMatcher.ResolveOverlaps([shortOne, longOne]));

            var left = new PatternMatch(Kinds.KrediKarti, 0, 10, "", 0, "card");
            var right = new PatternMatch(Kinds.KrediKarti, 5, 15, "", 0, "card");
            Assert.Equal([left], PatternMatcher.ResolveOverlaps([right, left]));
        }

        /// <summary>
        /// Candidates that only touch are both kept, and the result is sorted from left to right.
        /// </summary>
        [Fact]
        public void ResolveOverlaps_TouchingCandidates_BothKeptInOrder()
        {
            var first = new PatternMatch(Kinds.Tckn, 0, 11, "", 1, "tckn");
            var second = new PatternMatch(Kinds.Iban, 11, 37, "", 0, "iban_tr");
            Assert.Equal([first, second], PatternMatcher.ResolveOverlaps([second, first]));
        }

        /// <summary>
        /// When the validator rejects a match, the search restarts one character later,
        /// so a valid candidate that starts inside the rejected one is still found.
        /// </summary>
        [Fact]
        public void RegexRule_RejectedMatch_SearchRestartsOneCharacterLater()
        {
            var rule = new RegexRule("test", Kinds.Tckn, 1, new Regex("[0-9]{3}"), validator: s => s[0] == '5');
            Assert.Equal([new TextSpan(1, 4)], rule.FindSpans("1555", checksum: true));
            Assert.Equal([new TextSpan(0, 3)], rule.FindSpans("1555", checksum: false));
        }

        /// <summary>
        /// With a group name, only that group is reported.
        /// </summary>
        [Fact]
        public void RegexRule_WithGroup_ReportsOnlyTheGroup()
        {
            var rule = new RegexRule("kv", Kinds.ApiAnahtari, 3, new Regex(@"password=(?<v>[^\s]+)"), group: "v");
            Assert.Equal([new TextSpan(9, 15)], rule.FindSpans("password=abc123 ok", checksum: true));
        }

        /// <summary>
        /// A group name that does not exist in the expression is rejected when the rule is created.
        /// </summary>
        [Fact]
        public void RegexRule_UnknownGroup_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RegexRule("kv", Kinds.ApiAnahtari, 3, new Regex("abc"), group: "v"));
        }
    }
}