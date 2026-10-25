using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Patterns
{
    /// <summary>
    /// Represents a piece of sensitive data that a pattern rule found in a line of text.
    /// </summary>
    /// <param name="Kind">The kind of sensitive data; one of the constants in <see cref="Models.Kinds"/>.</param>
    /// <param name="Start">The index of the first matched character in the line.</param>
    /// <param name="End">The index just after the last matched character in the line.</param>
    /// <param name="Text">The matched text, equal to <c>line[Start..End]</c>.</param>
    /// <param name="Priority">
    /// The priority of the rule that produced the match. Lower values win when two matches overlap.
    /// </param>
    /// <param name="Rule">The name of the rule that produced the match, for example <c>"iban_tr"</c>. Used in error analysis.</param>
    public record PatternMatch(string Kind, int Start, int End, string Text, int Priority, string Rule)
    {
        /// <summary>
        /// Gets the number of matched characters.
        /// </summary>
        /// <value><c>End - Start</c>.</value>
        public int Length => End - Start;
    }
}
