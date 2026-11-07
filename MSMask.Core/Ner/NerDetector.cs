using MSMask.Core.Lines;
using MSMask.Core.Models;
using MSMask.Core.Patterns;

namespace MSMask.Core.Ner
{
    /// <summary>
    /// Finds person names and places in OCR lines and turns them into detections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <c>PER</c> (→ <see cref="Kinds.KisiAdi"/>) and <c>LOC</c> (→ <see cref="Kinds.Konum"/>) entities are used.
    /// Organization names are not personal data and are ignored.
    /// </para>
    /// <para>
    /// Entities below <see cref="Threshold"/> are dropped, the rest is cleaned with <see cref="NerSpanRefiner"/>, and
    /// a range that overlaps something the pattern rules already found is dropped too: in <c>"ali.yilmaz@firma.com"</c>
    /// the model may see a surname, but the whole address is already masked as an e-mail.
    /// </para>
    /// </remarks>
    public sealed class NerDetector
    {
        /// <summary>
        /// The default minimum entity score.
        /// </summary>
        public const double DefaultThreshold = 0.5;

        /// <summary>
        /// The <see cref="Detection.Source"/> value of detections found by this class.
        /// </summary>
        public const string Source = "ner";

        /// <summary>
        /// Maps model entity types to detection kinds. Types that are not listed are ignored.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> KindByLabel = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["PER"] = Kinds.KisiAdi,
            ["PERSON"] = Kinds.KisiAdi,
            ["LOC"] = Kinds.Konum,
            ["LOCATION"] = Kinds.Konum,
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="NerDetector"/> class.
        /// </summary>
        /// <param name="recognizer">The model that finds the entities.</param>
        /// <param name="threshold">The minimum entity score, from 0 to 1.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="recognizer"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is outside 0–1.</exception>
        public NerDetector(IEntityRecognizer recognizer, double threshold = DefaultThreshold)
        {
            ArgumentNullException.ThrowIfNull(recognizer);
            ArgumentOutOfRangeException.ThrowIfNegative(threshold);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(threshold, 1.0);
            Recognizer = recognizer;
            Threshold = threshold;
        }

        /// <summary>
        /// Gets the model that finds the entities.
        /// </summary>
        public IEntityRecognizer Recognizer { get; }

        /// <summary>
        /// Gets the minimum entity score.
        /// </summary>
        public double Threshold { get; }

        /// <summary>
        /// Finds person names and places in OCR lines.
        /// </summary>
        /// <param name="lines">The OCR lines of one image.</param>
        /// <param name="taken">
        /// For each line id, the character ranges the pattern rules already found, or <see langword="null"/> when
        /// there are none.
        /// </param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The detections, with boxes in image coordinates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is <see langword="null"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        public async Task<List<Detection>> DetectAsync(IReadOnlyList<TextLine> lines,
            IReadOnlyDictionary<int, List<TextSpan>>? taken = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(lines);

            // Lines without letters (numbers, dates, symbols) cannot hold a name, so the model is not asked.
            List<TextLine> targets = lines.Where(l => l.Text.Any(char.IsLetter)).ToList();
            var detections = new List<Detection>();
            if (targets.Count == 0)
            {
                return detections;
            }

            IReadOnlyList<IReadOnlyList<NerEntity>> entities = await Recognizer
                .RecognizeAsync(targets.Select(l => l.Text).ToList(), cancellationToken).ConfigureAwait(false);

            for (int i = 0; i < targets.Count; i++)
            {
                TextLine line = targets[i];
                List<TextSpan> lineTaken = taken is not null && taken.TryGetValue(line.LineId, out List<TextSpan>? spans) ? spans : [];
                foreach (NerEntity entity in entities[i])
                {
                    if (!KindByLabel.TryGetValue(entity.Label, out string? kind) || entity.Score < Threshold)
                    {
                        continue;
                    }
                    foreach (TextSpan span in NerSpanRefiner.Refine(line.Text, entity.Start, entity.End))
                    {
                        if (lineTaken.Any(t => span.Start < t.End && t.Start < span.End))
                        {
                            continue;
                        }
                        Box? box = line.SpanBox(span.Start, span.End);
                        if (box is not null)
                        {
                            detections.Add(new Detection(kind, box.Value, line.Text[span.Start..span.End], Source, entity.Score));
                        }
                    }
                }
            }
            return detections;
        }
    }
}