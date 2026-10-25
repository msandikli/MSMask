using MSMask.Core.Models;

namespace MSMask.Core.Masking
{
    /// <summary>
    /// Decides exactly which pixels are painted for each detection.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Text boxes get a margin before painting: <see cref="PadX"/> times the box height on the left and
    /// right, and <see cref="PadY"/> times the box height on the top and bottom. The horizontal margin is
    /// larger because a match inside a word is cropped by character count (see
    /// <see cref="Lines.TextLine.SpanBox"/>), and proportional fonts make that crop a little inaccurate.
    /// </para>
    /// <para>
    /// The painting code and the evaluation code both use <see cref="Region"/>, so the protection metric
    /// measures exactly the pixels that are painted.
    /// </para>
    /// </remarks>
    public static class MaskGeometry
    {
        /// <summary>
        /// The horizontal margin added to text boxes, as a fraction of the box height.
        /// </summary>
        public const double PadX = 0.4;

        /// <summary>
        /// The vertical margin added to text boxes, as a fraction of the box height.
        /// </summary>
        public const double PadY = 0.15;

        /// <summary>
        /// Computes the whole-pixel rectangle that is painted for a detection.
        /// </summary>
        /// <remarks>
        /// The margin is added first, then the box is clipped to the image, and finally its edges are rounded
        /// outwards (left and top down, right and bottom up) so that partly covered pixels are painted too.
        /// </remarks>
        /// <param name="detection">The detection to paint.</param>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <returns>The rectangle to paint; empty when the box lies completely outside the image.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detection"/> is <see langword="null"/>.</exception>
        public static PixelRect Region(Detection detection, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(detection);
            Box box = detection.Box;
            if (Kinds.TextKinds.Contains(detection.Kind))
            {
                double h = box.Height;
                box = box.Expand(h * PadX, h * PadY);
            }
            box = box.Clip(width, height);

            int x0 = (int)Math.Floor(box.X0);
            int y0 = (int)Math.Floor(box.Y0);
            int x1 = (int)Math.Ceiling(box.X1);
            int y1 = (int)Math.Ceiling(box.Y1);
            return new PixelRect(x0, y0, Math.Max(x0, x1), Math.Max(y0, y1));
        }

        /// <summary>
        /// Builds a map of the pixels that are painted for a set of detections.
        /// </summary>
        /// <param name="width">The image width in pixels.</param>
        /// <param name="height">The image height in pixels.</param>
        /// <param name="detections">The detections to paint.</param>
        /// <returns>
        /// A <c>[height, width]</c> array indexed as <c>[y, x]</c>; <see langword="true"/> marks a painted pixel.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="detections"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="width"/> or <paramref name="height"/> is negative.</exception>
        public static bool[,] PixelMap(int width, int height, IEnumerable<Detection> detections)
        {
            ArgumentNullException.ThrowIfNull(detections);
            ArgumentOutOfRangeException.ThrowIfNegative(width);
            ArgumentOutOfRangeException.ThrowIfNegative(height);

            var map = new bool[height, width];
            foreach (Detection detection in detections)
            {
                PixelRect r = Region(detection, width, height);
                for (int y = r.Y0; y < r.Y1; y++)
                {
                    for (int x = r.X0; x < r.X1; x++)
                    {
                        map[y, x] = true;
                    }
                }
            }
            return map;
        }
    }
}
