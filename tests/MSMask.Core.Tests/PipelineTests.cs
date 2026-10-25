using MSMask.Core.Models;
using MSMask.Core.Tests.Fakes;
using SkiaSharp;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Pipeline"/>, using <see cref="FakeOcrEngine"/> instead of a real OCR engine.
    /// </summary>
    public class PipelineTests
    {
        /// <summary>
        /// Creates a white bitmap.
        /// </summary>
        /// <param name="width">The bitmap width in pixels.</param>
        /// <param name="height">The bitmap height in pixels.</param>
        /// <returns>A new bitmap. The caller must dispose it.</returns>
        private static SKBitmap WhiteImage(int width, int height)
        {
            var bitmap = new SKBitmap(width, height);
            bitmap.Erase(SKColors.White);
            return bitmap;
        }

        /// <summary>
        /// Three rows of words: an identity number, a phone number and an order number.
        /// Only the first two are sensitive.
        /// </summary>
        /// <returns>The words of the three rows.</returns>
        private static List<Token> ThreeRows() =>
        [
            .. FakeOcrEngine.Words("TC 10000000146", y: 10),
            .. FakeOcrEngine.Words("Tel 0532 123 45 67", y: 50),
            .. FakeOcrEngine.Words("Sipariş 5321234567", y: 90),
        ];

        /// <summary>
        /// The pipeline finds the identity number and the phone number, records every stage time
        /// and returns a new masked image.
        /// </summary>
        [Fact]
        public async Task MaskAsync_FakeOcr_FindsSensitiveDataAndRecordsTimings()
        {
            var pipeline = new Pipeline(new FakeOcrEngine(ThreeRows()));
            using SKBitmap image = WhiteImage(400, 140);

            using MaskedImage masked = await pipeline.MaskAsync(image);

            Assert.Equal([Kinds.Tckn, Kinds.Telefon], masked.Result.Detections.Select(d => d.Kind).Order());
            foreach (string key in new[] { "ocr", "lines", "patterns", "mask", "total" })
            {
                Assert.True(masked.Result.TimingsMs.ContainsKey(key), $"Missing timing '{key}'.");
            }
            Assert.NotSame(image, masked.Image);
        }

        /// <summary>
        /// The detection box covers the matched words, the masked copy is black there, and the input stays white.
        /// </summary>
        [Fact]
        public async Task MaskAsync_PaintsDetectionAndKeepsInput()
        {
            List<Token> tokens = FakeOcrEngine.Words("TC 10000000146", y: 10);
            var pipeline = new Pipeline(new FakeOcrEngine(tokens));
            using SKBitmap image = WhiteImage(300, 60);

            using MaskedImage masked = await pipeline.MaskAsync(image);

            Detection detection = Assert.Single(masked.Result.Detections);
            Assert.Equal(tokens[1].Box, detection.Box);
            Assert.Equal("10000000146", detection.Text);
            Assert.Equal(Pipeline.PatternSource, detection.Source);

            int cx = (int)((detection.Box.X0 + detection.Box.X1) / 2);
            int cy = (int)((detection.Box.Y0 + detection.Box.Y1) / 2);
            Assert.Equal(SKColors.Black, masked.Image.GetPixel(cx, cy));
            Assert.Equal(SKColors.White, image.GetPixel(cx, cy));
        }

        /// <summary>
        /// With check-digit validation an invalid identity number is ignored; without it the number is reported.
        /// </summary>
        [Fact]
        public async Task AnalyzeAsync_ChecksumAblation()
        {
            List<Token> tokens = FakeOcrEngine.Words("No 10000000147");
            using SKBitmap image = WhiteImage(10, 10);

            PipelineResult strict = await new Pipeline(new FakeOcrEngine(tokens)).AnalyzeAsync(image);
            PipelineResult loose = await new Pipeline(new FakeOcrEngine(tokens), checksum: false).AnalyzeAsync(image);

            Assert.Empty(strict.Detections);
            Assert.Equal([Kinds.Tckn], loose.Detections.Select(d => d.Kind));
        }

        /// <summary>
        /// The result keeps every OCR word with the line id assigned by line building.
        /// </summary>
        [Fact]
        public async Task AnalyzeAsync_ReturnsTokensWithLineIds()
        {
            var pipeline = new Pipeline(new FakeOcrEngine(ThreeRows()));
            using SKBitmap image = WhiteImage(400, 140);

            PipelineResult result = await pipeline.AnalyzeAsync(image);

            Assert.Equal(9, result.Tokens.Count);
            Assert.Equal([0, 1, 2], result.Tokens.Select(t => t.LineId).Distinct());
            Assert.False(result.TimingsMs.ContainsKey("mask"));
        }

        /// <summary>
        /// Without the NER model, person names and locations are not supported.
        /// </summary>
        [Fact]
        public void SupportedKinds_WithoutNer_ExcludesNameAndLocation()
        {
            var pipeline = new Pipeline(new FakeOcrEngine([]));
            var expected = new HashSet<string>
            {
                Kinds.Tckn, Kinds.Telefon, Kinds.Eposta, Kinds.Iban, Kinds.KrediKarti, Kinds.ApiAnahtari,
            };
            Assert.True(expected.SetEquals(pipeline.SupportedKinds));
        }

        /// <summary>
        /// A cancelled token stops the pipeline before the OCR engine is called.
        /// </summary>
        [Fact]
        public async Task AnalyzeAsync_CancelledToken_Throws()
        {
            var engine = new FakeOcrEngine(ThreeRows());
            var pipeline = new Pipeline(engine);
            using SKBitmap image = WhiteImage(10, 10);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pipeline.AnalyzeAsync(image, cts.Token));
            Assert.Equal(0, engine.CallCount);
        }

        /// <summary>
        /// A pipeline cannot be created without an OCR engine.
        /// </summary>
        [Fact]
        public void Constructor_NullEngine_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Pipeline(null!));
        }
    }
}