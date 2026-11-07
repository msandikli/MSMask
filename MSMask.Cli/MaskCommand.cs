using System.Globalization;
using MSMask.Core;
using MSMask.Core.Ocr;
using SkiaSharp;

namespace MSMask.Cli
{
    /// <summary>
    /// The <c>mask</c> command: masks one image and saves the result.
    /// </summary>
    /// <remarks>
    /// Usage: <c>mask &lt;input&gt; &lt;output&gt; [--engine tesseract] [--no-checksum]</c>.
    /// The output format follows the output file extension (<c>.png</c>, <c>.jpg</c>/<c>.jpeg</c> or <c>.webp</c>).
    /// </remarks>
    public static class MaskCommand
    {
        /// <summary>
        /// Runs the command.
        /// </summary>
        /// <param name="args">The arguments after the command name.</param>
        /// <returns>The process exit code: 0 on success, 2 for wrong usage.</returns>
        /// <exception cref="InvalidDataException">Thrown when the input cannot be decoded or the output cannot be encoded.</exception>
        public static async Task<int> RunAsync(IReadOnlyList<string> args)
        {
            CommandLineArguments parsed = CommandLineArguments.Parse(args,
                flagNames: new HashSet<string> { "--no-checksum" },
                valueNames: new HashSet<string> { "--engine" });
            if (parsed.Positionals.Count != 2)
            {
                Console.Error.WriteLine("Usage: mask <input> <output> [--engine tesseract] [--no-checksum]");
                return 2;
            }
            string input = parsed.Positionals[0];
            string output = parsed.Positionals[1];
            SKEncodedImageFormat format = FormatFor(output);

            IOCREngine engine = EngineFactory.Create(parsed.Value("--engine", "tesseract")!);
            try
            {
                var pipeline = new Pipeline(engine, checksum: !parsed.Flags.Contains("--no-checksum"));
                using SKBitmap image = SKBitmap.Decode(input)
                    ?? throw new InvalidDataException($"The image '{input}' could not be decoded.");
                using MaskedImage masked = await pipeline.MaskAsync(image);

                using SKData data = masked.Image.Encode(format, 95)
                    ?? throw new InvalidDataException($"The image could not be encoded as {format}.");
                await File.WriteAllBytesAsync(output, data.ToArray());

                Dictionary<string, int> counts = masked.Result.KindCounts();
                string summary = counts.Count == 0
                    ? "none"
                    : string.Join(", ", counts.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}:{kv.Value}"));
                Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"{masked.Result.Detections.Count} detections ({summary}) - {masked.Result.TimingsMs["total"]:0} ms -> {output}"));
                return 0;
            }
            finally
            {
                (engine as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Chooses the image format from a file extension.
        /// </summary>
        /// <param name="path">The output path.</param>
        /// <returns>The encoder format.</returns>
        /// <exception cref="ArgumentException">Thrown when the extension is not supported.</exception>
        private static SKEncodedImageFormat FormatFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => throw new ArgumentException($"Unsupported output format '{Path.GetExtension(path)}'. Use .png, .jpg or .webp."),
        };
    }
}