namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Counts the results for one kind of data (or for all kinds together) and derives the metrics from them.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description><b>Precision</b>: of everything the system masked, how much was really sensitive (TP / (TP + FP)).</description></item>
    /// <item><description><b>Recall</b>: of everything sensitive, how much the system found (TP / (TP + FN)).</description></item>
    /// <item><description><b>F1</b>: the harmonic mean of precision and recall.</description></item>
    /// <item><description><b>Protection</b>: the share of labels whose area is really covered by the painted masks.</description></item>
    /// </list>
    /// </remarks>
    public sealed class KindStats
    {
        /// <summary>
        /// Gets or sets the number of detections that match a label (true positives).
        /// </summary>
        public int Tp { get; set; }

        /// <summary>
        /// Gets or sets the number of detections that match no label (false positives).
        /// </summary>
        public int Fp { get; set; }

        /// <summary>
        /// Gets or sets the number of labels that no detection matches (false negatives).
        /// </summary>
        public int Fn { get; set; }

        /// <summary>
        /// Gets or sets the number of labels whose area is sufficiently covered by masks.
        /// </summary>
        public int Protected { get; set; }

        /// <summary>
        /// Gets or sets the number of labels.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Gets the precision.
        /// </summary>
        /// <value>TP / (TP + FP), or 0 when there are no detections.</value>
        public double Precision => Tp + Fp > 0 ? (double)Tp / (Tp + Fp) : 0.0;

        /// <summary>
        /// Gets the recall.
        /// </summary>
        /// <value>TP / (TP + FN), or 0 when there are no labels.</value>
        public double Recall => Tp + Fn > 0 ? (double)Tp / (Tp + Fn) : 0.0;

        /// <summary>
        /// Gets the F1 score.
        /// </summary>
        /// <value>2 · P · R / (P + R), or 0 when both are 0.</value>
        public double F1 => Precision + Recall > 0 ? 2 * Precision * Recall / (Precision + Recall) : 0.0;

        /// <summary>
        /// Gets the protection rate.
        /// </summary>
        /// <value>Protected / Total, or 0 when there are no labels.</value>
        public double Protection => Total > 0 ? (double)Protected / Total : 0.0;

        /// <summary>
        /// Adds the counts of another instance to this one.
        /// </summary>
        /// <param name="other">The counts to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
        public void Add(KindStats other)
        {
            ArgumentNullException.ThrowIfNull(other);
            Tp += other.Tp;
            Fp += other.Fp;
            Fn += other.Fn;
            Protected += other.Protected;
            Total += other.Total;
        }
    }
}