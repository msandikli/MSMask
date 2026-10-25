using MSMask.Core.Masking;
using MSMask.Core.Models;
using SkiaSharp;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="MaskGeometry"/> and <see cref="ImageMasker"/>.
    /// </summary>
    public class MaskingTests
    {
        /// <summary>
        /// Creates a bitmap filled with one color.
        /// </summary>
        /// <param name="width">The bitmap width in pixels.</param>
        /// <param name="height">The bitmap height in pixels.</param>
        /// <param name="color">The fill color.</param>
        /// <param name="colorType">The pixel format.</param>
        /// <param name="alphaType">How the alpha channel is stored.</param>
        /// <returns>A new bitmap. The caller must dispose it.</returns>
        private static SKBitmap Filled(int width, int height, SKColor color,
            SKColorType colorType = SKColorType.Bgra8888, SKAlphaType alphaType = SKAlphaType.Premul)
        {
            var bitmap = new SKBitmap(new SKImageInfo(width, height, colorType, alphaType));
            bitmap.Erase(color);
            return bitmap;
        }

        /// <summary>
        /// A text box gets 0.4 x height of margin on the left and right and 0.15 x height on the top and bottom.
        /// </summary>
        [Fact]
        public void Region_TextBox_AddsMarginFromHeight()
        {
            // Height 20: 8 px on the left and right, 3 px on the top and bottom.
            var detection = new Detection(Kinds.Tckn, new Box(50, 40, 150, 60), "x", "pattern");
            Assert.Equal(new PixelRect(42, 37, 158, 63), MaskGeometry.Region(detection, 200, 100));
        }

        /// <summary>
        /// Fractional edges are rounded outwards so that partly covered pixels are painted.
        /// </summary>
        [Fact]
        public void Region_FractionalEdges_AreRoundedOutwards()
        {
            // Height 10: margins 4 and 1.5, so the box becomes (6.2, 18.5, 24.3, 31.5).
            var detection = new Detection(Kinds.Eposta, new Box(10.2, 20, 20.3, 30), "x", "pattern");
            Assert.Equal(new PixelRect(6, 18, 25, 32), MaskGeometry.Region(detection, 100, 100));
        }

        /// <summary>
        /// The region is clipped to the image; a box completely outside the image gives an empty region.
        /// </summary>
        [Fact]
        public void Region_IsClippedToImage()
        {
            var partly = new Detection(Kinds.Eposta, new Box(-10, -10, 20, 20), "", "pattern");
            Assert.Equal(new PixelRect(0, 0, 32, 25), MaskGeometry.Region(partly, 50, 50));

            var outside = new Detection(Kinds.Eposta, new Box(200, 200, 220, 210), "", "pattern");
            Assert.True(MaskGeometry.Region(outside, 50, 50).IsEmpty);
        }

        /// <summary>
        /// The masked copy has a solid black box with margins, everything else is unchanged,
        /// and the input image is not modified.
        /// </summary>
        [Fact]
        public void Mask_PaintsSolidBlackBoxAndKeepsInput()
        {
            using SKBitmap image = Filled(200, 100, SKColors.White);
            var detection = new Detection(Kinds.Tckn, new Box(50, 40, 150, 60), "x", "pattern");

            using SKBitmap masked = ImageMasker.Mask(image, [detection]);

            Assert.NotSame(image, masked);
            PixelRect r = MaskGeometry.Region(detection, 200, 100);
            for (int y = 0; y < 100; y++)
            {
                for (int x = 0; x < 200; x++)
                {
                    bool inside = x >= r.X0 && x < r.X1 && y >= r.Y0 && y < r.Y1;
                    Assert.Equal(inside ? SKColors.Black : SKColors.White, masked.GetPixel(x, y));
                    Assert.Equal(SKColors.White, image.GetPixel(x, y));
                }
            }
        }

        /// <summary>
        /// The pixel map marks exactly the pixels that the masker paints black.
        /// </summary>
        [Fact]
        public void PixelMap_MatchesPaintedPixels()
        {
            using SKBitmap image = Filled(120, 80, SKColors.White);
            var detections = new List<Detection>
            {
                new(Kinds.Telefon, new Box(10.5, 10.25, 60.75, 24.5), "x", "pattern"),
                new(Kinds.Iban, new Box(70, 50, 130, 70), "x", "pattern"), // runs past the right and bottom edges
            };

            using SKBitmap masked = ImageMasker.Mask(image, detections);
            bool[,] map = MaskGeometry.PixelMap(120, 80, detections);

            Assert.Equal(80, map.GetLength(0));
            Assert.Equal(120, map.GetLength(1));
            for (int y = 0; y < 80; y++)
            {
                for (int x = 0; x < 120; x++)
                {
                    Assert.Equal(map[y, x], masked.GetPixel(x, y) == SKColors.Black);
                }
            }
        }

        /// <summary>
        /// A transparent area is painted opaque black and the pixel format is kept.
        /// </summary>
        [Fact]
        public void Mask_TransparentImage_PaintsOpaqueBlack()
        {
            using SKBitmap image = Filled(50, 50, new SKColor(255, 255, 255, 0), SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using SKBitmap masked = ImageMasker.Mask(image, [new Detection(Kinds.Eposta, new Box(-10, -10, 20, 20), "", "pattern")]);

            Assert.Equal(SKColorType.Rgba8888, masked.ColorType);
            Assert.Equal(SKColors.Black, masked.GetPixel(0, 0));
            Assert.Equal(0, masked.GetPixel(49, 49).Alpha);
        }

        /// <summary>
        /// A grayscale image stays grayscale and the region becomes black.
        /// </summary>
        [Fact]
        public void Mask_GrayscaleImage_KeepsColorType()
        {
            using SKBitmap image = Filled(40, 40, SKColors.White, SKColorType.Gray8, SKAlphaType.Opaque);
            using SKBitmap masked = ImageMasker.Mask(image, [new Detection(Kinds.Tckn, new Box(10, 10, 30, 20), "", "pattern")]);

            Assert.Equal(SKColorType.Gray8, masked.ColorType);
            Assert.Equal(SKColors.Black, masked.GetPixel(20, 15));
            Assert.Equal(SKColors.White, masked.GetPixel(0, 0));
        }

        /// <summary>
        /// Without detections the result is an unchanged copy, not the same object.
        /// </summary>
        [Fact]
        public void Mask_NoDetections_ReturnsUnchangedCopy()
        {
            using SKBitmap image = Filled(10, 10, SKColors.Red);
            using SKBitmap masked = ImageMasker.Mask(image, []);

            Assert.NotSame(image, masked);
            Assert.Equal(SKColors.Red, masked.GetPixel(5, 5));
        }
    }
}