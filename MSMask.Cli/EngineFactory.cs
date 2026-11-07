using MSMask.Core.Ocr;

namespace MSMask.Cli
{
    /// <summary>
    /// Creates OCR engines by name.
    /// </summary>
    public static class EngineFactory
    {
        /// <summary>
        /// The names accepted by <see cref="Create"/>.
        /// </summary>
        public static readonly IReadOnlyList<string> Names = ["tesseract"];

        /// <summary>
        /// Creates an OCR engine.
        /// </summary>
        /// <param name="name">The engine name; one of <see cref="Names"/>.</param>
        /// <returns>The engine. The caller must dispose it when it is disposable.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is not a known engine.</exception>
        public static IOCREngine Create(string name) => name.ToLowerInvariant() switch
        {
            "tesseract" => new TesseractOCREngine(),
            _ => throw new ArgumentException($"Unknown OCR engine '{name}'. Known engines: {string.Join(", ", Names)}."),
        };
    }
}