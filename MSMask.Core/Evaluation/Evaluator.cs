using MSMask.Core.Masking;
using MSMask.Core.Models;
using SkiaSharp;

namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Runs a pipeline on every image of a labeled data set and measures precision, recall, F1, protection and latency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Precision, recall and F1</b> are kind-sensitive: a detection counts only when its kind is the label's kind
    /// and its IoU with the label is at least <see cref="DetectionMatcher.IouThreshold"/> (see <see cref="DetectionMatcher"/>).
    /// </para>
    /// <para>
    /// <b>Protection</b> is kind-insensitive and answers the privacy question directly: is at least
    /// <see cref="CoverThreshold"/> of the label area really painted black, using exactly the same pixel rule as the
    /// masker (<see cref="MaskGeometry.PixelMap"/>, margins included)? An IoU of 0.5 can still leave half of a
    /// number readable, so this is the metric that matters most for privacy.
    /// </para>
    /// <para>
    /// By default only the kinds in <see cref="Pipeline.SupportedKinds"/> are evaluated, so a pipeline without the
    /// NER model is not punished for person names it cannot detect.
    /// </para>
    /// </remarks>
    public static class Evaluator
    {
        /// <summary>
        /// The minimum masked fraction of a label area for the label to count as protected.
        /// </summary>
        public const double CoverThreshold = 0.95;

        /// <summary>
        /// Evaluates a pipeline on a labeled data set.
        /// </summary>
        /// <param name="pipeline">The pipeline to evaluate.</param>
        /// <param name="labels">The labeled data set.</param>
        /// <param name="allKinds"><see langword="true"/> to also evaluate kinds the pipeline cannot detect.</param>
        /// <param name="limit">The number of images to evaluate from the start of the set, or <see langword="null"/> for all.</param>
        /// <param name="progress">Receives the number of finished images after each image, or <see langword="null"/>.</param>
        /// <param name="cancellationToken">A token that cancels the run between images.</param>
        /// <returns>The report and the error list.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pipeline"/> or <paramref name="labels"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidDataException">Thrown when an image file cannot be decoded.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public static async Task<EvaluationOutcome> EvaluateAsync(Pipeline pipeline, LabelSet labels,
            bool allKinds = false, int? limit = null, IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(labels);

            List<string> kinds = Kinds.All.Where(k => allKinds || pipeline.SupportedKinds.Contains(k)).ToList();
            var kindSet = kinds.ToHashSet();
            Dictionary<string, KindStats> stats = kinds.ToDictionary(k => k, _ => new KindStats());
            var timings = new List<IReadOnlyDictionary<string, double>>();
            var errors = new List<EvaluationError>();
            IEnumerable<LabeledImage> images = limit is int n ? labels.Images.Take(n) : labels.Images;

            int done = 0;
            foreach (LabeledImage image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = labels.ImagePath(image);
                using SKBitmap bitmap = SKBitmap.Decode(path)
                    ?? throw new InvalidDataException($"The image '{path}' could not be decoded.");
                PipelineResult result = await pipeline.AnalyzeAsync(bitmap, cancellationToken).ConfigureAwait(false);
                timings.Add(result.TimingsMs);

                List<Detection> predictions = result.Detections.Where(d => kindSet.Contains(d.Kind)).ToList();
                List<GroundTruthLabel> truths = image.Labels.Where(l => kindSet.Contains(l.Kind)).ToList();
                MatchResult match = DetectionMatcher.Match(predictions, truths);

                foreach ((_, GroundTruthLabel label) in match.TruePositives)
                {
                    stats[label.Kind].Tp++;
                }
                foreach (Detection detection in match.FalsePositives)
                {
                    stats[detection.Kind].Fp++;
                }
                foreach (GroundTruthLabel label in match.FalseNegatives)
                {
                    stats[label.Kind].Fn++;
                }

                // Protection is measured against every detection, whatever its kind.
                bool[,] painted = MaskGeometry.PixelMap(image.Width, image.Height, result.Detections);
                foreach (GroundTruthLabel label in truths)
                {
                    KindStats kindStats = stats[label.Kind];
                    kindStats.Total++;
                    double coverage = CoveredFraction(label.Box, painted);
                    if (coverage >= CoverThreshold)
                    {
                        kindStats.Protected++;
                    }
                    else
                    {
                        bool matched = match.TruePositives.Any(tp => ReferenceEquals(tp.Label, label));
                        errors.Add(new EvaluationError(image.File, EvaluationError.Unprotected, label.Kind, label.Text,
                            Coverage: Math.Round(coverage, 3), Matched: matched, Ocr: LineTextsNear(result.Tokens, label.Box)));
                    }
                }
                foreach (GroundTruthLabel label in match.FalseNegatives)
                {
                    errors.Add(new EvaluationError(image.File, EvaluationError.Missed, label.Kind, label.Text,
                        Ocr: LineTextsNear(result.Tokens, label.Box)));
                }
                foreach (Detection detection in match.FalsePositives)
                {
                    Box b = detection.Box;
                    errors.Add(new EvaluationError(image.File, EvaluationError.Extra, detection.Kind, detection.Text,
                        Box: [b.X0, b.Y0, b.X1, b.Y1]));
                }

                // Not "progress?.Report(++done)": with a null progress the increment would be skipped too.
                done++;
                progress?.Report(done);
            }

            var micro = new KindStats();
            foreach (KindStats kindStats in stats.Values)
            {
                micro.Add(kindStats);
            }
            var report = new EvaluationReport(
                Engine: pipeline.Name,
                Checksum: pipeline.Checksum,
                Dataset: labels.Path,
                ImageCount: done,
                Kinds: kinds,
                IouThreshold: DetectionMatcher.IouThreshold,
                CoverThreshold: CoverThreshold,
                PerKind: stats,
                Micro: micro,
                MacroF1: stats.Count > 0 ? stats.Values.Average(s => s.F1) : 0.0,
                Latency: LatencyStats.From(timings));
            return new EvaluationOutcome(report, errors);
        }

        /// <summary>
        /// Computes the fraction of a box that is painted.
        /// </summary>
        /// <remarks>
        /// The box edges are rounded to whole pixels and clipped to the map. A box with no pixels inside the map
        /// counts as fully covered, because there is nothing visible to protect.
        /// </remarks>
        /// <param name="box">The label box.</param>
        /// <param name="painted">The painted pixels from <see cref="MaskGeometry.PixelMap"/>, indexed as <c>[y, x]</c>.</param>
        /// <returns>A value from 0 (nothing painted) to 1 (everything painted).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="painted"/> is <see langword="null"/>.</exception>
        public static double CoveredFraction(Box box, bool[,] painted)
        {
            ArgumentNullException.ThrowIfNull(painted);
            int height = painted.GetLength(0);
            int width = painted.GetLength(1);
            int x0 = Math.Max(0, (int)Math.Round(box.X0));
            int y0 = Math.Max(0, (int)Math.Round(box.Y0));
            int x1 = Math.Min(width, (int)Math.Round(box.X1));
            int y1 = Math.Min(height, (int)Math.Round(box.Y1));
            if (x1 <= x0 || y1 <= y0)
            {
                return 1.0;
            }

            int count = 0;
            for (int y = y0; y < y1; y++)
            {
                for (int x = x0; x < x1; x++)
                {
                    if (painted[y, x])
                    {
                        count++;
                    }
                }
            }
            return (double)count / ((x1 - x0) * (y1 - y0));
        }

        /// <summary>
        /// Finds the OCR lines near a label, to show in error reports what OCR read there.
        /// </summary>
        /// <remarks>
        /// A line is near when it overlaps the label vertically by more than 30% of the label height and reaches
        /// within 50 pixels of the label horizontally.
        /// </remarks>
        /// <param name="tokens">The OCR words with line ids.</param>
        /// <param name="box">The label box.</param>
        /// <returns>The texts of the nearby lines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokens"/> is <see langword="null"/>.</exception>
        public static List<string> LineTextsNear(IEnumerable<Token> tokens, Box box)
        {
            ArgumentNullException.ThrowIfNull(tokens);
            var texts = new List<string>();
            foreach (IGrouping<int, Token> line in tokens.GroupBy(t => t.LineId))
            {
                Box lineBox = Box.UnionAll(line.Select(t => t.Box));
                double overlap = Math.Min(lineBox.Y1, box.Y1) - Math.Max(lineBox.Y0, box.Y0);
                if (overlap > 0.3 * box.Height && lineBox.X1 > box.X0 - 50 && lineBox.X0 < box.X1 + 50)
                {
                    texts.Add(string.Join(' ', line.Select(t => t.Text)));
                }
            }
            return texts;
        }
    }
}