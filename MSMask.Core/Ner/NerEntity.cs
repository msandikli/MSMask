namespace MSMask.Core.Ner
{
    /// <summary>
    /// A named entity found in a text by a model, before it is turned into a <see cref="Models.Detection"/>.
    /// </summary>
    /// <param name="Label">The entity type without the B-/I- prefix, for example <c>"PER"</c>, <c>"LOC"</c> or <c>"ORG"</c>.</param>
    /// <param name="Start">The index of the first character of the entity in the text.</param>
    /// <param name="End">The index just after the last character of the entity in the text.</param>
    /// <param name="Score">The mean probability of the entity's pieces, from 0 to 1.</param>
    public record NerEntity(string Label, int Start, int End, double Score);
}