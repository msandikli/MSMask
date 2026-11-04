using MSMask.Core.Ocr;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Marks a test that needs the Tesseract language models. The test is skipped when no tessdata folder is found.
    /// </summary>
    /// <remarks>
    /// The models are large binary files that are not stored in the repository, so a fresh clone cannot run these
    /// tests until the files are downloaded. Skipping keeps the test run green and shows clearly why they did not run.
    /// </remarks>
    public sealed class TesseractFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TesseractFactAttribute"/> class.
        /// </summary>
        public TesseractFactAttribute()
        {
            if (TesseractOCREngine.FindTessdataDirectory() is null)
            {
                Skip = "Tesseract language models (tessdata) were not found.";
            }
        }
    }
}