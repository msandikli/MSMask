using MSMask.Core.Models;
using SkiaSharp;

namespace MSMask.Core.Ocr
{
    /// <summary>
    /// Reads the words in an image. Every OCR engine (Tesseract, Windows OCR, ...) implements this interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An engine may enlarge, invert or otherwise preprocess the image internally, but the boxes it returns
    /// must always be in the coordinates of the original image, because the masks are painted there.
    /// </para>
    /// <para>
    /// The method is asynchronous because some engines are asynchronous by nature (Windows OCR) and others are
    /// slow enough that a user interface must not wait for them on its own thread.
    /// </para>
    /// </remarks>
    public interface IOCREngine
    {
        /// <summary>
        /// Gets a short name for the engine, used in reports.
        /// </summary>
        /// <value>For example <c>"tesseract"</c> or <c>"windows"</c>.</value>
        string Name { get; }

        /// <summary>
        /// Recognizes the words in an image.
        /// </summary>
        /// <param name="image">The image to read. The engine must not modify or dispose it.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>
        /// One token per recognized word, with its box in original image coordinates. The order does not matter;
        /// words are grouped into lines by geometry afterwards.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        Task<IReadOnlyList<Token>> ReadAsync(SKBitmap image, CancellationToken cancellationToken = default);
    }
}