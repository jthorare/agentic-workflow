using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;

namespace Aw.Sindri.Core.Events;

/// <summary>
/// An event's post-state delta as the ledger row shows it (spec §5.6): each part the decision changed, as state names,
/// reasons, counters and digests. A null part didn't change, except <see cref="Suspension"/> and <see cref="PendingPark"/>,
/// which are always their post-state values. No question text and no actor's words; only the started row's subject
/// (the subject as named at start: a text subject or a rendered name cut to 40/80, a link or payload subject's key and label
/// as stored) carries text taken from facts.
/// </summary>
/// <param name="Frame">The frame after it.</param>
/// <param name="Suspension">The suspension after it; none when there is none.</param>
/// <param name="PendingPark">The pending park after it; none when there is none.</param>
/// <param name="Loops">Every loop's round and allowance after it.</param>
/// <param name="TokensSpent">The tokens spent after it.</param>
/// <param name="TokensGranted">The tokens granted after it.</param>
/// <param name="Facts">The facts it added or replaced: key to the content's SHA-256.</param>
/// <param name="Outcome">The terminal outcome, when it ended the instance.</param>
/// <param name="Subject">The subject as named at the start (kind, key and the rendered name as label); only workflow.started carries it.</param>
/// <param name="Producers">Each fact in <paramref name="Facts"/> to who made it, 'engine:generate' for an input the start generated; only workflow.started carries it.</param>
public sealed record WorkflowEventDelta(
    FrameSummary? Frame,
    SuspensionSummary? Suspension,
    SuspensionSummary? PendingPark,
    IReadOnlyDictionary<string, LoopCounter>? Loops,
    int? TokensSpent,
    int? TokensGranted,
    IReadOnlyDictionary<string, string>? Facts,
    string? Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EventSubject? Subject = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Producers = null)
{
    /// <summary>The recipe's id; only workflow.started carries it, so the recipe stats can tell whose run a row belongs to. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Recipe { get; init; }

    /// <summary>The recipe's hash; only workflow.started carries it. Not written when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RecipeHash { get; init; }

    /// <summary>The public projection of <paramref name="delta"/>.</summary>
    /// <param name="delta">The decision's delta.</param>
    /// <returns>The delta the row carries.</returns>
    public static WorkflowEventDelta Of(InstanceDelta delta)
    {
        ArgumentNullException.ThrowIfNull(delta);
        return new WorkflowEventDelta(
            delta.Frame is { } f ? new FrameSummary(f.State, f.Entry, f.Run?.Token.Value, f.Child?.Value, f.Run?.Runtime) : null,
            Summary(delta.Suspension),
            Summary(delta.PendingPark),
            delta.Loops?.ToDictionary(l => l.Key, l => new LoopCounter(l.Value.Round, l.Value.Allowed), StringComparer.Ordinal),
            delta.Spent?.Total,
            delta.Spent?.Granted,
            delta.Facts?.ToDictionary(f => f.Key.Value, f => f.Value.Sha256, StringComparer.Ordinal),
            delta.Outcome?.Value);
    }

    /// <summary>A suspension as the delta shows it.</summary>
    /// <param name="s">The suspension, or none.</param>
    /// <returns>The summary, or none.</returns>
    internal static SuspensionSummary? Summary(Suspension? s) =>
        s is null ? null : new SuspensionSummary(s.Kind, s.Reason, s.ParkKind, s.Escalation, s.Mirrors?.Value);
}
