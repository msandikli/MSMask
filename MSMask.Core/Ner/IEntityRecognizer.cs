namespace MSMask.Core.Ner
{
    /// <summary>
    /// Finds named entities (people, places, organizations) in texts. Implemented by the BERT model and by test fakes.
    /// </summary>
    public interface IEntityRecognizer
    {
        /// <summary>
        /// Finds the entities in several texts at once.
        /// </summary>
        /// <remarks>Processing many short texts together is much faster than one by one.</remarks>
        /// <param name="texts">The texts, typically the OCR lines of one image.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>One entity list per text, in the order of <paramref name="texts"/>.</returns>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
        Task<IReadOnlyList<IReadOnlyList<NerEntity>>> RecognizeAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default);
    }
}