using MSMask.Core.Ner;

namespace MSMask.Core.Tests
{
    /// <summary>
    /// Marks a test that needs the NER model files. The test is skipped when the model folder is not found.
    /// </summary>
    /// <remarks>
    /// The model is a large download that is not stored in the repository, so a fresh clone cannot run these tests
    /// until it is downloaded. Skipping keeps the test run green and shows clearly why they did not run.
    /// </remarks>
    public sealed class NerModelFactAttribute : FactAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NerModelFactAttribute"/> class.
        /// </summary>
        public NerModelFactAttribute()
        {
            if (BertEntityRecognizer.FindModelDirectory() is null)
            {
                Skip = "The NER model (models/bert-base-turkish-cased-ner) was not found.";
            }
        }
    }
}