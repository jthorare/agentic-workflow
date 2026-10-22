using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// The line's phase board (line spec §8.2, LN-WF1): one row per phase of the plan, in plan order. It is <c language="text">fact:phaseBoard</c>,
/// <c language="text">{ phases: [{ phase, entry, deps, state, attempt, headBranch, parked, parkReason, packet }] }</c>, and this type folds the line's events and the owner's edits
/// into it and picks the next phase to start. Pure: no clock, no I/O.
/// </summary>
internal sealed class PhaseBoard
{
    private readonly List<PhaseRow> _rows;

    private PhaseBoard(List<PhaseRow> rows)
    {
        _rows = rows;
    }

    /// <summary>The marker row for a phase that builds on the default branch.</summary>
    internal static PhaseRow Default { get; } = new() { Phase = string.Empty };

    /// <summary>The rows, in plan order.</summary>
    internal IReadOnlyList<PhaseRow> Rows => _rows;

    /// <summary>The empty board of a plan: every phase waiting, attempt 0 until it starts.</summary>
    /// <param name="phasePackets"><c language="text">fact:phasePackets</c>: <c language="text">[{ phase, id, hash, dir, deps? }]</c> in plan order.</param>
    /// <returns>The board.</returns>
    internal static PhaseBoard Of(JsonNode? phasePackets) => new([.. LineJson.Items(phasePackets).Select(static entry => new PhaseRow
    {
        Phase = LineJson.Str(entry, "phase") ?? string.Empty,
        Entry = entry is JsonObject obj ? (JsonObject)obj.DeepClone() : [],
        Deps = [.. LineJson.Strings(entry, "deps")],
    }).Where(static r => r.Phase.Length > 0)]);

    /// <summary>The board <c language="text">fact:phaseBoard</c> holds; null when it is not a board.</summary>
    /// <param name="board">The fact.</param>
    /// <returns>The board.</returns>
    internal static PhaseBoard? Read(JsonNode? board) => board is JsonObject { } obj && obj["phases"] is JsonArray phases
        ? new([.. phases.OfType<JsonObject>().Select(static p => new PhaseRow
        {
            Phase = LineJson.Str(p, "phase") ?? string.Empty,
            Entry = LineJson.Obj(p, "entry") is { } entry ? (JsonObject)entry.DeepClone() : [],
            Deps = [.. LineJson.Strings(p, "deps")],
            State = PhaseRow.StateOf(LineJson.Str(p, "state")),
            Attempt = LineJson.Int(p, "attempt") ?? 0,
            HeadBranch = LineJson.Str(p, "headBranch"),
            Parked = LineJson.Bool(p, "parked") ?? false,
            ParkReason = LineJson.Str(p, "parkReason"),
            Packet = LineJson.Str(p, "packet"),
        }).Where(static r => r.Phase.Length > 0)])
        : null;

    /// <summary>Marks <paramref name="next"/>'s phase running at its attempt: it is started, whatever event comes next.</summary>
    /// <param name="next">What <see cref="Decide"/> picked.</param>
    internal static void Start(NextPhase next)
    {
        ArgumentNullException.ThrowIfNull(next);
        (next.Row.State, next.Row.Attempt, next.Row.Parked, next.Row.ParkReason, next.Row.HeadBranch) = (PhaseState.Running, next.Attempt, false, null, null);
    }

    /// <summary>The board as the fact stores it.</summary>
    /// <returns>The JSON.</returns>
    internal JsonObject ToJson() => new() { ["phases"] = new JsonArray([.. _rows.Select(static r => (JsonNode?)r.ToJson())]) };

    /// <summary>
    /// Folds one <c language="text">line.phaseChanged</c> event (<c language="text">fact:phaseEvent</c>: <c language="text">{ phase, attempt, outcome, headBranch?, parkReason?, ended?, packet? }</c>, or a report's <c language="text">{ line, phase, status, instance, at }</c>, whose attempt is its instance address's)
    /// into the board. An event for a phase the plan lacks, for an attempt older than the row's, or for a settled phase changes nothing, and a state never moves back.
    /// </summary>
    /// <param name="phaseEvent">The event fact; an empty one, or anything with no phase, changes nothing.</param>
    internal void Fold(JsonNode? phaseEvent)
    {
        if (LineJson.Str(phaseEvent, "phase") is not { } phase || Find(phase) is not { } row || row.Settled)
        {
            return;
        }

        var attempt = LineJson.Int(phaseEvent, "attempt") ?? AttemptOf(LineJson.Str(phaseEvent, "instance")) ?? row.Attempt;
        if (attempt >= row.Attempt)
        {
            Apply(row, attempt, LineJson.Str(phaseEvent, "outcome") ?? LineJson.Str(phaseEvent, "status"), phaseEvent);
        }
    }

    /// <summary>
    /// Applies the owner's edit (<c language="text">fact:boardEdit</c>: <c language="text">{ edit: retry|skip, phase }</c>). A retry puts a failed phase back to waiting for its next
    /// attempt; a skip skips the phase and, transitively, every phase that depends on it.
    /// </summary>
    /// <param name="edit">The edit fact; an empty one, or anything else, changes nothing.</param>
    internal void Edit(JsonNode? edit)
    {
        if (LineJson.Str(edit, "phase") is not { } phase || Find(phase) is not { } row)
        {
            return;
        }

        var kind = LineJson.Str(edit, "edit");
        if (string.Equals(kind, "retry", StringComparison.Ordinal) && row.State == PhaseState.Failed)
        {
            (row.State, row.Parked, row.ParkReason, row.HeadBranch) = (PhaseState.Waiting, false, null, null);
        }
        else if (string.Equals(kind, "skip", StringComparison.Ordinal) && !row.Settled)
        {
            SkipWithDependents(row);
        }
    }

