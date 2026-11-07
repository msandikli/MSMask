namespace MSMask.Core.Ner
{
    /// <summary>
    /// The model's decision for one text piece: its most likely label and that label's probability.
    /// </summary>
    /// <param name="Label">The full label, for example <c>"B-PER"</c>, <c>"I-LOC"</c> or <c>"O"</c>.</param>
    /// <param name="Score">The probability of <paramref name="Label"/>, from 0 to 1.</param>
    /// <param name="Start">The index of the first character of the piece in the text.</param>
    /// <param name="End">The index just after the last character of the piece in the text.</param>
    /// <param name="IsSubword">
    /// <see langword="true"/> when the piece continues the previous word (a <c>"##"</c> piece);
    /// <see langword="false"/> when it starts a new word.
    /// </param>
    public record TokenPrediction(string Label, double Score, int Start, int End, bool IsSubword = false);
}