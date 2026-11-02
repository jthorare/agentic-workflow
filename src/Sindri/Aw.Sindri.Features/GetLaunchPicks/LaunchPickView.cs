using System.Text.Json.Serialization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.GetLaunchPicks;

/// <summary>One run's model pick: how it was chosen (the 'model.picked' record), what the run spent and how it ended.</summary>
/// <param name="Launch">The launch.</param>
/// <param name="Item">The item.</param>
/// <param name="Instance">The workflow instance.</param>
/// <param name="State">The Step's state id.</param>
/// <param name="Attempt">The Step run's number.</param>
/// <param name="Escalation">0 for the first run, n for the n-th retry on a stronger model (the row's '.e1' suffix).</param>
/// <param name="Picking">True while only 'model.picking' exists for the launch: Jev is being asked.</param>
/// <param name="Pick">The 'model.picked' record, or null while <paramref name="Picking"/>.</param>
/// <param name="InputTokens">The input tokens of the run's last usage sample, cache reads included; 0 before any sample.</param>
/// <param name="OutputTokens">The output tokens of the run's last usage sample.</param>
/// <param name="CostUsd">The tokens priced with Budgets.PriceOf, or null when the model has no price (or none is known).</param>
/// <param name="Outcome">'refused', 'failed', 'passed' or 'running'; see LaunchPickOutcomes.</param>
/// <param name="Why">The plain sentence for the pick, from PickWhy.</param>
/// <param name="At">When the pick was recorded.</param>
public sealed record LaunchPickView(
    [property: JsonPropertyName("launchId")] LaunchId Launch,
    ItemId Item,
    InstanceAddress Instance,
    string State,
    int Attempt,
    int Escalation,
    bool Picking,
    ModelPicked? Pick,
    long InputTokens,
    long OutputTokens,
    decimal? CostUsd,
    string Outcome,
    string Why,
    DateTimeOffset At);
