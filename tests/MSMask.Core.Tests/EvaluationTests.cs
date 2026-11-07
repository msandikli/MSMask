using MSMask.Core.Evaluation;
using MSMask.Core.Masking;
using MSMask.Core.Models;
using MSMask.Core.Tests.Fakes;
using SkiaSharp;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for the evaluation code: matching, protection, latency statistics, label loading and the full run.
    /// </summary>
    public class EvaluationTests
    {
        /// <summary>
        /// Creates a detection found by the rules.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="box">The box.</param>
        /// <returns>The detection.</returns>
        private static Detection Det(string kind, Box box) => new(kind, box, "x", "regex");

        /// <summary>
        /// Creates a ground-truth label.
        /// </summary>
        /// <param name="kind">The kind.</param>
        /// <param name="box">The box.</param>
        /// <returns>The label.</returns>
        private static GroundTruthLabel Label(string kind, Box box) => new(kind, box, "x");

        /// <summary>
        /// A detection matches only a label of the same kind, even when the boxes are identical.
        /// </summary>
        [Fact]
        public void Match_RequiresSameKind()
        {
            var box = new Box(10, 10, 110, 30);
            MatchResult result = DetectionMatcher.Match([Det(Kinds.Telefon, box)], [Label(Kinds.Tckn, box)]);

            Assert.Empty(result.TruePositives);
            Assert.Single(result.FalsePositives);
            Assert.Single(result.FalseNegatives);
        }

        /// <summary>
        /// When two labels overlap one detection, the detection goes to the label with the higher IoU,
        /// and the other label stays missed.
        /// </summary>
        [Fact]
        public void Match_PrefersHigherIou()
        {
            Detection detection = Det(Kinds.Tckn, new Box(0, 0, 100, 20));
            GroundTruthLabel weaker = Label(Kinds.Tckn, new Box(30, 0, 100, 20));
            GroundTruthLabel stronger = Label(Kinds.Tckn, new Box(5, 0, 100, 20));

            MatchResult result = DetectionMatcher.Match([detection], [weaker, stronger]);

            Assert.Same(stronger, Assert.Single(result.TruePositives).Label);
            Assert.Same(weaker, Assert.Single(result.FalseNegatives));
            Assert.Empty(result.FalsePositives);
        }

        /// <summary>
        /// An IoU of exactly 0.5 is a match; an IoU just below it is not.
        /// </summary>
        /// <param name="labelX1">The right edge of the label, which sets the IoU.</param>
        /// <param name="matched">Whether the pair must match.</param>
        [Theory]
        [InlineData(50.0, true)]   // IoU = 50 / 100 = 0.50
        [InlineData(49.0, false)]  // IoU = 49 / 100 = 0.49
        public void Match_IouThreshold(double labelX1, bool matched)
        {
            Detection detection = Det(Kinds.Iban, new Box(0, 0, 100, 10));
            GroundTruthLabel label = Label(Kinds.Iban, new Box(0, 0, labelX1, 10));

            MatchResult result = DetectionMatcher.Match([detection], [label]);

            Assert.Equal(matched ? 1 : 0, result.TruePositives.Count);
        }

        /// <summary>
        /// Protection uses the masker's own margins: a label slightly wider than the detection is still fully
        /// painted, while a label that sticks far out of the painted area is not.
        /// </summary>
        [Fact]
        public void CoveredFraction_UsesMaskMargins()
        {
            // Height 20 → margins of 8 px left/right and 3 px top/bottom: painted x 102..198, y 47..73.
            bool[,] painted = MaskGeometry.PixelMap(300, 120, [Det(Kinds.Tckn, new Box(110, 50, 190, 70))]);

            Assert.Equal(1.0, Evaluator.CoveredFraction(new Box(106, 50, 194, 70), painted));
            Assert.Equal(0.5, Evaluator.CoveredFraction(new Box(150, 50, 246, 70), painted), 3);
        }

        /// <summary>
        /// A label that lies completely outside the image has nothing visible, so it counts as covered.
        /// </summary>
        [Fact]
        public void CoveredFraction_OutsideImage_IsCovered()
        {
            bool[,] painted = MaskGeometry.PixelMap(100, 100, []);

            Assert.Equal(1.0, Evaluator.CoveredFraction(new Box(200, 200, 300, 220), painted));
        }

        /// <summary>
        /// Percentiles interpolate linearly between the two nearest sorted values.
        /// </summary>
        [Fact]
        public void Percentile_InterpolatesLinearly()
        {
            double[] values = [40, 10, 30, 20];

            Assert.Equal(25.0, LatencyStats.Percentile(values, 50), 9);
            Assert.Equal(38.5, LatencyStats.Percentile(values, 95), 9);
            Assert.Equal(10.0, LatencyStats.Percentile(values, 0), 9);
            Assert.Equal(0.0, LatencyStats.Percentile([], 50));
        }

        /// <summary>
        /// The latency summary uses the total time for mean, median and p95, and averages every other stage.
        /// </summary>
        [Fact]
        public void LatencyStats_From_SummarizesTotalsAndStages()
        {
            LatencyStats stats = LatencyStats.From(
            [
                new Dictionary<string, double> { ["ocr"] = 80, ["total"] = 100 },
                new Dictionary<string, double> { ["ocr"] = 160, ["total"] = 200 },
            ]);

            Assert.Equal(150.0, stats.Mean, 9);
            Assert.Equal(150.0, stats.Median, 9);
            Assert.Equal(195.0, stats.P95, 9);
            Assert.Equal(120.0, stats.StagesMean["ocr"], 9);
            Assert.False(stats.StagesMean.ContainsKey("total"));
        }

        /// <summary>
        /// Precision, recall, F1 and protection are zero instead of NaN when nothing was counted.
        /// </summary>
        [Fact]
        public void KindStats_Empty_IsZero()
        {
            var stats = new KindStats();

            Assert.Equal(0.0, stats.Precision);
            Assert.Equal(0.0, stats.Recall);
            Assert.Equal(0.0, stats.F1);
            Assert.Equal(0.0, stats.Protection);
        }

        /// <summary>
        /// A missing labels file gives a clear error.
        /// </summary>
        [Fact]
        public void LabelSet_Load_MissingFile_Throws()
        {
            string path = Path.Combine(Path.GetTempPath(), "msmask-" + Guid.NewGuid().ToString("N"), "labels.json");

            Assert.Throws<FileNotFoundException>(() => LabelSet.Load(path));
        }

        /// <summary>
        /// A full run on a small data set: one image with an identity number that is found and masked,
        /// a phone label that nothing detects, and a name label that is only evaluated with <c>allKinds</c>.
        /// The report files are written too.
        /// </summary>
        /// <returns>A task that completes when the test is done.</returns>
        [Fact]
        public async Task EvaluateAsync_EndToEnd()
        {
            string folder = Path.Combine(Path.GetTempPath(), "msmask-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                using (var bitmap = new SKBitmap(400, 100))
                {
                    bitmap.Erase(SKColors.White);
                    using SKData png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                    await File.WriteAllBytesAsync(Path.Combine(folder, "img_0000.png"), png.ToArray());
                }
                const string json = """
                    {
                      "images": [
                        {
                          "file": "img_0000.png",
                          "width": 400,
                          "height": 100,
                          "labels": [
                            { "kind": "TCKN", "box": [40, 10, 150, 30], "text": "10000000146" },
                            { "kind": "TELEFON", "box": [40, 50, 150, 70], "text": "10000000147" },
                            { "kind": "KISI_ADI", "box": [200, 10, 260, 30], "text": "Deniz" }
                          ]
                        }
                      ]
                    }
                    """;
                string labelsPath = Path.Combine(folder, "labels.json");
                await File.WriteAllTextAsync(labelsPath, json);

                List<Token> words =
                [
                    .. FakeOcrEngine.Words("TC 10000000146", y: 10),
                    .. FakeOcrEngine.Words("No 10000000147", y: 50),
                ];
                var pipeline = new Pipeline(new FakeOcrEngine(words));
                LabelSet labels = LabelSet.Load(labelsPath);

                EvaluationOutcome outcome = await Evaluator.EvaluateAsync(pipeline, labels);
                EvaluationReport report = outcome.Report;

                Assert.Equal(1, report.ImageCount);
                Assert.DoesNotContain(Kinds.KisiAdi, report.Kinds);
                Assert.Equal(1, report.PerKind[Kinds.Tckn].Tp);
                Assert.Equal(1, report.PerKind[Kinds.Tckn].Protected);
                Assert.Equal(1, report.PerKind[Kinds.Telefon].Fn);
                Assert.Equal(1.0, report.Micro.Precision);
                Assert.Equal(0.5, report.Micro.Recall);
                Assert.Equal(0.5, report.Micro.Protection);

                EvaluationError missed = Assert.Single(outcome.Errors, e => e.Type == EvaluationError.Missed);
                Assert.Equal(Kinds.Telefon, missed.Kind);
                Assert.Equal(["No 10000000147"], missed.Ocr!);
                EvaluationError unprotected = Assert.Single(outcome.Errors, e => e.Type == EvaluationError.Unprotected);
                Assert.False(unprotected.Matched);
                Assert.Equal(0.0, unprotected.Coverage);

                EvaluationOutcome all = await Evaluator.EvaluateAsync(pipeline, labels, allKinds: true);
                Assert.Equal(1, all.Report.PerKind[Kinds.KisiAdi].Fn);

                string prefix = Path.Combine(folder, "results", "fake");
                await ReportWriter.WriteAsync(outcome, prefix);
                Assert.True(File.Exists(prefix + ".json"));
                Assert.True(File.Exists(prefix + "_errors.jsonl"));
                Assert.Contains("| TCKN |", await File.ReadAllTextAsync(prefix + ".md"));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}