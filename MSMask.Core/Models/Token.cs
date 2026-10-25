using MSMask.Core.Models.SsMask.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace MSMask.Core.Models
{
    /// <summary>
    /// Represents a single word recognized by an OCR engine.
    /// </summary>
    /// <param name="Text">The recognized text of the word, without surrounding whitespace.</param>
    /// <param name="Box">The bounding box of the word in original image coordinates.</param>
    /// <param name="LineId">
    /// The index of the line the word belongs to, assigned by geometric line building.
    /// -1 until lines are built; OCR engine line numbers are not trusted.
    /// </param>
    /// <param name="Conf">The recognition confidence reported by the OCR engine, between 0 and 1.</param>
    public record Token(string Text, Box Box, int LineId = -1, double Conf = 1.0);
}
