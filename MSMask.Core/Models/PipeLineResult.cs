namespace MSMask.Core.Models
{
    /// <summary>
    /// Holds the outcome of analyzing a single image.
    /// </summary>
    public class PipelineResult
    {
        /// <summary>
        /// Gets the sensitive data found in the image.
        /// </summary>
        public List<Detection> Detections { get; } = [];

        /// <summary>
        /// Gets the duration of each pipeline stage in milliseconds.
        /// </summary>
        /// <remarks>
        /// Keys are stage names such as <c>"ocr"</c>, <c>"lines"</c>, <caaa>"patterns"</c>, <c>"ner"</c>,
        /// <c>"face"</c>, <c>"mask"</c> and <c>"total"</c>.
        /// </remarks>
        public Dictionary<string, double> TimingsMs { get; } = [];

        /// <summary>
        /// Gets every word recognized by OCR, with line ids assigned.
        /// </summary>
        /// <remarks>Kept for error analysis.</remarks>
        public List<Token> Tokens { get; } = [];

        /// <summary>
        /// Counts the detections of each kind.
        /// </summary>
        /// <returns>
        /// A dictionary that maps each detected kind to its number of detections,
        /// for example <c>{"TCKN": 1, "TELEFON": 2}</c>. Kinds with no detections are omitted.
        /// </returns>
        public Dictionary<string, int> KindCounts() =>
            Detections.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Count());
    }
}
