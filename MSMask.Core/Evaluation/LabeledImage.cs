namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Represents one test image and the sensitive data that is known to be in it.
    /// </summary>
    /// <param name="File">The image path relative to the folder of <c>labels.json</c>, for example <c>images/0000.png</c>.</param>
    /// <param name="Width">The image width in pixels.</param>
    /// <param name="Height">The image height in pixels.</param>
    /// <param name="Labels">The ground-truth labels of the image.</param>
    public record LabeledImage(string File, int Width, int Height, IReadOnlyList<GroundTruthLabel> Labels);
}