using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The one state chip of the screens fixture ("State chips: one mapping of word to colour"): a word and the colour class it takes.</summary>
/// <param name="Word">The chip word, upper case.</param>
/// <param name="Tone">The colour class.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateChip(string Word, ChipTone Tone)
{
    /// <summary>An instance parked: it waits for an answer.</summary>
    public const string Parked = "PARKED";

    /// <summary>An instance escalated: a human decides.</summary>
    public const string Escalated = "ESCALATED";

    /// <summary>An instance a person took over.</summary>
    public const string WithYou = "WITH YOU";

    /// <summary>An instance whose desk answer is open.</summary>
    public const string Decision = "DECISION";

    /// <summary>An instance that has not started.</summary>
    public const string Queued = "QUEUED";

    /// <summary>An instance that finished well (any outcome but the three below, 'resolved' and 'alreadySatisfied' among them).</summary>
    public const string Done = "DONE";

    /// <summary>An instance that ended with outcome 'failed'.</summary>
    public const string Failed = "FAILED";

    /// <summary>An instance that ended with outcome 'unresolved': it stopped without an answer, and a person may want to look.</summary>
    public const string DidntWorkOut = "DIDN'T WORK OUT";

    /// <summary>An instance that ended with outcome 'cancelled' (the engine's cancel).</summary>
    public const string Cancelled = "CANCELLED";

    /// <summary>An instance that is running.</summary>
    public const string Running = "RUNNING";

    private static readonly HashSet<string> _finished = new([Done, Failed, DidntWorkOut, Cancelled], StringComparer.Ordinal);

    /// <summary>
    /// The chip of an instance: an engine-owned badge decides first, then an open desk answer, then not started, then the terminal
    /// outcome (failed FAILED/fail, unresolved DIDN'T WORK OUT/neutral, cancelled CANCELLED/neutral, any other DONE/neutral; the owner's table, 2026-10-19), then running.
    /// </summary>
    /// <param name="engineOwned">The engine-owned badge, if any; a taken-over instance arrives here as the "taken-over" badge (the plan's "takenOver" flag).</param>
    /// <param name="deskOpen">Whether a desk answer is open.</param>
    /// <param name="outcome">The terminal outcome, or null while the instance has not ended.</param>
    /// <param name="notStarted">Whether the instance hasn't started: it sits at its recipe's first state, waiting for a slot.</param>
    /// <returns>The chip.</returns>
    public static StateChip For(EngineOwnedBadge? engineOwned, bool deskOpen, OutcomeName? outcome, bool notStarted = false) => engineOwned?.Kind switch
    {
        EngineOwnedKind.Parked => new StateChip(Parked, ChipTone.Attn),
        EngineOwnedKind.Escalated => new StateChip(Escalated, ChipTone.Attn),
        EngineOwnedKind.TakenOver => new StateChip(WithYou, ChipTone.Neutral),
        _ when deskOpen => new StateChip(Decision, ChipTone.Attn),
        _ when notStarted => new StateChip(Queued, ChipTone.Neutral),
        _ when outcome is { } ended => Ended(ended),
        _ => new StateChip(Running, ChipTone.Ok),
    };

    /// <summary>Whether <paramref name="word"/> is the chip of an instance that has ended, whatever its outcome.</summary>
    /// <param name="word">The chip word.</param>
    /// <returns>True for DONE, FAILED, DIDN'T WORK OUT and CANCELLED.</returns>
    public static bool IsFinished(string word) => word is not null && _finished.Contains(word);

    private static StateChip Ended(OutcomeName outcome) => outcome.Value switch
    {
        "failed" => new StateChip(Failed, ChipTone.Fail),
        "unresolved" => new StateChip(DidntWorkOut, ChipTone.Neutral),
        "cancelled" => new StateChip(Cancelled, ChipTone.Neutral),
        _ => new StateChip(Done, ChipTone.Neutral),
    };
}
