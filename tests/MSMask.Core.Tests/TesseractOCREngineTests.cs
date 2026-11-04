using MSMask.Core.Models;
using MSMask.Core.Ocr;
using MSMask.Core.Ocr.MSMask.Core.Ocr;
using SkiaSharp;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="OcrImagePreparer"/> and <see cref="TesseractOCREngine"/>.
    /// Tests marked with <see cref="TesseractFactAttribute"/> run the real OCR engine.
    /// </summary>
    public class TesseractOCREngineTests
    {
        /// <summary>
        /// Draws lines of text the way they would appear in a screenshot.
        /// </summary>
        /// <param name="lines">The lines to draw, from top to bottom.</param>
        /// <param name="dark"><see langword="true"/> for light text on a dark background.</param>
        /// <returns>A new bitmap. The caller must dispose it.</returns>
        private static SKBitmap Render(string[] lines, bool dark = false)
        {
            var bitmap = new SKBitmap(720, 60 + 44 * lines.Length);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(dark ? new SKColor(24, 24, 28) : SKColors.White);

            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 22);
            using var paint = new SKPaint { Color = dark ? new SKColor(230, 230, 230) : SKColors.Black, IsAntialias = true };
            for (int i = 0; i < lines.Length; i++)
            {
                canvas.DrawText(lines[i], 20, 42 + 44 * i, SKTextAlign.Left, font, paint);
            }
            return bitmap;
        }

        /// <summary>
        /// Word rows are kept; other levels, empty words and negative confidences are dropped;
        /// coordinates are divided by the scale.
        /// </summary>
        [Fact]
        public void ParseTsv_KeepsWordsAndUndoesScale()
        {
            string tsv = string.Join('\n',
                "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext",
                "1\t1\t0\t0\t0\t0\t0\t0\t800\t600\t-1\t",
                "4\t1\t1\t1\t1\t0\t20\t40\t300\t40\t-1\t",
                "5\t1\t1\t1\t1\t1\t20\t40\t100\t40\t96.5\tTelefon:",
                "5\t1\t1\t1\t1\t2\t140\t40\t80\t40\t91\t0532",
                "5\t1\t1\t1\t1\t3\t240\t40\t10\t40\t-1\tx",
                "5\t1\t1\t1\t1\t4\t260\t40\t10\t40\t50\t   ");

            List<Token> tokens = TesseractOCREngine.ParseTsv(tsv, scale: 2.0);

            Assert.Equal(["Telefon:", "0532"], tokens.Select(t => t.Text));
            Assert.Equal(new Box(10, 20, 60, 40), tokens[0].Box);
            Assert.Equal(new Box(70, 20, 110, 40), tokens[1].Box);
            Assert.Equal(0.965, tokens[0].Conf, precision: 9);
            Assert.All(tokens, t => Assert.Equal(-1, t.LineId));
        }

        /// <summary>
        /// Gray values follow the BT.601 luma weights.
        /// </summary>
        [Fact]
        public void ToGray_UsesLumaWeights()
        {
            using var bitmap = new SKBitmap(4, 1);
            bitmap.SetPixel(0, 0, SKColors.White);
            bitmap.SetPixel(1, 0, SKColors.Black);
            bitmap.SetPixel(2, 0, new SKColor(255, 0, 0));
            bitmap.SetPixel(3, 0, new SKColor(0, 255, 0));

            Assert.Equal(new byte[] { 255, 0, 76, 150 }, OcrImagePreparer.ToGray(bitmap));
        }

        /// <summary>
        /// An image is dark when its average gray value is below 128; inverting flips every value.
        /// </summary>
        [Fact]
        public void IsDarkAndInvert()
        {
            byte[] gray = [10, 20, 250];
            Assert.True(OcrImagePreparer.IsDark(gray));

            OcrImagePreparer.Invert(gray);
            Assert.Equal(new byte[] { 245, 235, 5 }, gray);
            Assert.False(OcrImagePreparer.IsDark(gray));
            Assert.False(OcrImagePreparer.IsDark([]));
        }

        /// <summary>
        /// The prepared image is enlarged, grayscale, and a dark theme becomes light.
        /// </summary>
        [Fact]
        public void PrepareAsPng_DarkImage_IsInvertedAndEnlarged()
        {
            using var dark = new SKBitmap(30, 20);
            dark.Erase(new SKColor(20, 20, 20));

            using SKBitmap prepared = SKBitmap.Decode(OcrImagePreparer.PrepareAsPng(dark, scale: 2.0, autoInvert: true));

            Assert.Equal(60, prepared.Width);
            Assert.Equal(40, prepared.Height);
            SKColor pixel = prepared.GetPixel(30, 20);
            Assert.Equal(pixel.Red, pixel.Green);
            Assert.True(pixel.Red > 200, $"Expected a light pixel, got {pixel}.");
        }

        /// <summary>
        /// A folder without eng.traineddata is rejected with a clear error.
        /// </summary>
        [Fact]
        public void Constructor_FolderWithoutModels_Throws()
        {
            string empty = Directory.CreateTempSubdirectory().FullName;
            try
            {
                Assert.Throws<FileNotFoundException>(() => new TesseractOCREngine(empty));
            }
            finally
            {
                Directory.Delete(empty);
            }
        }

        /// <summary>
        /// Real OCR: an identity number, a phone number and an e-mail address are read and detected,
        /// and every box lies inside the original image.
        /// </summary>
        [TesseractFact]
        public async Task Pipeline_RealTesseract_DetectsThreeKinds()
        {
            using var engine = new TesseractOCREngine();
            var pipeline = new Pipeline(engine);
            using SKBitmap image = Render(["T.C. Kimlik No: 10000000146", "Telefon: 0532 123 45 67", "E-posta: ali.veli@firma.com.tr"]);

            PipelineResult result = await pipeline.AnalyzeAsync(image);

            Assert.Equal([Kinds.Eposta, Kinds.Tckn, Kinds.Telefon], result.Detections.Select(d => d.Kind).Order());
            Assert.All(result.Detections, d =>
            {
                Assert.InRange(d.Box.X0, 0, d.Box.X1);
                Assert.InRange(d.Box.X1, d.Box.X0, image.Width);
                Assert.InRange(d.Box.Y0, 0, d.Box.Y1);
                Assert.InRange(d.Box.Y1, d.Box.Y0, image.Height);
            });
        }

        /// <summary>
        /// Real OCR on a dark theme: the image is inverted before recognition, so the IBAN is still read.
        /// </summary>
        [TesseractFact]
        public async Task Pipeline_RealTesseract_ReadsDarkTheme()
        {
            using var engine = new TesseractOCREngine();
            var pipeline = new Pipeline(engine);
            using SKBitmap image = Render(["IBAN: TR33 0006 1005 1978 6457 8413 26"], dark: true);

            PipelineResult result = await pipeline.AnalyzeAsync(image);

            Assert.Equal([Kinds.Iban], result.Detections.Select(d => d.Kind));
        }
    }
}