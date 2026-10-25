namespace MSMask.Core.Masking
{
    /// <summary>
    /// Represents a rectangle of whole pixels.
    /// </summary>
    /// <remarks>
    /// The rectangle is half-open like <see cref="Patterns.TextSpan"/>: it covers the columns
    /// <see cref="X0"/> to <c>X1 - 1</c> and the rows <see cref="Y0"/> to <c>Y1 - 1</c>.
    /// </remarks>
    /// <param name="X0">The first column.</param>
    /// <param name="Y0">The first row.</param>
    /// <param name="X1">The column just after the last one.</param>
    /// <param name="Y1">The row just after the last one.</param>
    public readonly record struct PixelRect(int X0, int Y0, int X1, int Y1)
    {
        /// <summary>
        /// Gets the number of columns.
        /// </summary>
        /// <value><c>X1 - X0</c>, or 0 when the rectangle is inverted.</value>
        public int Width => Math.Max(0, X1 - X0);

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        /// <value><c>Y1 - Y0</c>, or 0 when the rectangle is inverted.</value>
        public int Height => Math.Max(0, Y1 - Y0);

        /// <summary>
        /// Gets a value indicating whether the rectangle covers no pixels.
        /// </summary>
        /// <value><see langword="true"/> when <see cref="Width"/> or <see cref="Height"/> is 0.</value>
        public bool IsEmpty => Width == 0 || Height == 0;
    }
}