    /// <summary>What the dispatcher decides for this board (§8.2): done, blocked, full, or start with the phase to start.</summary>
    /// <param name="parallel">At most this many phases running at once (<c language="text">workflow.line.parallel</c>).</param>
    /// <param name="maxOpenPrs">At most this many phases shipped and unmerged (<c language="text">workflow.line.maxOpenPrs</c>).</param>
    /// <returns>The decision.</returns>
    internal PhaseDecision Decide(int parallel, int maxOpenPrs)
    {
        if (_rows.TrueForAll(static r => r.Settled))
        {
            return new PhaseDecision("done", Next: null);
        }

        if (_rows.Exists(static r => r.State == PhaseState.Failed))
        {
            return new PhaseDecision("blocked", Next: null);
        }

        var running = _rows.Count(static r => r.State == PhaseState.Running);
        var open = _rows.Count(static r => r.State == PhaseState.Shipped);
        if (running >= parallel || open >= maxOpenPrs)
        {
            return new PhaseDecision("full", Next: null);
        }

        foreach (var row in _rows.Where(static r => r.State == PhaseState.Waiting))
        {
            if (Ready(row) is { } stack)
            {
                return new PhaseDecision("start", new NextPhase(row, row.Attempt + 1, stack));
            }
        }

        // Nothing is ready: with a phase still running or shipped the line waits for its event; with none, no event will come.
        return new PhaseDecision(_rows.Exists(static r => r.State is PhaseState.Running or PhaseState.Shipped) ? "full" : "blocked", Next: null);
    }

    private static void Apply(PhaseRow row, int attempt, string? outcome, JsonNode? phaseEvent)
    {
        switch (outcome)
        {
            case "started" when attempt > row.Attempt:
                (row.State, row.Attempt, row.Parked, row.ParkReason, row.HeadBranch) = (PhaseState.Running, attempt, false, null, null);
                break;
            case "shipped" when row.State is PhaseState.Running or PhaseState.Waiting:
                (row.State, row.Attempt, row.HeadBranch) = (PhaseState.Shipped, attempt, LineJson.Str(phaseEvent, "headBranch") ?? row.HeadBranch);
                break;
            case "parked" when row.State is PhaseState.Running or PhaseState.Shipped:
                (row.Parked, row.ParkReason) = (true, LineJson.Str(phaseEvent, "parkReason"));
                break;
            case "resumed":
                (row.Parked, row.ParkReason) = (false, null);
                break;
            case "merged":
                row.State = PhaseState.Merged;
                break;
            case "ended":
                End(row, phaseEvent);
                break;
            case "failed":
                (row.Parked, row.ParkReason, row.State) = (false, null, PhaseState.Failed);
                break;
            default:
                break;
        }
    }

    /// <summary>A phase's run ended: resolved is a merged phase (the phase recipe's resolved end is its merge); anything else is a failed one, and keeps the newest packet hash for a retry.</summary>
    private static void End(PhaseRow row, JsonNode? phaseEvent)
    {
        row.Packet = LineJson.Str(phaseEvent, "packet") ?? row.Packet;
        row.Parked = false;
        row.State = string.Equals(LineJson.Str(phaseEvent, "ended"), "resolved", StringComparison.Ordinal) ? PhaseState.Merged : PhaseState.Failed;
    }

    /// <summary>The attempt a linked instance's address names, '{line}/phase.{id}.{attempt}'; null for anything else.</summary>
    private static int? AttemptOf(string? instance) =>
        instance is { } text && text.LastIndexOf('.') is >= 0 and var dot && int.TryParse(text.AsSpan(dot + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var attempt) && attempt >= 1 ? attempt : null;

    private void SkipWithDependents(PhaseRow row)
    {
        row.State = PhaseState.Skipped;
        foreach (var dependent in _rows.Where(r => !r.Settled && r.Deps.Contains(row.Phase, StringComparer.Ordinal)))
        {
            SkipWithDependents(dependent);
        }
    }

    /// <summary>
    /// Whether <paramref name="row"/> can start (§8.3): every dependency merged (built on the default branch), or exactly one left unmerged and it is shipped with its head branch known
    /// (stacked on it). Returns the phase it is stacked on, <see cref="Default"/> for the default branch, or null when it cannot start yet.
    /// </summary>
    private PhaseRow? Ready(PhaseRow row)
    {
        var dependencies = row.Deps.Select(Find).ToList();
        if (dependencies.Exists(static d => d is null))
        {
            return null;   // a dependency the plan does not have: never ready
        }

        var open = dependencies.OfType<PhaseRow>().Where(static d => d.State != PhaseState.Merged).ToList();
        return open switch
        {
            [] => Default,
            [{ State: PhaseState.Shipped, HeadBranch.Length: > 0 } parent] => parent,
            _ => null,
        };
    }

    private PhaseRow? Find(string phase) => _rows.Find(r => string.Equals(r.Phase, phase, StringComparison.Ordinal));
}
