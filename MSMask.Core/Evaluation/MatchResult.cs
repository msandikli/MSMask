using MSMask.Core.Models;

namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// The result of matching the detections of one image to its labels.
    /// </summary>
    /// <param name="TruePositives">The matched pairs of a detection and a label.</param>
    /// <param name="FalsePositives">The detections that match no label.</param>
    /// <param name="FalseNegatives">The labels that no detection matches.</param>
    public record MatchResult(
        IReadOnlyList<(Detection Detection, GroundTruthLabel Label)> TruePositives,
        IReadOnlyList<Detection> FalsePositives,
        IReadOnlyList<GroundTruthLabel> FalseNegatives);
}