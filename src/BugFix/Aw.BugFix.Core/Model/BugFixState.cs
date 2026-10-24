using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Ids;
using Aw.Kernel;

namespace Aw.BugFix.Core.Model;

/// <summary>A bug fix's whole state (state.json). Every change goes through a step that records a <see cref="HistoryEntry"/>.</summary>
/// <param name="SchemaVersion">The state file's schema version (<see cref="CurrentSchema"/>).</param>
/// <param name="Ticket">The ticket, verbatim.</param>
/// <param name="Phase">The current phase.</param>
/// <param name="Status">Where the fix stands overall.</param>
/// <param name="Attempt">The current attempt (0 before the first).</param>
/// <param name="Mode">The current attempt's mode.</param>
/// <param name="Investigation">The handoff snapshot, from advance investigate on.</param>
/// <param name="Check">The frozen check, from advance reproduce on.</param>
/// <param name="Baseline">The failing baseline run, from advance reproduce on.</param>
/// <param name="Candidates">Every candidate, in recording order.</param>
/// <param name="Runs">Every run record run-test and run-ui wrote.</param>
/// <param name="ResolvedBy">The candidate that resolved the ticket.</param>
/// <param name="History">Every step, oldest first.</param>
public sealed record BugFixState(
    int SchemaVersion,
    Ticket Ticket,
    Phase Phase,
    BugFixStatus Status,
    int Attempt,
    FixMode? Mode,
    Investigation? Investigation,
    FrozenCheck? Check,
    Baseline? Baseline,
    IReadOnlyList<Candidate> Candidates,
    IReadOnlyList<RegisteredRun> Runs,
    CandidateId? ResolvedBy,
    IReadOnlyList<HistoryEntry> History)
{
    /// <summary>The state file schema this build writes and reads.</summary>
    public const int CurrentSchema = 1;

    /// <summary>The most attempts a bug fix gets before it is reported unresolved.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Gets a value indicating whether the handoff confirmed a hypothesis; without one the first attempt is mode B.</summary>
    public bool Confirmed => Investigation?.Hypotheses.Any(static h => h.Result == HypothesisResult.Confirmed) ?? false;

    /// <summary>Gets the candidates of the current attempt.</summary>
    public IEnumerable<Candidate> CurrentCandidates => Candidates.Where(c => c.Attempt == Attempt);

    /// <summary>Gets a value indicating whether the bug fix is finished (resolved or unresolved).</summary>
    public bool Finished => Status is BugFixStatus.Resolved or BugFixStatus.Unresolved;

    /// <summary>A fresh state at intake.</summary>
    /// <param name="ticket">The ticket.</param>
    /// <param name="at">When init ran.</param>
    /// <param name="ticketFile">The ticket file's full path.</param>
    /// <returns>The state.</returns>
    public static BugFixState Start(Ticket ticket, DateTimeOffset at, string ticketFile) =>
        new(CurrentSchema, ticket, Phase.Intake, BugFixStatus.Active, 0, Mode: null, Investigation: null, Check: null, Baseline: null, [], [], ResolvedBy: null,
            [new HistoryEntry(at, "init", From: null, Phase.Intake, ticketFile)]);

    /// <summary>Refuses unless the state is in one of <paramref name="allowed"/>.</summary>
    /// <param name="allowed">The phases the step runs in.</param>
    /// <returns>Success, or AW-BUGFIX-005.</returns>
    public Result<Unit> Require(params Phase[] allowed) =>
        allowed.Contains(Phase)
            ? Results.Ok()
            : BugFixErrors.WrongPhase.With($"phase is {Names.Of(Phase)}; this step needs {string.Join(" or ", allowed.Select(Names.Of))}");

    /// <summary>The state after a step: <paramref name="to"/> as the phase, with the step in the history.</summary>
    /// <param name="to">The phase after the step.</param>
    /// <param name="command">The step.</param>
    /// <param name="at">When.</param>
    /// <param name="evidence">The file the step rests on.</param>
    /// <returns>The new state.</returns>
    public BugFixState Step(Phase to, string command, DateTimeOffset at, string? evidence = null) =>
        this with { Phase = to, History = [.. History, new HistoryEntry(at, command, Phase, to, evidence)] };

    /// <summary>Finds a candidate by id.</summary>
    /// <param name="id">The candidate id.</param>
    /// <returns>The candidate, or AW-BUGFIX-013.</returns>
    public Result<Candidate> Find(CandidateId id) =>
        Candidates.FirstOrDefault(c => c.Id == id) is { } found
            ? found
            : BugFixErrors.UnknownCandidate.With($"unknown candidate: {id.Value}");

    /// <summary>Gets the frozen check and baseline; only valid from phase reproduce on, which every caller requires first.</summary>
    public (FrozenCheck Check, Baseline Baseline) Frozen =>
        (Check!, Baseline!); // why: advance reproduce sets both together, and every phase after it (fix, evaluate) is checked by the caller before this is read.

    /// <summary>The state with one more registered run record.</summary>
    /// <param name="run">The run record's path and hash.</param>
    /// <returns>The new state.</returns>
    public BugFixState Register(RegisteredRun run) => this with { Runs = [.. Runs, run] };

    /// <summary>The state with <paramref name="candidate"/> replacing the candidate of the same id.</summary>
    /// <param name="candidate">The updated candidate.</param>
    /// <returns>The new state.</returns>
    public BugFixState With(Candidate candidate) =>
        this with { Candidates = [.. Candidates.Select(c => c.Id == candidate.Id ? candidate : c)] };
}
