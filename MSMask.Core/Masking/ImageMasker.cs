using MSMask.Core.Models;
using SkiaSharp;

namespace MSMask.Core.Masking
{
    /// <summary>
    /// Paints solid black boxes over the detected regions of an image.
    /// </summary>
    /// <remarks>
    /// Blurring and pixelation are not used on purpose: blurred or pixelated text can often be
    /// reconstructed, while an opaque black box removes the information completely.
    /// </remarks>
    public static class ImageMasker
    {
        /// <summary>
        /// Creates a masked copy of an image.
        /// </summary>
        /// <remarks>
        /// The input image is never modified. The copy keeps the input's color type (for example a grayscale
        /// image stays grayscale). Each region from <see cref="MaskGeometry.Region"/> is filled with opaque black,
        /// so a transparent area becomes visible black instead of staying see-through.
        /// </remarks>
        /// <param name="image">The image to mask.</param>
        /// <param name="detections">The detections to paint over.</param>
        /// <returns>A new bitmap with the regions painted black. The caller owns it and must dispose it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="detections"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the image cannot be copied, for example because its color type is not supported.</exception>
        public static SKBitmap Mask(SKBitmap image, IEnumerable<Detection> detections)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(detections);

            SKBitmap result = image.Copy() ?? throw new InvalidOperationException("The image could not be copied.");
            foreach (Detection detection in detections)
            {
                PixelRect r = MaskGeometry.Region(detection, result.Width, result.Height);
                if (!r.IsEmpty)
                {
                    result.Erase(SKColors.Black, new SKRectI(r.X0, r.Y0, r.X1, r.Y1));
                }
            }
            return result;
        }
    }
}