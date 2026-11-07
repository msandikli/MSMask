namespace MSMask.Core.Evaluation
{
    /// <summary>
    /// The report and the error list produced by an evaluation run.
    /// </summary>
    /// <param name="Report">The metrics.</param>
    /// <param name="Errors">Every missed label, unprotected label and extra detection, for error analysis.</param>
    public record EvaluationOutcome(EvaluationReport Report, IReadOnlyList<EvaluationError> Errors);
}