namespace MSMask.Core.Ner
{
    /// <summary>
    /// One piece of text produced by <see cref="WordPieceTokenizer"/>.
    /// </summary>
    /// <param name="Id">The index of the piece in the vocabulary, which is what the model reads.</param>
    /// <param name="Text">The piece as it appears in the vocabulary, for example <c>"Ayşe"</c> or <c>"##lar"</c>.</param>
    /// <param name="Start">The index of the first character of the piece in the original text.</param>
    /// <param name="End">The index just after the last character of the piece in the original text.</param>
    public record WordPiece(int Id, string Text, int Start, int End);
}