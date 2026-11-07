using MSMask.Core.Models;

namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Matches detections to ground-truth labels.
    /// </summary>
    /// <remarks>
    /// A detection and a label can only be matched when they have the same kind and their IoU is at least
    /// <see cref="IouThreshold"/>. Every possible pair is sorted by IoU, highest first, and pairs are taken greedily:
    /// a detection or a label that is already used is skipped. A second detection on an already matched label is
    /// therefore a false positive.
    /// </remarks>
    public static class DetectionMatcher
    {
        /// <summary>
        /// The minimum intersection over union for a detection to count as a match.
        /// </summary>
        public const double IouThreshold = 0.5;

        /// <summary>
        /// Matches the detections of one image to its labels.
        /// </summary>
        /// <param name="detections">The detections produced by the pipeline.</param>
        /// <param name="labels">The ground-truth labels of the image.</param>
        /// <param name="iouThreshold">The minimum IoU for a match.</param>
        /// <returns>The matched pairs, the unmatched detections and the unmatched labels.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detections"/> or <paramref name="labels"/> is <see langword="null"/>.</exception>
        public static MatchResult Match(IReadOnlyList<Detection> detections, IReadOnlyList<GroundTruthLabel> labels,
            double iouThreshold = IouThreshold)
        {
            ArgumentNullException.ThrowIfNull(detections);
            ArgumentNullException.ThrowIfNull(labels);

            var pairs = new List<(double Iou, int D, int L)>();
            for (int d = 0; d < detections.Count; d++)
            {
                for (int l = 0; l < labels.Count; l++)
                {
                    if (detections[d].Kind != labels[l].Kind)
                    {
                        continue;
                    }
                    double iou = detections[d].Box.Iou(labels[l].Box);
                    if (iou >= iouThreshold)
                    {
                        pairs.Add((iou, d, l));
                    }
                }
            }

            var usedDetections = new HashSet<int>();
            var usedLabels = new HashSet<int>();
            var truePositives = new List<(Detection, GroundTruthLabel)>();
            foreach ((_, int d, int l) in pairs.OrderByDescending(p => p.Iou))
            {
                if (usedDetections.Contains(d) || usedLabels.Contains(l))
                {
                    continue;
                }
                usedDetections.Add(d);
                usedLabels.Add(l);
                truePositives.Add((detections[d], labels[l]));
            }

            List<Detection> falsePositives = detections.Where((_, i) => !usedDetections.Contains(i)).ToList();
            List<GroundTruthLabel> falseNegatives = labels.Where((_, i) => !usedLabels.Contains(i)).ToList();
            return new MatchResult(truePositives, falsePositives, falseNegatives);
        }
    }
}