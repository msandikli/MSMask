using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Validators"/>. Cases are ported from the Python reference (tests/test_validators.py).
    /// </summary>
    public class ValidatorsTests
    {
        /// <summary>
        /// Valid identity numbers are accepted, including one whose 10th-digit formula is negative.
        /// </summary>
        [Theory]
        [InlineData("10000000146")]
        [InlineData("12345678950")]
        [InlineData("11111111110")]
        [InlineData("19090909018")] // (1+0+0+0+0)*7 - (9+9+9+9) = -29 → mod 10 must give 1, not -9
        [InlineData("100 000 001 46")] // spaces are ignored
        public void TcknValid_ValidNumbers_ReturnsTrue(string text)
        {
            Assert.True(Validators.TcknValid(text));
        }

        /// <summary>
        /// Wrong check digits, a leading zero, wrong lengths and non-ASCII digits are rejected.
        /// </summary>
        [Theory]
        [InlineData("10000000147")] // wrong 11th digit
        [InlineData("10000000156")] // wrong 10th digit
        [InlineData("01234567890")] // starts with 0
        [InlineData("1000000014")] // 10 digits
        [InlineData("100000001461")] // 12 digits
        [InlineData("")]
        [InlineData("١٠٠٠٠٠٠٠١٤٦")] // Arabic-Indic digits are not ASCII digits
        public void TcknValid_InvalidNumbers_ReturnsFalse(string text)
        {
            Assert.False(Validators.TcknValid(text));
        }

        /// <summary>
        /// Valid IBANs are accepted with or without spaces and in lower case.
        /// </summary>
        [Theory]
        [InlineData("TR330006100519786457841326")]
        [InlineData("TR33 0006 1005 1978 6457 8413 26")]
        [InlineData("tr33 0006 1005 1978 6457 8413 26")]
        [InlineData("DE89370400440532013000")]
        [InlineData("GB82WEST12345698765432")]
        public void IbanValid_ValidIbans_ReturnsTrue(string text)
        {
            Assert.True(Validators.IbanValid(text));
        }

        /// <summary>
        /// Wrong check digits and wrong lengths are rejected.
        /// </summary>
        [Theory]
        [InlineData("TR340006100519786457841326")] // wrong check digits
        [InlineData("TR33000610051978645784132")] // 25 characters; TR needs 26
        [InlineData("TR3300061005197864578413261")] // 27 characters
        [InlineData("DE89370400440532013001")]
        [InlineData("XX")]
        public void IbanValid_InvalidIbans_ReturnsFalse(string text)
        {
            Assert.False(Validators.IbanValid(text));
        }

        /// <summary>
        /// Card numbers that pass the Luhn check are accepted with spaces or dashes.
        /// </summary>
        [Theory]
        [InlineData("4111111111111111")]
        [InlineData("4111 1111 1111 1111")]
        [InlineData("5555-5555-5555-4444")]
        [InlineData("378282246310005")] // 15-digit American Express
        public void LuhnValid_ValidCards_ReturnsTrue(string text)
        {
            Assert.True(Validators.LuhnValid(text));
        }

        /// <summary>
        /// Numbers that fail the Luhn check or have the wrong length are rejected.
        /// </summary>
        [Theory]
        [InlineData("4111111111111112")] // Luhn fails
        [InlineData("1234567812345678")] // Luhn fails
        [InlineData("411111111111")] // 12 digits
        [InlineData("41111111111111111111")] // 20 digits
        public void LuhnValid_InvalidCards_ReturnsFalse(string text)
        {
            Assert.False(Validators.LuhnValid(text));
        }

        /// <summary>
        /// DigitsOf keeps ASCII digits only.
        /// </summary>
        [Fact]
        public void DigitsOf_RemovesEverythingButAsciiDigits()
        {
            Assert.Equal("05321234567", Validators.DigitsOf("+(0532) 123-45 67"));
            Assert.Equal("", Validators.DigitsOf("١٢٣"));
        }
    }
}
