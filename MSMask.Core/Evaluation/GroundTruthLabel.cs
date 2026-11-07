using MSMask.Core.Models;

namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Represents one piece of sensitive data that is known to be in a test image (a ground-truth label).
    /// </summary>
    /// <param name="Kind">The kind of data; one of the constants in <see cref="Kinds"/>.</param>
    /// <param name="Box">The exact region of the data in the image.</param>
    /// <param name="Text">The text that is drawn in the region, used in error reports.</param>
    public record GroundTruthLabel(string Kind, Box Box, string Text);
}
