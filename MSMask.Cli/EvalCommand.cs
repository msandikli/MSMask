using System.Diagnostics;
using System.Globalization;
using MSMask.Core;
using MSMask.Core.Evaluation;
using MSMask.Core.Ocr;

namespace MSMask.Cli
{
    /// <summary>
    /// The <c>eval</c> command: measures a pipeline on a labeled data set and writes the reports.
    /// </summary>
    /// <remarks>
    /// Usage: <c>eval &lt;labels.json&gt; [--engine tesseract] [--no-checksum] [--all-kinds] [--limit N] [--out prefix]</c>.
    /// Without <c>--out</c>, the reports go to <c>results/&lt;data set folder&gt;/&lt;engine&gt;</c>, for example
    /// <c>results/dev/tesseract.json</c>.
    /// </remarks>
    public static class EvalCommand
    {
        /// <summary>
        /// Runs the command.
        /// </summary>
        /// <param name="args">The arguments after the command name.</param>
        /// <returns>The process exit code: 0 on success, 2 for wrong usage.</returns>
        public static async Task<int> RunAsync(IReadOnlyList<string> args)
        {
            CommandLineArguments parsed = CommandLineArguments.Parse(args,
                flagNames: new HashSet<string> { "--no-checksum", "--all-kinds" },
                valueNames: new HashSet<string> { "--engine", "--limit", "--out" });
            if (parsed.Positionals.Count != 1)
            {
                Console.Error.WriteLine("Usage: eval <labels.json> [--engine tesseract] [--no-checksum] [--all-kinds] [--limit N] [--out prefix]");
                return 2;
            }

            string labelsPath = parsed.Positionals[0];
            string engineName = parsed.Value("--engine", "tesseract")!;
            bool checksum = !parsed.Flags.Contains("--no-checksum");
            int? limit = parsed.Value("--limit") is string text ? int.Parse(text, CultureInfo.InvariantCulture) : null;
            string datasetName = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(labelsPath)) ?? ".").Name;
            string prefix = parsed.Value("--out")
                ?? Path.Combine("results", datasetName, engineName + (checksum ? "" : "_nochecksum"));

            LabelSet labels = LabelSet.Load(labelsPath);
            int total = limit is int n ? Math.Min(n, labels.Images.Count) : labels.Images.Count;

            IOCREngine engine = EngineFactory.Create(engineName);
            try
            {
                var pipeline = new Pipeline(engine, checksum);
                long start = Stopwatch.GetTimestamp();
                var progress = new Progress<int>(done =>
                {
                    if (done % 10 == 0 || done == total)
                    {
                        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
                            $"  {done}/{total} images ({Stopwatch.GetElapsedTime(start).TotalSeconds:0} s)"));
                    }
                });

                EvaluationOutcome outcome = await Evaluator.EvaluateAsync(pipeline, labels,
                    allKinds: parsed.Flags.Contains("--all-kinds"), limit: limit, progress: progress);
                await ReportWriter.WriteAsync(outcome, prefix);

                Console.WriteLine(ReportWriter.ToMarkdown(outcome.Report));
                Console.WriteLine($"Results: {prefix}.json, {prefix}.md, errors: {prefix}_errors.jsonl");
                return 0;
            }
            finally
            {
                (engine as IDisposable)?.Dispose();
            }
        }
    }
}