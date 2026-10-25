using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Patterns
{
    /// <summary>
    /// Represents a range of characters within a string.
    /// </summary>
    /// <remarks>
    /// The range is half-open: <see cref="Start"/> is included and <see cref="End"/> is excluded,
    /// so the covered text is <c>text[Start..End]</c>. This matches C# ranges and Python slices.
    /// </remarks>
    /// <param name="Start">The index of the first character in the range.</param>
    /// <param name="End">The index just after the last character in the range.</param>
    public readonly record struct TextSpan(int Start, int End)
    {
        /// <summary>
        /// Gets the number of characters in the range.
        /// </summary>
        /// <value><c>End - Start</c>.</value>
        public int Length => End - Start;
    }
}
