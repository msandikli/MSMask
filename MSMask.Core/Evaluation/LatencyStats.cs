namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// Summarizes how long the pipeline took per image.
    /// </summary>
    /// <param name="Mean">The mean total time in milliseconds.</param>
    /// <param name="Median">The median total time in milliseconds.</param>
    /// <param name="P95">The 95th percentile of the total time in milliseconds: 95% of the images were faster.</param>
    /// <param name="StagesMean">The mean time of each stage (<c>"ocr"</c>, <c>"lines"</c>, <c>"patterns"</c> ...) in milliseconds.</param>
    public record LatencyStats(double Mean, double Median, double P95, IReadOnlyDictionary<string, double> StagesMean)
    {
        /// <summary>
        /// Summarizes the stage timings of many images.
        /// </summary>
        /// <param name="timings">The <see cref="Models.PipelineResult.TimingsMs"/> of every image.</param>
        /// <returns>The summary; all zeros when <paramref name="timings"/> is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="timings"/> is <see langword="null"/>.</exception>
        public static LatencyStats From(IReadOnlyList<IReadOnlyDictionary<string, double>> timings)
        {
            ArgumentNullException.ThrowIfNull(timings);
            List<double> totals = timings.Select(t => t.GetValueOrDefault("total")).ToList();
            var stages = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (string key in timings.SelectMany(t => t.Keys).Where(k => k != "total").Distinct())
            {
                stages[key] = timings.Average(t => t.GetValueOrDefault(key));
            }
            return new LatencyStats(
                totals.Count > 0 ? totals.Average() : 0.0,
                Percentile(totals, 50),
                Percentile(totals, 95),
                stages);
        }

        /// <summary>
        /// Computes a percentile with linear interpolation between the two nearest values.
        /// </summary>
        /// <remarks>
        /// The values are sorted and the percentile <c>q</c> is read at position <c>q / 100 · (n − 1)</c>.
        /// The 50th percentile is the median.
        /// </remarks>
        /// <param name="values">The values.</param>
        /// <param name="q">The percentile, from 0 to 100.</param>
        /// <returns>The percentile, or 0 when <paramref name="values"/> is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="q"/> is outside 0–100.</exception>
        public static double Percentile(IEnumerable<double> values, double q)
        {
            ArgumentNullException.ThrowIfNull(values);
            ArgumentOutOfRangeException.ThrowIfLessThan(q, 0);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(q, 100);

            List<double> sorted = values.Order().ToList();
            if (sorted.Count == 0)
            {
                return 0.0;
            }
            double position = q / 100.0 * (sorted.Count - 1);
            int lower = (int)Math.Floor(position);
            int upper = Math.Min(lower + 1, sorted.Count - 1);
            return sorted[lower] + (sorted[upper] - sorted[lower]) * (position - lower);
        }
    }
}