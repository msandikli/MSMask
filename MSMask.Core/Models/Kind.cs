using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Models
{
    /// <summary>
    /// Defines the kinds of sensitive data the pipeline can detect.
    /// </summary>
    /// <remarks>
    /// Always use these constants instead of string literals so that typos are caught by the compiler.
    /// The string values are part of the public contract (labels.json, HTTP headers, reports) and must not change.
    /// </remarks>
    public static class Kinds
    {
        /// <summary>Turkish national identity number (T.C. Kimlik No), 11 digits with two check digits.</summary>
        public const string Tckn = "TCKN";

        /// <summary>Turkish phone number (mobile or landline).</summary>
        public const string Telefon = "TELEFON";

        /// <summary>E-mail address.</summary>
        public const string Eposta = "EPOSTA";

        /// <summary>International Bank Account Number, validated with ISO 13616 mod-97.</summary>
        public const string Iban = "IBAN";

        /// <summary>Payment card number, validated with the Luhn algorithm.</summary>
        public const string KrediKarti = "KREDI_KARTI";

        /// <summary>API key, access token, password or connection-string secret.</summary>
        public const string ApiAnahtari = "API_ANAHTARI";

        /// <summary>Person name; detected only by the NER model.</summary>
        public const string KisiAdi = "KISI_ADI";

        /// <summary>Location (city, district); detected only by the NER model.</summary>
        public const string Konum = "KONUM";

        /// <summary>
        /// Gets every kind, in the fixed order used by reports and notifications.
        /// </summary>
        public static readonly IReadOnlyList<string> All =
            [Tckn, Telefon, Eposta, Iban, KrediKarti, ApiAnahtari, KisiAdi, Konum];

        /// <summary>
        /// Gets the text-based kinds (every kind except <see cref="Yuz"/>).
        /// </summary>
        /// <remarks>Masks of text kinds receive an extra margin around the detected box.</remarks>
        public static readonly IReadOnlySet<string> TextKinds = new HashSet<string>(All);

        /// <summary>
        /// Gets the kinds that only the NER model can detect.
        /// </summary>
        /// <remarks>Excluded from evaluation when NER is disabled.</remarks>
        public static readonly IReadOnlySet<string> NerKinds = new HashSet<string> { KisiAdi, Konum };
    }
}
