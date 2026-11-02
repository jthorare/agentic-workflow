namespace Aw.Sindri.Ui.Api;

/// <summary>One run's model pick, as the app draws it (the Runs table; M-8).</summary>
/// <param name="LaunchId">The launch id (LCH-…).</param>
/// <param name="Item">The item id.</param>
/// <param name="Instance">The workflow instance address.</param>
/// <param name="State">The Step's state id.</param>
/// <param name="Attempt">The Step run's number.</param>
/// <param name="Escalation">0 for the first run, n for the n-th retry on a stronger model.</param>
/// <param name="Picking">True while Jev is being asked ('model.picking' and no 'model.picked' yet).</param>
/// <param name="Pick">How the model was chosen; null while <paramref name="Picking"/>.</param>
/// <param name="InputTokens">The run's input tokens so far, cache reads included.</param>
/// <param name="OutputTokens">The run's output tokens so far.</param>
/// <param name="CostUsd">The tokens priced, or null when the model has no price ('cost unknown').</param>
/// <param name="Outcome">'refused', 'failed', 'passed' or 'running'.</param>
/// <param name="Why">The plain sentence for the pick.</param>
/// <param name="At">When the pick was recorded.</param>
internal sealed record LaunchPickRow(
    string LaunchId, string Item, string Instance, string State, int Attempt, int Escalation, bool Picking, PickedView? Pick,
    long InputTokens, long OutputTokens, decimal? CostUsd, string Outcome, string Why, DateTimeOffset At);
