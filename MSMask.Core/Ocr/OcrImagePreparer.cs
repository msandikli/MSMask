using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Ocr
{
    using SkiaSharp;

    namespace MSMask.Core.Ocr
    {
        /// <summary>
        /// Prepares a screenshot for OCR: converts it to grayscale, turns dark themes into dark-on-light text
        /// and enlarges it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// OCR models are trained mostly on dark text on a light background, so light text on a dark theme is read
        /// much worse. An image whose average brightness is below the middle (128) is therefore inverted.
        /// </para>
        /// <para>
        /// Screen text is small (often 11–14 px high), smaller than the text the models are tuned for, so the image
        /// is enlarged before recognition. The boxes returned by the engine must then be divided by the same factor.
        /// </para>
        /// </remarks>
        public static class OcrImagePreparer
        {
            /// <summary>
            /// The average brightness below which an image is treated as a dark theme.
            /// </summary>
            public const double DarkThreshold = 128.0;

            /// <summary>
            /// Converts an image to 8-bit grayscale.
            /// </summary>
            /// <remarks>
            /// Uses the ITU-R BT.601 luma weights (0.299 R + 0.587 G + 0.114 B) in 16-bit fixed point.
            /// The alpha channel is ignored: every pixel contributes its color as if it were opaque.
            /// </remarks>
            /// <param name="image">The image to convert. Any color type is accepted.</param>
            /// <returns>The gray values, one byte per pixel, row by row (<c>width * height</c> bytes).</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the pixels of <paramref name="image"/> cannot be read.</exception>
            public static byte[] ToGray(SKBitmap image)
            {
                ArgumentNullException.ThrowIfNull(image);
                int width = image.Width;
                int height = image.Height;

                // Read the pixels as unpremultiplied RGBA so that the color of semi-transparent pixels is kept.
                var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                using var rgba = new SKBitmap(info);
                using SKPixmap source = image.PeekPixels() ?? throw new InvalidOperationException("The image pixels cannot be read.");
                using SKPixmap target = rgba.PeekPixels();
                if (!source.ReadPixels(target))
                {
                    throw new InvalidOperationException("The image could not be converted to RGBA.");
                }

                ReadOnlySpan<byte> pixels = rgba.GetPixelSpan();
                var gray = new byte[width * height];
                for (int i = 0; i < gray.Length; i++)
                {
                    int r = pixels[i * 4];
                    int g = pixels[i * 4 + 1];
                    int b = pixels[i * 4 + 2];
                    gray[i] = (byte)((r * 19595 + g * 38470 + b * 7471 + 0x8000) >> 16);
                }
                return gray;
            }

            /// <summary>
            /// Determines whether a grayscale image is dark on average.
            /// </summary>
            /// <param name="gray">The gray values returned by <see cref="ToGray"/>.</param>
            /// <returns><see langword="true"/> if the mean value is below <see cref="DarkThreshold"/>; otherwise <see langword="false"/>.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="gray"/> is <see langword="null"/>.</exception>
            public static bool IsDark(byte[] gray)
            {
                ArgumentNullException.ThrowIfNull(gray);
                if (gray.Length == 0)
                {
                    return false;
                }
                long sum = 0;
                foreach (byte v in gray)
                {
                    sum += v;
                }
                return (double)sum / gray.Length < DarkThreshold;
            }

            /// <summary>
            /// Inverts gray values in place (0 becomes 255 and 255 becomes 0).
            /// </summary>
            /// <param name="gray">The gray values to invert.</param>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="gray"/> is <see langword="null"/>.</exception>
            public static void Invert(byte[] gray)
            {
                ArgumentNullException.ThrowIfNull(gray);
                for (int i = 0; i < gray.Length; i++)
                {
                    gray[i] = (byte)(255 - gray[i]);
                }
            }

            /// <summary>
            /// Prepares an image for OCR and encodes it as PNG.
            /// </summary>
            /// <param name="image">The original image.</param>
            /// <param name="scale">The enlargement factor, for example 2.0. Use 1.0 to keep the original size.</param>
            /// <param name="autoInvert"><see langword="true"/> to invert dark images; <see langword="false"/> to keep them as they are.</param>
            /// <returns>The PNG bytes of the grayscale, possibly inverted and enlarged image.</returns>
            /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="scale"/> is not positive.</exception>
            /// <exception cref="InvalidOperationException">Thrown when the image cannot be converted, resized or encoded.</exception>
            public static byte[] PrepareAsPng(SKBitmap image, double scale, bool autoInvert)
            {
                ArgumentNullException.ThrowIfNull(image);
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);

                byte[] gray = ToGray(image);
                if (autoInvert && IsDark(gray))
                {
                    Invert(gray);
                }

                var grayInfo = new SKImageInfo(image.Width, image.Height, SKColorType.Gray8, SKAlphaType.Opaque);
                using var grayBitmap = new SKBitmap(grayInfo);
                gray.CopyTo(grayBitmap.GetPixelSpan());

                using SKBitmap scaled = Resize(grayBitmap, scale);
                using SKData png = scaled.Encode(SKEncodedImageFormat.Png, 100)
                    ?? throw new InvalidOperationException("The image could not be encoded as PNG.");
                return png.ToArray();
            }

            /// <summary>
            /// Enlarges or shrinks a bitmap with bicubic (Catmull-Rom) sampling, which keeps text edges sharp.
            /// </summary>
            /// <param name="bitmap">The bitmap to resize.</param>
            /// <param name="scale">The resize factor.</param>
            /// <returns>A new bitmap; a plain copy when <paramref name="scale"/> is 1. The caller must dispose it.</returns>
            /// <exception cref="InvalidOperationException">Thrown when the bitmap cannot be resized.</exception>
            private static SKBitmap Resize(SKBitmap bitmap, double scale)
            {
                if (scale == 1.0)
                {
                    return bitmap.Copy() ?? throw new InvalidOperationException("The image could not be copied.");
                }
                int width = Math.Max(1, (int)(bitmap.Width * scale));
                int height = Math.Max(1, (int)(bitmap.Height * scale));
                var info = new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType);
                return bitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.CatmullRom))
                    ?? throw new InvalidOperationException("The image could not be resized.");
            }
        }
    }
}
