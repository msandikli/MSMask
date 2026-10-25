using MSMask.Core.Models;
using SkiaSharp;

namespace MSMask.Core
{
    /// <summary>
    /// Holds a masked image together with the analysis that produced it.
    /// </summary>
    /// <remarks>
    /// The masked bitmap uses native memory, so this type is disposable. Use it with a <c>using</c> statement.
    /// </remarks>
    public sealed class MaskedImage : IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaskedImage"/> class.
        /// </summary>
        /// <param name="image">The masked image. The new instance takes ownership of it.</param>
        /// <param name="result">The analysis that produced the masks.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="result"/> is <see langword="null"/>.</exception>
        public MaskedImage(SKBitmap image, PipelineResult result)
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(result);
            Image = image;
            Result = result;
        }

        /// <summary>
        /// Gets the masked image.
        /// </summary>
        public SKBitmap Image { get; }

        /// <summary>
        /// Gets the analysis: detections, stage timings and OCR words.
        /// </summary>
        public PipelineResult Result { get; }

        /// <summary>
        /// Releases the native memory of the masked image.
        /// </summary>
        public void Dispose() => Image.Dispose();
    }
}