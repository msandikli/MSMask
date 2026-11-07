namespace MSMask.Core.Ner
{
    /// <summary>
    /// Joins the per-piece labels of a token classification model into whole entities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The model labels every piece in the BIO scheme: <c>B-PER</c> begins a person name, <c>I-PER</c> continues it,
    /// and <c>O</c> is outside any entity. For <c>"Ayşe Yılmaz ile"</c> the labels are <c>B-PER I-PER O</c>,
    /// which become one <c>PER</c> entity over <c>"Ayşe Yılmaz"</c>.
    /// </para>
    /// <para>
    /// The work happens in two steps:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Pieces → words.</b> A word is a piece followed by its <c>"##"</c> continuation pieces, and it takes the
    /// label and score of its first piece. This matters because many models label every piece of a word the same
    /// way: <c>Er</c> = <c>B-PER</c>, <c>##iş</c> = <c>B-PER</c>, <c>Aksu</c> = <c>I-PER</c>. Read piece by piece,
    /// the second <c>B-PER</c> would wrongly start a new name in the middle of <c>"Eriş"</c>.
    /// </description></item>
    /// <item><description>
    /// <b>Words → entities.</b> Neighbouring words of the same type form one entity; <c>O</c> words are dropped.
    /// The <c>B-</c> prefix does not split a group, because the model often starts a surname with <c>B-PER</c>
    /// too (<c>Ayşe</c> = <c>B-PER</c>, <c>Yılmaz</c> = <c>B-PER</c>), and two separate halves of a name would each
    /// cover only part of it. Two different people are almost always separated by a comma or a word such as
    /// <c>"ve"</c>, which ends the group. The score of an entity is the mean score of its words, and its range runs
    /// from the start of its first word to the end of its last.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class EntityAggregator
    {
        /// <summary>
        /// The label of pieces outside any entity.
        /// </summary>
        public const string Outside = "O";

        /// <summary>
        /// Groups piece predictions into entities.
        /// </summary>
        /// <param name="pieces">The predictions in text order, without the special pieces.</param>
        /// <returns>The entities in text order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pieces"/> is <see langword="null"/>.</exception>
        public static List<NerEntity> Aggregate(IReadOnlyList<TokenPrediction> pieces)
        {
            ArgumentNullException.ThrowIfNull(pieces);
            List<TokenPrediction> words = ToWords(pieces);

            var entities = new List<NerEntity>();
            var group = new List<TokenPrediction>();
            string groupType = "";
            foreach (TokenPrediction word in words)
            {
                string type = TypeOf(word.Label);
                if (group.Count > 0 && type != groupType)
                {
                    Flush(group, groupType, entities);
                }
                groupType = type;
                group.Add(word);
            }
            Flush(group, groupType, entities);
            return entities;
        }

        /// <summary>
        /// Joins every piece with its continuation pieces into one word that keeps the first piece's label and score.
        /// </summary>
        /// <param name="pieces">The piece predictions.</param>
        /// <returns>One prediction per word, covering all of the word's characters.</returns>
        private static List<TokenPrediction> ToWords(IReadOnlyList<TokenPrediction> pieces)
        {
            var words = new List<TokenPrediction>();
            foreach (TokenPrediction piece in pieces)
            {
                if (piece.IsSubword && words.Count > 0)
                {
                    words[^1] = words[^1] with { End = piece.End };
                }
                else
                {
                    words.Add(piece with { IsSubword = false });
                }
            }
            return words;
        }

        /// <summary>
        /// Removes the B-/I- prefix of a label.
        /// </summary>
        /// <param name="label">The label, for example <c>"B-PER"</c> or <c>"O"</c>.</param>
        /// <returns>The type: <c>"PER"</c> for <c>"B-PER"</c> and <c>"I-PER"</c>, <c>"O"</c> for <c>"O"</c>.</returns>
        private static string TypeOf(string label) =>
            label.StartsWith("B-", StringComparison.Ordinal) || label.StartsWith("I-", StringComparison.Ordinal)
                ? label[2..]
                : label;

        /// <summary>
        /// Turns the current group into an entity, unless it is a group of outside words, and empties it.
        /// </summary>
        /// <param name="group">The words of the group.</param>
        /// <param name="type">The type of the group.</param>
        /// <param name="entities">The list the entity is added to.</param>
        private static void Flush(List<TokenPrediction> group, string type, List<NerEntity> entities)
        {
            if (group.Count > 0 && type != Outside)
            {
                entities.Add(new NerEntity(type, group[0].Start, group[^1].End, group.Average(p => p.Score)));
            }
            group.Clear();
        }
    }
}