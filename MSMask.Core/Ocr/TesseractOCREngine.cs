using global::MSMask.Core.Models;
using MSMask.Core.Ocr.MSMask.Core.Ocr;
using SkiaSharp;
using System.Globalization;
using TesseractOCR;
using TesseractOCR.Enums;
using TesseractImage = TesseractOCR.Pix.Image;

namespace MSMask.Core.Ocr
{
    /// <summary>
    /// Reads words with Tesseract, an open-source OCR engine whose recognizer is an LSTM neural network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before recognition the image is prepared by <see cref="OcrImagePreparer"/>: grayscale, inverted when the
    /// theme is dark, and enlarged by <see cref="Scale"/>. The word boxes are divided by the same factor, so they
    /// are returned in original image coordinates.
    /// </para>
    /// <para>
    /// Page segmentation mode 11 (sparse text) is used. Screenshots are not book pages: they contain scattered
    /// labels, table cells and buttons, and the default page layout analysis would miss many of them.
    /// </para>
    /// <para>
    /// The native Tesseract engine is not thread-safe, so calls are serialized. Create one instance and reuse it:
    /// loading the language models takes a noticeable time. Dispose it when it is no longer needed.
    /// </para>
    /// </remarks>
    public sealed class TesseractOCREngine : IOCREngine, IDisposable
    {
        /// <summary>
        /// The name of the environment variable that can point to the folder with the <c>.traineddata</c> files.
        /// </summary>
        public const string TessdataEnvironmentVariable = "MSMASK_TESSDATA_DIR";

        /// <summary>
        /// The default enlargement factor applied before recognition.
        /// </summary>
        public const double DefaultScale = 2.0;

        /// <summary>
        /// The Tesseract result level of a single word in TSV output.
        /// </summary>
        private const int WordLevel = 5;

        /// <summary>
        /// The native Tesseract engine.
        /// </summary>
        private readonly Engine _engine;

        /// <summary>
        /// Allows only one recognition at a time, because the native engine is not thread-safe.
        /// </summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>
        /// Whether <see cref="Dispose"/> has been called.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="TesseractOCREngine"/> class and loads the language models.
        /// </summary>
        /// <param name="tessdataDirectory">
        /// The folder that contains the <c>.traineddata</c> files, or <see langword="null"/> to search for it with
        /// <see cref="FindTessdataDirectory"/>.
        /// </param>
        /// <param name="languages">
        /// The Tesseract language codes joined with "+", for example <c>"tur+eng"</c>, or <see langword="null"/> to use
        /// <c>"tur+eng"</c> when the Turkish model is present and <c>"eng"</c> otherwise.
        /// </param>
        /// <param name="scale">The enlargement factor applied before recognition.</param>
        /// <param name="autoInvert"><see langword="true"/> to invert dark-theme images before recognition.</param>
        /// <exception cref="DirectoryNotFoundException">Thrown when no tessdata folder is given and none can be found.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the folder does not contain <c>eng.traineddata</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
        public TesseractOCREngine(string? tessdataDirectory = null, string? languages = null,
            double scale = DefaultScale, bool autoInvert = true)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
            string directory = tessdataDirectory ?? FindTessdataDirectory()
                ?? throw new DirectoryNotFoundException(
                    $"No tessdata folder was found. Put eng.traineddata and tur.traineddata into a 'tessdata' folder " +
                    $"next to the application or above it, or set the {TessdataEnvironmentVariable} environment variable.");
            string english = Path.Combine(directory, "eng.traineddata");
            if (!File.Exists(english))
            {
                throw new FileNotFoundException("The tessdata folder does not contain eng.traineddata.", english);
            }

            TessdataDirectory = directory;
            Languages = languages ?? (File.Exists(Path.Combine(directory, "tur.traineddata")) ? "tur+eng" : "eng");
            Scale = scale;
            AutoInvert = autoInvert;
            _engine = new Engine(directory, Languages, EngineMode.Default);
            // Tesseract writes notes such as "Estimating resolution as 237" to the console; send them nowhere.
            _engine.SetDebugVariable("debug_file", OperatingSystem.IsWindows() ? "NUL" : "/dev/null");
        }

        /// <inheritdoc/>
        public string Name => "tesseract";

        /// <summary>
        /// Gets the folder that contains the language models.
        /// </summary>
        public string TessdataDirectory { get; }

        /// <summary>
        /// Gets the loaded languages.
        /// </summary>
        /// <value>For example <c>"tur+eng"</c>.</value>
        public string Languages { get; }

