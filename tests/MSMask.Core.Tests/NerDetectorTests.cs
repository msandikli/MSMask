using MSMask.Core.Lines;
using MSMask.Core.Models;
using MSMask.Core.Ner;
using MSMask.Core.Patterns;
using MSMask.Core.Tests.Fakes;
using SkiaSharp;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="NerSpanRefiner"/>, <see cref="NerDetector"/> and the NER stage of <see cref="Pipeline"/>,
    /// using <see cref="FakeEntityRecognizer"/> instead of the real model.
    /// </summary>
    public class NerDetectorTests
    {
        /// <summary>
        /// Refines the whole text as one entity and returns the kept parts as strings.
        /// </summary>
        /// <param name="text">The entity text.</param>
        /// <returns>The kept parts.</returns>
        private static string[] Refine(string text) =>
            NerSpanRefiner.Refine(text, 0, text.Length).Select(s => text[s.Start..s.End]).ToArray();

        /// <summary>
        /// Builds one OCR line from a space-separated text.
        /// </summary>
        /// <param name="text">The words of the line.</param>
        /// <param name="y">The top edge of the line.</param>
        /// <returns>The line.</returns>
        private static TextLine Line(string text, double y = 10) =>
            Assert.Single(LineBuilder.Build(FakeOcrEngine.Words(text, y: y)));

        /// <summary>
        /// The refinement rules: split at "/" and ",", trim short words at the edges, and drop parts that are too
        /// short, not capitalized or a short word in capitals.
        /// </summary>
        /// <param name="text">The entity text the model returned.</param>
        /// <param name="expected">The parts that are kept, joined with "|".</param>
        [Theory]
        [InlineData("Gebze / Kocaeli", "Gebze|Kocaeli")]
        [InlineData("Bayraklı, İzmir", "Bayraklı|İzmir")]
        [InlineData("AY Ayşe Yılmaz", "Ayşe Yılmaz")]
        [InlineData("Ayşe Yılmaz 12", "Ayşe Yılmaz")]
        [InlineData("ayşe", "")]
        [InlineData("A", "")]
        [InlineData("WARN", "")]
        [InlineData("GITHUB", "")]
        [InlineData("Ayşe", "Ayşe")]
        public void Refine_AppliesRules(string text, string expected)
        {
            Assert.Equal(expected, string.Join("|", Refine(text)));
        }

        /// <summary>
        /// Person and place entities become detections with the right kind, text, source and a box on the words;
        /// organizations and entities below the threshold are ignored.
        /// </summary>
        /// <returns>A task that completes when the test is done.</returns>
        [Fact]
        public async Task Detect_MapsKindsAndAppliesThreshold()
        {
            var recognizer = new FakeEntityRecognizer()
                .Add("Ayşe Yılmaz", "PER")
                .Add("Kadıköy", "LOC")
                .Add("Firma", "ORG")
                .Add("Mehmet", "PER", score: 0.4);
            TextLine line = Line("Ayşe Yılmaz Kadıköy Firma Mehmet");

            List<Detection> detections = await new NerDetector(recognizer).DetectAsync([line]);

            Assert.Equal(["KISI_ADI:Ayşe Yılmaz", "KONUM:Kadıköy"], detections.Select(d => $"{d.Kind}:{d.Text}"));
            Assert.All(detections, d => Assert.Equal(NerDetector.Source, d.Source));
            Assert.Equal(line.SpanBox(0, 11), detections[0].Box);
        }

        /// <summary>
        /// An entity that overlaps a range the patterns already found is dropped.
        /// </summary>
        /// <returns>A task that completes when the test is done.</returns>
        [Fact]
        public async Task Detect_SkipsRangesTakenByPatterns()
        {
            var recognizer = new FakeEntityRecognizer().Add("Yilmaz", "PER");
            TextLine line = Line("Kimden ali.Yilmaz@firma.com");
            var taken = new Dictionary<int, List<TextSpan>> { [line.LineId] = [new TextSpan(7, 27)] };

            List<Detection> detections = await new NerDetector(recognizer).DetectAsync([line], taken);

            Assert.Empty(detections);
        }

        /// <summary>
        /// Lines without letters are not sent to the model.
        /// </summary>
        /// <returns>A task that completes when the test is done.</returns>
        [Fact]
        public async Task Detect_SkipsLinesWithoutLetters()
        {
            var recognizer = new FakeEntityRecognizer();
            List<TextLine> lines = LineBuilder.Build(
            [
                .. FakeOcrEngine.Words("0532 123 45 67", y: 10),
                .. FakeOcrEngine.Words("Ayşe Yılmaz", y: 50),
            ]);

            await new NerDetector(recognizer).DetectAsync(lines);

            Assert.Equal(["Ayşe Yılmaz"], Assert.Single(recognizer.Calls));
        }

        /// <summary>
        /// With NER the pipeline supports every kind, adds "+ner" to its name, records the "ner" stage, and does not
        /// mask a surname inside an e-mail address a second time.
        /// </summary>
        /// <returns>A task that completes when the test is done.</returns>
        [Fact]
        public async Task Pipeline_WithNer_AddsPersonDetections()
        {
            List<Token> words =
            [
                .. FakeOcrEngine.Words("Kimden: Ayşe Yılmaz", y: 10),
                .. FakeOcrEngine.Words("ayse.Yilmaz@firma.com.tr", y: 50),
            ];
            var recognizer = new FakeEntityRecognizer().Add("Ayşe Yılmaz", "PER").Add("Yilmaz", "PER");
            var pipeline = new Pipeline(new FakeOcrEngine(words), ner: new NerDetector(recognizer));
            using var image = new SKBitmap(400, 100);

            PipelineResult result = await pipeline.AnalyzeAsync(image);

            Assert.Equal("fake+ner", pipeline.Name);
            Assert.Contains(Kinds.KisiAdi, pipeline.SupportedKinds);
            Assert.True(result.TimingsMs.ContainsKey("ner"));
            Assert.Equal(["EPOSTA:ayse.Yilmaz@firma.com.tr", "KISI_ADI:Ayşe Yılmaz"],
                result.Detections.Select(d => $"{d.Kind}:{d.Text}").Order());
        }

        /// <summary>
        /// Without NER the pipeline does not claim person names and places.
        /// </summary>
        [Fact]
        public void Pipeline_WithoutNer_ExcludesNerKinds()
        {
            var pipeline = new Pipeline(new FakeOcrEngine([]));

            Assert.Equal("fake", pipeline.Name);
            Assert.DoesNotContain(Kinds.KisiAdi, pipeline.SupportedKinds);
            Assert.DoesNotContain(Kinds.Konum, pipeline.SupportedKinds);
        }
    }
}