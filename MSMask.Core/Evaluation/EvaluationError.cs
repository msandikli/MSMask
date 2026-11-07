namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Describes one mistake found during evaluation, for error analysis.
    /// </summary>
    /// <param name="File">The image the mistake was found in.</param>
    /// <param name="Type">
    /// <see cref="Missed"/> for a label that no detection matched, <see cref="Unprotected"/> for a label whose area is
    /// not covered well enough by masks, or <see cref="Extra"/> for a detection that matches no label.
    /// </param>
    /// <param name="Kind">The kind of the label or detection.</param>
    /// <param name="Text">The text of the label or detection.</param>
    /// <param name="Coverage">For <see cref="Unprotected"/>: the fraction of the label area that is masked.</param>
    /// <param name="Matched">For <see cref="Unprotected"/>: whether a detection matched the label anyway.</param>
    /// <param name="Ocr">For labels: the OCR lines near the label, which usually show why it was missed.</param>
    /// <param name="Box">For <see cref="Extra"/>: the detection box as <c>[x0, y0, x1, y1]</c>.</param>
    public record EvaluationError(
        string File,
        string Type,
        string Kind,
        string Text,
        double? Coverage = null,
        bool? Matched = null,
        IReadOnlyList<string>? Ocr = null,
        double[]? Box = null)
    {
        /// <summary>
        /// The <see cref="Type"/> of a label that no detection matched (a false negative).
        /// </summary>
        public const string Missed = "fn";

        /// <summary>
        /// The <see cref="Type"/> of a detection that matches no label (a false positive).
        /// </summary>
        public const string Extra = "fp";

        /// <summary>
        /// The <see cref="Type"/> of a label whose area is not covered well enough by masks.
        /// </summary>
        public const string Unprotected = "unprotected";
    }
}