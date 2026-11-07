using MSMask.Core.Ner;

namespace MSMask.Core.Tests.Fakes
{
    /// <summary>
    /// An entity recognizer for tests: it finds fixed phrases instead of running a model.
    /// </summary>
    /// <remarks>
    /// Every occurrence of a registered phrase in a text is reported with the registered label and score, so a test
    /// decides exactly what the "model" sees and can check the rest of the NER code in isolation.
    /// </remarks>
    public sealed class FakeEntityRecognizer : IEntityRecognizer
    {
        /// <summary>
        /// The phrases to find, with their label and score.
        /// </summary>
        private readonly List<(string Phrase, string Label, double Score)> _phrases = [];

        /// <summary>
        /// Gets the texts of every call, in order.
        /// </summary>
        public List<IReadOnlyList<string>> Calls { get; } = [];

        /// <summary>
        /// Registers a phrase to report.
        /// </summary>
        /// <param name="phrase">The exact text of the entity.</param>
        /// <param name="label">The entity type, for example <c>"PER"</c>.</param>
        /// <param name="score">The score to report.</param>
        /// <returns>This instance, so that calls can be chained.</returns>
        public FakeEntityRecognizer Add(string phrase, string label, double score = 0.99)
        {
            _phrases.Add((phrase, label, score));
            return this;
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<IReadOnlyList<NerEntity>>> RecognizeAsync(IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(texts);
            var result = new List<IReadOnlyList<NerEntity>>();
            foreach (string text in texts)
            {
                var entities = new List<NerEntity>();
                foreach ((string phrase, string label, double score) in _phrases)
                {
                    for (int i = text.IndexOf(phrase, StringComparison.Ordinal); i >= 0;
                         i = text.IndexOf(phrase, i + 1, StringComparison.Ordinal))
                    {
                        entities.Add(new NerEntity(label, i, i + phrase.Length, score));
                    }
                }
                result.Add(entities.OrderBy(e => e.Start).ToList());
            }
            return Task.FromResult<IReadOnlyList<IReadOnlyList<NerEntity>>>(result);
        }
    }
}