using System.Diagnostics;
using MSMask.Core.Lines;
using MSMask.Core.Masking;
using MSMask.Core.Models;
using MSMask.Core.Ocr;
using MSMask.Core.Patterns;
using SkiaSharp;

namespace MSMask.Core
{
    /// <summary>
    /// Runs the whole detection process on an image: OCR, line building, pattern matching and masking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The evaluation tool, the tray application and the HTTP service all use this class, so the system that is
    /// measured is exactly the system that is shipped.
    /// </para>
    /// <para>
    /// The duration of every stage is recorded in <see cref="PipelineResult.TimingsMs"/> under the keys
    /// <c>"ocr"</c>, <c>"lines"</c>, <c>"patterns"</c>, <c>"mask"</c> (only when masking) and <c>"total"</c>.
    /// </para>
    /// </remarks>
    public sealed class Pipeline
    {
        /// <summary>
        /// The <see cref="Detection.Source"/> value of detections found by the pattern rules.
        /// </summary>
        public const string PatternSource = "pattern";

        /// <summary>
        /// Initializes a new instance of the <see cref="Pipeline"/> class.
        /// </summary>
        /// <param name="engine">The OCR engine that reads the words in the image.</param>
        /// <param name="checksum">
        /// <see langword="true"/> to validate check digits (TCKN, IBAN, card); <see langword="false"/> to skip
        /// validation, which is only useful for the checksum ablation experiment.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="engine"/> is <see langword="null"/>.</exception>
        public Pipeline(IOCREngine engine, bool checksum = true)
        {
            ArgumentNullException.ThrowIfNull(engine);
            Engine = engine;
            Checksum = checksum;
        }

        /// <summary>
        /// Gets the OCR engine.
        /// </summary>
        public IOCREngine Engine { get; }

        /// <summary>
        /// Gets a value indicating whether check digits are validated.
        /// </summary>
        public bool Checksum { get; }

        /// <summary>
        /// Gets the kinds of data this pipeline can detect.
        /// </summary>
        /// <value>
        /// Every kind except the ones that need the NER model (person names and locations).
        /// The evaluation ignores labels of other kinds, so a pipeline is not punished for kinds it cannot detect.
        /// </value>
        public IReadOnlySet<string> SupportedKinds { get; } = Kinds.All.Except(Kinds.NerKinds).ToHashSet();

        /// <summary>
        /// Finds the sensitive data in an image without changing it.
        /// </summary>
        /// <param name="image">The image to analyze. It is not modified.</param>
        /// <param name="cancellationToken">A token that cancels the operation between stages.</param>
        /// <returns>The detections, the stage timings and the OCR words (with line ids).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<PipelineResult> AnalyzeAsync(SKBitmap image, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(image);
            var result = new PipelineResult();
            long totalStart = Stopwatch.GetTimestamp();

            cancellationToken.ThrowIfCancellationRequested();
            long start = Stopwatch.GetTimestamp();
            IReadOnlyList<Token> tokens = await Engine.ReadAsync(image, cancellationToken).ConfigureAwait(false);
            result.TimingsMs["ocr"] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            cancellationToken.ThrowIfCancellationRequested();
            start = Stopwatch.GetTimestamp();
            List<TextLine> lines = LineBuilder.Build(tokens);
            result.TimingsMs["lines"] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            cancellationToken.ThrowIfCancellationRequested();
            start = Stopwatch.GetTimestamp();
            foreach (TextLine line in lines)
            {
                foreach (PatternMatch match in PatternMatcher.FindMatches(line.Text, Checksum))
                {
                    Box? box = line.SpanBox(match.Start, match.End);
                    if (box is not null)
                    {
                        result.Detections.Add(new Detection(match.Kind, box.Value, match.Text, PatternSource));
                    }
                }
            }
            result.TimingsMs["patterns"] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;

            result.Tokens.AddRange(lines.SelectMany(l => l.Tokens));
            result.TimingsMs["total"] = Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// Finds the sensitive data in an image and returns a masked copy.
        /// </summary>
        /// <param name="image">The image to mask. It is not modified.</param>
        /// <param name="cancellationToken">A token that cancels the operation between stages.</param>
        /// <returns>The masked copy and the analysis. The caller must dispose the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<MaskedImage> MaskAsync(SKBitmap image, CancellationToken cancellationToken = default)
        {
            PipelineResult result = await AnalyzeAsync(image, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            long start = Stopwatch.GetTimestamp();
            SKBitmap masked = ImageMasker.Mask(image, result.Detections);
            double maskMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            result.TimingsMs["mask"] = maskMs;
            result.TimingsMs["total"] += maskMs;
            return new MaskedImage(masked, result);
        }
    }
}