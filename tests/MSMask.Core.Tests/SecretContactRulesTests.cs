using MSMask.Core.Models;
using MSMask.Core.Patterns;
using MSMask.Core.Rules;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for the API key, e-mail and phone rules.
    /// Cases are ported from the Python reference (tests/test_patterns.py).
    /// </summary>
    public class SecretContactRulesTests
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
        /// Phone numbers, e-mail addresses and keys in their usual written forms are found.
        /// </summary>
        [Theory]
        [InlineData("Tel: 0532 123 45 67", Kinds.Telefon, "0532 123 45 67")]
        [InlineData("Tel: 05321234567", Kinds.Telefon, "05321234567")]
        [InlineData("+90 532 123 45 67", Kinds.Telefon, "+90 532 123 45 67")]
        [InlineData("+905321234567", Kinds.Telefon, "+905321234567")]
        [InlineData("90 532 123 4567", Kinds.Telefon, "90 532 123 4567")]
        [InlineData("0 (532) 123-45-67", Kinds.Telefon, "0 (532) 123-45-67")]
        [InlineData("(0212) 345 67 89", Kinds.Telefon, "(0212) 345 67 89")]
        [InlineData("0212.345.67.89", Kinds.Telefon, "0212.345.67.89")]
        [InlineData("cep 532 123 45 67", Kinds.Telefon, "532 123 45 67")]
        [InlineData("(532) 123 45 67", Kinds.Telefon, "(532) 123 45 67")]
        [InlineData("mail: ali.veli@firma.com.tr", Kinds.Eposta, "ali.veli@firma.com.tr")]
        [InlineData("ali@firma. com. tr adresine", Kinds.Eposta, "ali@firma. com. tr")]
        [InlineData("ali @ firma.com yaz", Kinds.Eposta, "ali @ firma.com")]
        [InlineData("ayhan4 8@edu.tr", Kinds.Eposta, "ayhan4 8@edu.tr")]
        [InlineData("key AKIAIOSFODNN7EXAMPLE", Kinds.ApiAnahtari, "AKIAIOSFODNN7EXAMPLE")]
        [InlineData("key AKIAIOSF ODNN7EXAMPLE", Kinds.ApiAnahtari, "AKIAIOSF ODNN7EXAMPLE")] // a single space inserted by OCR
        [InlineData("ghp_a1B2a1B2a1B2a1B2a1B2a1B2a1B2a1B2a1B2", Kinds.ApiAnahtari, "ghp_a1B2a1B2a1B2a1B2a1B2a1B2a1B2a1B2a1B2")]
        [InlineData("xoxb-1234567890-abcdefghij", Kinds.ApiAnahtari, "xoxb-1234567890-abcdefghij")]
        [InlineData("sk-proj-abcdefghijklmnopqrstuvwx", Kinds.ApiAnahtari, "sk-proj-abcdefghijklmnopqrstuvwx")]
        [InlineData("AIzaBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB", Kinds.ApiAnahtari, "AIzaBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB")]
        [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U", Kinds.ApiAnahtari,
            "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0In0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U")]
        public void FindMatches_CommonForms_AreFound(string text, string kind, string value)
        {
            Assert.Contains((kind, value), KindsTexts(text));
        }

        /// <summary>
        /// In a key-value pair only the value is masked; the key name stays readable.
        /// </summary>
        [Theory]
        [InlineData("password=hunter22", "hunter22")]
        [InlineData("password = hunter22", "hunter22")]
        [InlineData("\"api_key\": \"abc123XYZ\"", "abc123XYZ")]
        [InlineData("DB_PASSWORD=s3cr3t!", "s3cr3t!")]
        [InlineData("GITHUB_TOKEN: tok_9876", "tok_9876")]
        [InlineData("Şifre: Gizli123", "Gizli123")]
        [InlineData("ŞİFRE: x9y8z7", "x9y8z7")]
        [InlineData("parola: Elma.2024", "Elma.2024")]
        [InlineData("Authorization: Bearer abc.def-ghi_123", "abc.def-ghi_123")]
        [InlineData("apı_key: 78193797091", "78193797091")] // Turkish OCR read "i" as "ı"
        public void FindMatches_KeyValue_MasksOnlyValue(string text, string value)
        {
            Assert.Equal([(Kinds.ApiAnahtari, value)], KindsTexts(text));
        }

        /// <summary>
        /// A word that only starts like a key name is not a key.
        /// </summary>
        [Fact]
        public void FindMatches_PassportIsNotPassword_ReturnsEmpty()
        {
            Assert.Empty(KindsTexts("passport: kimlik"));
        }

        /// <summary>
        /// The sentence after an e-mail address is not joined to it.
        /// </summary>
        [Theory]
        [InlineData("ayse@x.com. Yarın", "ayse@x.com")]
        [InlineData("ayse@x.com. yarin gel", "ayse@x.com")]
        [InlineData("iletisim ali@x.com", "ali@x.com")]
        [InlineData("kargoya verildi. zulfuye_k@firma.com.tr. Yarın", "zulfuye_k@firma.com.tr")]
        [InlineData("Tel 2 ali@x.com", "ali@x.com")]
        [InlineData("son ödeme 01.11.2023 ali@x.com", "ali@x.com")]
        public void FindMatches_Email_DoesNotTakeNeighbouringWords(string text, string value)
        {
            Assert.Equal([(Kinds.Eposta, value)], KindsTexts(text));
        }

        /// <summary>
        /// A card number followed by a phone number gives two separate matches.
        /// </summary>
        [Fact]
        public void FindMatches_CardFollowedByPhone_ReturnsBoth()
        {
            var expected = new List<(string, string)>
            {
                (Kinds.KrediKarti, "4111 1111 1111 1111"),
                (Kinds.Telefon, "0532 123 45 67"),
            };
            Assert.Equal(expected, KindsTexts("Kart 4111 1111 1111 1111 0532 123 45 67"));
        }

        /// <summary>
        /// Different kinds on one line are all reported, from left to right.
        /// </summary>
        [Fact]
        public void FindMatches_SeveralKinds_ReturnsAllFromLeftToRight()
        {
            List<string> kinds = KindsTexts("TC 10000000146 tel 0532 123 45 67 e-posta ali@x.com").Select(m => m.Kind).ToList();
            Assert.Equal([Kinds.Tckn, Kinds.Telefon, Kinds.Eposta], kinds);
        }

        /// <summary>
        /// A 10-digit number without a prefix or separator is an order number, not a phone number,
        /// with or without check-digit validation.
        /// </summary>
        [Fact]
        public void FindMatches_BareTenDigits_IsNotPhone()
        {
            Assert.Empty(KindsTexts("Sipariş no: 5321234567"));
            Assert.Empty(KindsTexts("5321234567", checksum: false));
        }

        /// <summary>
        /// Mistakes seen in real OCR output of the development set are tolerated.
        /// </summary>
        [Theory]
        [InlineData("4905326901354", Kinds.Telefon, "4905326901354")] // "+" read as "4"
        [InlineData("numarası O (531) 661 74 28", Kinds.Telefon, "O (531) 661 74 28")] // "0" read as "O"
        [InlineData("Bana akdora68 ©posta.net üzerinden", Kinds.Eposta, "akdora68 ©posta.net")] // "@" read as "©"
        [InlineData("login ok user=thayrioglu&gmail.com ip=10.1.2.3", Kinds.Eposta, "thayrioglu&gmail.com")]
        [InlineData("ilperakca(©yahoo.com", Kinds.Eposta, "ilperakca(©yahoo.com")]
        [InlineData("AlzagTFRihkLD8O5CwLWP8vLOSBNcYK3kLPsrxk", Kinds.ApiAnahtari, "AlzagTFRihkLD8O5CwLWP8vLOSBNcYK3kLPsrxk")] // "AIza" read as "Alza"
        [InlineData("ghp w25dgBgEcNwXp6HSBcTKLUİZcıwLfXaNSCcP", Kinds.ApiAnahtari, "ghp w25dgBgEcNwXp6HSBcTKLUİZcıwLfXaNSCcP")] // Turkish letters
        [InlineData("aws anahtarı: AKIA66C7XEGAİMOJ6641", Kinds.ApiAnahtari, "AKIA66C7XEGAİMOJ6641")]
        [InlineData("OPENAI_API_KEY=sk-proj-oL LcZUUFgbJ1T8aPdbsJ8Mak1jFyQUIO", Kinds.ApiAnahtari,
            "sk-proj-oL LcZUUFgbJ1T8aPdbsJ8Mak1jFyQUIO")] // a space inside the key
        [InlineData("SLACK_BOT_TOKEN=xoxb- 28409192199 -792528668364-QhQLT4Mqu0y1", Kinds.ApiAnahtari,
            "xoxb- 28409192199 -792528668364-QhQLT4Mqu0y1")]
        [InlineData("eyJhbGciOiJIUZI1 NilsinRöcCci6ikpXVC9.eyJzdWliOilzNjk3NSİsimihdci6MTY2ODk10DcC5MX0.WYIwSxeZ", Kinds.ApiAnahtari,
            "eyJhbGciOiJIUZI1 NilsinRöcCci6ikpXVC9.eyJzdWliOilzNjk3NSİsimihdci6MTY2ODk10DcC5MX0.WYIwSxeZ")]
        [InlineData("DB, PASSWORD-elma 723", Kinds.ApiAnahtari, "elma 723")] // "_" read as ",", "=" read as "-"
        [InlineData("GOOGLE MAPS KEY-AIza4O0sehUgEbECNoLGTc9fezHISvAgdyIRJ6sw", Kinds.ApiAnahtari,
            "AIza4O0sehUgEbECNoLGTc9fezHISvAgdyIRJ6sw")]
        [InlineData("export AWS SECRET, ACCESS KEY-WVVV98FwuKkVp11k8yalgFcZedmC7KOaF973M4KSs", Kinds.ApiAnahtari,
            "WVVV98FwuKkVp11k8yalgFcZedmC7KOaF973M4KSs")]
        [InlineData("Prod Password:deniz 111 bunu kimseyle paylaşma", Kinds.ApiAnahtari, "deniz 111")]
        [InlineData("Prod api key:9pZk8XV3 bunu kimseyle paylaşma", Kinds.ApiAnahtari, "9pZk8XV3")]
        public void FindMatches_OcrMistakes_AreTolerated(string text, string kind, string value)
        {
            Assert.Contains((kind, value), KindsTexts(text));
        }

        /// <summary>
        /// The OCR tolerances do not turn ordinary text into matches.
        /// </summary>
        [Theory]
        [InlineData("token-based kimlik doğrulama")] // dash form, but the value is an ordinary word
        [InlineData("pass-through modu")]
        [InlineData("Ar&Ge birimi")] // "&" without a domain
        public void FindMatches_OcrToleranceLookAlikes_ReturnsEmpty(string text)
        {
            Assert.Empty(KindsTexts(text));
        }

        /// <summary>
        /// A fixed-length key does not swallow the first letter of the next word.
        /// </summary>
        [Fact]
        public void FindMatches_FixedLengthKey_StopsBeforeNextWord()
        {
            Assert.Equal([(Kinds.ApiAnahtari, "AKIA1O0VUOVPSAU7I9MR")], KindsTexts("AKIA1O0VUOVPSAU7I9MR Müşteri"));
        }

        /// <summary>
        /// Extension stops at ordinary words. A period inside the value is kept, a sentence-ending period
        /// after an added chunk is removed.
        /// </summary>
        [Fact]
        public void FindMatches_Extension_StopsAtPlainWords()
        {
            Assert.Equal([(Kinds.ApiAnahtari, "Kale3634.")], KindsTexts("password: Kale3634. bunu kimseyle paylaşma"));
            Assert.Equal([(Kinds.ApiAnahtari, "elma 723")], KindsTexts("password: elma 723. Yarın"));
        }

        /// <summary>
        /// A random-looking token with no key name around it is still found, for example a key alone in a table cell.
        /// </summary>
        [Theory]
        [InlineData("mx0abcQdRwxuNXGQMz")]
        [InlineData("7q2XpLmR4tVbNw9k")]
        [InlineData("dGpX4mQVmDU3vAAb0xEys7IIkiGBPZ1wWposgk9T")]
        [InlineData("c2VjcmV0S2V5MTIzNDU2Nzg=")] // base64 padding is included
        public void FindMatches_RandomTokenWithoutContext_IsFound(string text)
        {
            Assert.Equal([(Kinds.ApiAnahtari, text)], KindsTexts(text));
        }

        /// <summary>
        /// Product names, camelCase names, hex ids, UUIDs and invoice numbers are not random tokens.
        /// </summary>
        [Theory]
        [InlineData("iPhone15ProMax")]
        [InlineData("GalaxyS24Ultra")]
        [InlineData("MacBookPro2023")]
        [InlineData("getUserAccountBalance2")]
        [InlineData("Windows11Home")]
        [InlineData("FTR2026381485")]
        [InlineData("3fcaf50cc3a65273")]
        [InlineData("550e8400-e29b-41d4-a716-446655440000")]
        [InlineData("demo_db_user")]
        [InlineData("HelloWorld2024Final")]
        [InlineData("request_id=3fcaf50cc3a65273")]
        public void FindMatches_NotRandomTokens_ReturnsEmpty(string text)
        {
            Assert.Empty(KindsTexts(text));
        }

        /// <summary>
        /// The random token rule does not join the key name to its value; the key-value rule wins.
        /// </summary>
        [Fact]
        public void FindMatches_RandomToken_DoesNotSwallowKeyName()
        {
            Assert.Equal([(Kinds.ApiAnahtari, "XBIklCUby6AfXW")], KindsTexts("DB_PASSWORD=XBIklCUby6AfXW"));
        }

        /// <summary>
        /// The whole password in a connection string is masked, not only the part after it.
        /// </summary>
        [Theory]
        [InlineData("mongodb+srv://<db_username>:7q2XpLmR4tVbNw9k@demodb.ab12cd.mongodb.net/", "7q2XpLmR4tVbNw9k")]
        [InlineData("postgres://admin:elma123@db.local:5432/app", "elma123")]
        [InlineData("redis://:Sifre.2024@cache:6379", "Sifre.2024")] // empty user name
        public void FindMatches_UriCredentials_MasksWholePassword(string text, string value)
        {
            Assert.Equal([(Kinds.ApiAnahtari, value)], KindsTexts(text));
        }

        /// <summary>
        /// A placeholder in a connection string template is not a secret.
        /// </summary>
        [Fact]
        public void FindMatches_UriPlaceholder_ReturnsEmpty()
        {
            Assert.Empty(KindsTexts("mongodb+srv://<db_username>:<db_password>@demodb.ab12cd.mongodb.net/"));
        }

        /// <summary>
        /// Chunks with digits or inner upper-case letters look random; ordinary words, quotes and URLs do not.
        /// </summary>
        [Theory]
        [InlineData("723", true)]
        [InlineData("LcZUUFgbJ1T8", true)]
        [InlineData("abcDef", true)]
        [InlineData("723.", true)]
        [InlineData("bunu", false)]
        [InlineData("Yarın", false)]
        [InlineData("\"abc123", false)]
        [InlineData("https://x.com/1", false)]
        [InlineData("...", false)]
        [InlineData("", false)]
        public void LooksRandom_ReturnsExpected(string chunk, bool expected)
        {
            Assert.Equal(expected, ChunkExtender.LooksRandom(chunk));
        }

        /// <summary>
        /// Extension adds random-looking chunks and stops at the first ordinary word.
        /// </summary>
        [Fact]
        public void ExtendSpan_AddsRandomChunksOnly()
        {
            const string text = "elma 723 A1b bunu 999";
            Assert.Equal(12, ChunkExtender.ExtendSpan(text, 4)); // "elma" + " 723" + " A1b"
            Assert.Equal(4, ChunkExtender.ExtendSpan(text, 4, maxChunks: 0));
        }
    }
}