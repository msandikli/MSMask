namespace MSMask.Core.Models
{
    /// <summary>
    /// Represents a piece of sensitive data found in an image.
    /// </summary>
    /// <param name="Kind">The kind of sensitive data; one of the constants in <see cref="Kinds"/>.</param>
    /// <param name="Box">The region of the image that contains the data and must be masked.</param>
    /// <param name="Text">
    /// The detected text. Kept for evaluation and error analysis only; never returned by the service.
    /// Empty for face detections.
    /// </param>
    /// <param name="Source">
    /// The detector that produced the result: <c>"pattern"</c>, <c>"ner"</c> or <c>"face"</c>.
    /// </param>
    /// <param name="Score">
    /// The detector's confidence between 0 and 1. Pattern and face detections always use 1.0.
    /// </param>
    public record Detection(string Kind, Box Box, string Text, string Source, double Score = 1.0);
}
