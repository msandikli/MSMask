using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Writes evaluation results as JSON, Markdown and a JSON Lines error list.
    /// </summary>
    /// <remarks>
    /// Numbers are always written with <see cref="CultureInfo.InvariantCulture"/>. Under a Turkish culture
    /// <c>0.981.ToString()</c> gives <c>"0,981"</c>, which would break tables and anything that parses them.
    /// </remarks>
    public static class ReportWriter
    {
        /// <summary>
        /// The JSON settings: snake_case names, indentation, readable non-ASCII text, and no null fields.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// The JSON settings for one error per line.
        /// </summary>
        private static readonly JsonSerializerOptions JsonLineOptions = new(JsonOptions) { WriteIndented = false };

        /// <summary>
        /// Writes the report and the errors next to each other.
        /// </summary>
        /// <remarks>
        /// For the prefix <c>results/dev/tesseract</c> the files are <c>results/dev/tesseract.json</c>,
        /// <c>results/dev/tesseract.md</c> and <c>results/dev/tesseract_errors.jsonl</c>. Missing folders are created.
        /// </remarks>
        /// <param name="outcome">The evaluation outcome.</param>
        /// <param name="prefix">The path of the output files without extension.</param>
        /// <param name="cancellationToken">A token that cancels the writing.</param>
        /// <returns>A task that completes when all three files are written.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="outcome"/> or <paramref name="prefix"/> is <see langword="null"/>.</exception>
        public static async Task WriteAsync(EvaluationOutcome outcome, string prefix, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            ArgumentNullException.ThrowIfNull(prefix);

            string? folder = Path.GetDirectoryName(Path.GetFullPath(prefix));
            if (folder is not null)
            {
                Directory.CreateDirectory(folder);
            }

            string json = JsonSerializer.Serialize(outcome.Report, JsonOptions);
            await File.WriteAllTextAsync(prefix + ".json", json, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(prefix + ".md", ToMarkdown(outcome.Report), cancellationToken).ConfigureAwait(false);

            var lines = new StringBuilder();
            foreach (EvaluationError error in outcome.Errors)
            {
                lines.Append(JsonSerializer.Serialize(error, JsonLineOptions)).Append('\n');
            }
            await File.WriteAllTextAsync(prefix + "_errors.jsonl", lines.ToString(), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Formats a report as a Markdown table with one row per kind and a total row.
        /// </summary>
        /// <param name="report">The report to format.</param>
        /// <returns>The Markdown text.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="report"/> is <see langword="null"/>.</exception>
        public static string ToMarkdown(EvaluationReport report)
        {
            ArgumentNullException.ThrowIfNull(report);
            CultureInfo c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(c, $"### {report.ConfigName}\n\n");
            sb.Append(c, $"Dataset: `{report.Dataset}` ({report.ImageCount} images), IoU >= {report.IouThreshold}, ");
            sb.Append(c, $"protection threshold {report.CoverThreshold * 100:0}%\n\n");
            sb.Append("| Kind | TP | FP | FN | Precision | Recall | F1 | Protection |\n");
            sb.Append("|---|---:|---:|---:|---:|---:|---:|---:|\n");
            foreach ((string kind, KindStats s) in report.PerKind)
            {
                sb.Append(c, $"| {kind} | {s.Tp} | {s.Fp} | {s.Fn} | {s.Precision:0.000} | {s.Recall:0.000} | {s.F1:0.000} | ");
                sb.Append(c, $"{s.Protection:0.000} ({s.Protected}/{s.Total}) |\n");
            }
            KindStats m = report.Micro;
            sb.Append(c, $"| **Total (micro)** | {m.Tp} | {m.Fp} | {m.Fn} | **{m.Precision:0.000}** | **{m.Recall:0.000}** | ");
            sb.Append(c, $"**{m.F1:0.000}** | **{m.Protection:0.000}** |\n\n");

            LatencyStats l = report.Latency;
            string stages = string.Join(", ", l.StagesMean.Select(kv => string.Create(c, $"{kv.Key} {kv.Value:0}")));
            sb.Append(c, $"Macro F1: {report.MacroF1:0.000} · Latency (ms): mean {l.Mean:0}, median {l.Median:0}, p95 {l.P95:0}");
            sb.Append(c, $" · Stage means: {stages}\n");
            return sb.ToString();
        }
    }
}