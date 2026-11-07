namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// The measured quality and speed of a pipeline on a labeled data set.
    /// </summary>
    /// <param name="Engine">The pipeline name: the OCR engine, followed by "+ner" when NER is on.</param>
    /// <param name="Checksum">Whether check digits were validated.</param>
    /// <param name="Dataset">The path of the label file.</param>
    /// <param name="ImageCount">The number of evaluated images.</param>
    /// <param name="Kinds">The evaluated kinds, in report order.</param>
    /// <param name="IouThreshold">The minimum IoU for a detection to match a label.</param>
    /// <param name="CoverThreshold">The minimum masked fraction of a label area for the label to count as protected.</param>
    /// <param name="PerKind">The counts and metrics of each kind, in the order of <paramref name="Kinds"/>.</param>
    /// <param name="Micro">The counts and metrics of all kinds together (every label weighs the same).</param>
    /// <param name="MacroF1">The mean of the per-kind F1 scores (every kind weighs the same).</param>
    /// <param name="Latency">The processing time per image.</param>
    public record EvaluationReport(
        string Engine,
        bool Checksum,
        string Dataset,
        int ImageCount,
        IReadOnlyList<string> Kinds,
        double IouThreshold,
        double CoverThreshold,
        IReadOnlyDictionary<string, KindStats> PerKind,
        KindStats Micro,
        double MacroF1,
        LatencyStats Latency)
    {
        /// <summary>
        /// Gets a short name for the evaluated configuration.
        /// </summary>
        /// <value>For example <c>"tesseract"</c> or <c>"tesseract (no checksum)"</c>.</value>
        public string ConfigName => Checksum ? Engine : $"{Engine} (no checksum)";
    }
}