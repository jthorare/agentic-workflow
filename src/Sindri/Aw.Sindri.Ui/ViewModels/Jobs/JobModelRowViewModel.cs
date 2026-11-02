namespace Aw.Sindri.Ui.ViewModels.Jobs;

/// <summary>
/// One try of a model call on the job sheet's "Model calls" list (context model routing spec §8.2): when, the kind, the harness and model, what it cost or why it didn't answer, and how it ended. A Jev pick reads as one
/// sentence in <see cref="Who"/> and leaves the rest empty. The accessible name reads the parts in that order.
/// </summary>
/// <param name="Time">The time (with its date when it isn't today).</param>
/// <param name="Kind">The profile entry the call used ('labeling', ...); empty for a Jev pick.</param>
/// <param name="Who">The harness and model, or the Jev sentence.</param>
/// <param name="Detail">Tokens for an answer, the reason for a failed or skipped try; empty otherwise.</param>
/// <param name="Outcome">'answered', 'limit', 'failed', 'skipped' or 'not asked'; empty for a Jev pick.</param>
internal sealed record JobModelRowViewModel(string Time, string Kind, string Who, string Detail, string Outcome)
{
    /// <summary>The row's accessible name: time, kind, harness and model, tokens or reason, outcome.</summary>
    public string Name => string.Join(", ", new[] { Time, Kind, Who, Detail, Outcome }.Where(static part => part.Length > 0));
}