        /// <summary>
        /// Gets the enlargement factor applied before recognition.
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// Gets a value indicating whether dark-theme images are inverted before recognition.
        /// </summary>
        public bool AutoInvert { get; }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
        public async Task<IReadOnlyList<Token>> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(image);
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Preparation and recognition are CPU-heavy, so they run on a thread-pool thread.
            byte[] png = await Task.Run(() => OcrImagePreparer.PrepareAsPng(image, Scale, AutoInvert), cancellationToken)
                .ConfigureAwait(false);

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await Task.Run(() => Recognize(png), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Releases the native Tesseract engine.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _engine.Dispose();
            _gate.Dispose();
        }

        /// <summary>
        /// Searches for a folder that contains <c>eng.traineddata</c>.
        /// </summary>
        /// <remarks>
        /// The search order is:
        /// <list type="number">
        /// <item><description>The folder named by the <see cref="TessdataEnvironmentVariable"/> environment variable.</description></item>
        /// <item><description>A <c>tessdata</c> folder in the application folder or any folder above it
        /// (so tests that run from <c>bin/Debug</c> find the one at the repository root).</description></item>
        /// <item><description>The <c>tessdata</c> folder of a standard Tesseract installation in Program Files.</description></item>
        /// </list>
        /// </remarks>
        /// <returns>The folder path, or <see langword="null"/> when none is found.</returns>
        public static string? FindTessdataDirectory()
        {
            string? fromEnvironment = Environment.GetEnvironmentVariable(TessdataEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(Path.Combine(fromEnvironment, "eng.traineddata")))
            {
                return fromEnvironment;
            }

            for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "tessdata");
                if (File.Exists(Path.Combine(candidate, "eng.traineddata")))
                {
                    return candidate;
                }
            }

            foreach (Environment.SpecialFolder folder in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string candidate = Path.Combine(Environment.GetFolderPath(folder), "Tesseract-OCR", "tessdata");
                if (File.Exists(Path.Combine(candidate, "eng.traineddata")))
                {
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// Converts Tesseract TSV output into word tokens.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each TSV row has twelve tab-separated columns: <c>level, page_num, block_num, par_num, line_num,
        /// word_num, left, top, width, height, conf, text</c>. Only word rows (level 5) with non-empty text and a
        /// non-negative confidence are kept. A header row, if present, is skipped.
        /// </para>
        /// <para>
        /// The block and line numbers are not copied into the tokens, because lines are rebuilt from geometry.
        /// </para>
        /// </remarks>
        /// <param name="tsv">The TSV text produced by Tesseract.</param>
        /// <param name="scale">The factor the image was enlarged by; every coordinate is divided by it.</param>
        /// <returns>The words, with boxes in original image coordinates and confidence between 0 and 1.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tsv"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
        public static List<Token> ParseTsv(string tsv, double scale)
        {
            ArgumentNullException.ThrowIfNull(tsv);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

            var tokens = new List<Token>();
            foreach (string row in tsv.Split('\n'))
            {
                string[] cols = row.TrimEnd('\r').Split('\t');
                if (cols.Length < 12 || !int.TryParse(cols[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
                {
                    continue;
                }
                string text = cols[11].Trim();
                if (level != WordLevel || text.Length == 0)
                {
                    continue;
                }
                if (!double.TryParse(cols[10], NumberStyles.Float, CultureInfo.InvariantCulture, out double conf) || conf < 0)
                {
                    continue;
                }

                int left = int.Parse(cols[6], CultureInfo.InvariantCulture);
                int top = int.Parse(cols[7], CultureInfo.InvariantCulture);
                int width = int.Parse(cols[8], CultureInfo.InvariantCulture);
                int height = int.Parse(cols[9], CultureInfo.InvariantCulture);
                var box = new Box(left / scale, top / scale, (left + width) / scale, (top + height) / scale);
                tokens.Add(new Token(text, box, Conf: conf / 100.0));
            }
            return tokens;
        }

        /// <summary>
        /// Runs Tesseract on a prepared image.
        /// </summary>
        /// <param name="png">The prepared image as PNG bytes.</param>
        /// <returns>The recognized words in original image coordinates.</returns>
        private IReadOnlyList<Token> Recognize(byte[] png)
        {
            using TesseractImage pix = TesseractImage.LoadFromMemory(png);
            using Page page = _engine.Process(pix, PageSegMode.SparseText);
            return ParseTsv(page.TsvText, Scale);
        }
    }
}
