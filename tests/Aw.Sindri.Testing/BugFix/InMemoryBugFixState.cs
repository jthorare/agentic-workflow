using System.Globalization;
using Aw.Sindri.Core.BugFix;

namespace Aw.Sindri.Testing.BugFix;

/// <summary>
/// The in-memory <see cref="IBugFixState"/>: the rules of bugfix-state the Steps rely on, over a state directory <see cref="Init"/> made. At most 3 attempts; a new attempt waits until every
/// candidate of the current one has a run; a commit is recorded once; a check run passes for a branch <see cref="Fixes"/> named; a verdict is asked once, and only after a passing run.
/// Refusals carry bugfix-state's codes.
/// </summary>
public sealed class InMemoryBugFixState : IBugFixState
{
    private const int Cap = 5;

    private readonly Dictionary<string, Bug> _bugs = new(StringComparer.Ordinal);
    private readonly HashSet<string> _fixing = new(StringComparer.Ordinal);
    private readonly List<string> _calls = [];
    private readonly List<CandidateSource> _sources = [];
    private JudgeDecision _decision = JudgeDecision.Resolved;

    /// <summary>The sources of the candidates asked to be recorded, in order.</summary>
    public IReadOnlyList<CandidateSource> Sources => _sources;

    /// <summary>Every call so far, as 'verb candidate-or-mode', in order.</summary>
    public IReadOnlyList<string> Calls => _calls;

    /// <summary>Makes a state directory a started bug fix.</summary>
    /// <param name="stateDirectory">The directory.</param>
    /// <returns>The same instance.</returns>
    public InMemoryBugFixState Init(string stateDirectory)
    {
        _bugs[stateDirectory] = new Bug();
        return this;
    }

    /// <summary>Makes the frozen check pass on a branch.</summary>
    /// <param name="branch">The candidate's branch.</param>
    /// <returns>The same instance.</returns>
    public InMemoryBugFixState Fixes(string branch)
    {
        _ = _fixing.Add(branch);
        return this;
    }

    /// <summary>Sets what judge decides.</summary>
    /// <param name="decision">The decision.</param>
    /// <returns>The same instance.</returns>
    public InMemoryBugFixState Judges(JudgeDecision decision)
    {
        _decision = decision;
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<BugFixAnswer<AttemptStarted>> StartAttempt(string stateDirectory, AttemptMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("start-attempt " + mode);
        if (Find(stateDirectory) is not { } bug)
        {
            return Answer(BugFixAnswers.Refused<AttemptStarted>(Missing));
        }

        if (bug.Current.FirstOrDefault(static c => !c.Ran) is { } unrun)
        {
            // why: bugfix-state stays in the fix phase until the candidate's run is recorded, and the step is not allowed there (AW-BUGFIX-005)
            return Answer(BugFixAnswers.Refused<AttemptStarted>(new BugFixRefusal("AW-BUGFIX-005", RefusalKind.Other, $"start-attempt is not allowed in the fix phase: candidate {unrun.Id} has no run yet")));
        }

        if (bug.Current.FirstOrDefault(static c => !c.Evaluated) is { } pending)
        {
            return Answer(BugFixAnswers.Refused<AttemptStarted>(new BugFixRefusal("AW-BUGFIX-010", RefusalKind.Other, $"candidate {pending.Id} has not been evaluated yet")));
        }

        if (bug.Attempt >= Cap)
        {
            return Answer(BugFixAnswers.Refused<AttemptStarted>(new BugFixRefusal("AW-BUGFIX-010", RefusalKind.AttemptCap, "attempt cap (5) reached; run advance report --unresolved")));
        }

        bug.Attempt++;
        return Answer(BugFixAnswers.Ok(new AttemptStarted(bug.Attempt, mode)));
    }

    /// <inheritdoc/>
    public ValueTask<BugFixAnswer<CandidateRecorded>> RecordCandidate(string stateDirectory, CandidateSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("record-candidate " + source.Branch);
        _sources.Add(source);
        if (Find(stateDirectory) is not { } bug)
        {
            return Answer(BugFixAnswers.Refused<CandidateRecorded>(Missing));
        }

        if (bug.Attempt == 0)
        {
            return Answer(BugFixAnswers.Refused<CandidateRecorded>(new BugFixRefusal("AW-BUGFIX-005", RefusalKind.Other, "start an attempt first")));
        }

        var commit = Commit(source.Branch);
        if (bug.Candidates.Find(c => c.Commit == commit) is { } duplicate)
        {
            return Answer(BugFixAnswers.Refused<CandidateRecorded>(new BugFixRefusal("AW-BUGFIX-011", RefusalKind.Other, $"commit {commit} is already candidate {duplicate.Id}")));
        }

        var candidate = new Candidate("c" + (bug.Candidates.Count + 1).ToString(CultureInfo.InvariantCulture), source.Branch, commit, bug.Attempt);
        bug.Candidates.Add(candidate);
        return Answer(BugFixAnswers.Ok(new CandidateRecorded(candidate.Id, commit, candidate.Attempt, ["src/app.txt"])));
    }

    /// <inheritdoc/>
    public ValueTask<BugFixAnswer<CheckRun>> Recheck(string stateDirectory, string candidate, string worktree, FrozenCheck check, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("recheck " + candidate);
        if (Find(stateDirectory) is not { } bug)
        {
            return Answer(BugFixAnswers.Refused<CheckRun>(Missing));
        }

        if (bug.Candidates.Find(c => c.Id == candidate) is not { } found)
        {
            return Answer(BugFixAnswers.Refused<CheckRun>(Unknown(candidate)));
        }

        found.Ran = true;
        found.Passed = _fixing.Contains(found.Branch);
        return Answer(BugFixAnswers.Ok(new CheckRun(candidate, found.Passed, found.Passed ? 0 : 1, $"{stateDirectory}/runs/{candidate}.json")));
    }

    /// <inheritdoc/>
    public ValueTask<BugFixAnswer<ResolutionVerdict>> Judge(string stateDirectory, string candidate, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add("judge " + candidate);
        if (Find(stateDirectory) is not { } bug)
        {
            return Answer(BugFixAnswers.Refused<ResolutionVerdict>(Missing));
        }

        if (bug.Candidates.Find(c => c.Id == candidate) is not { } found)
        {
            return Answer(BugFixAnswers.Refused<ResolutionVerdict>(Unknown(candidate)));
        }

        if (!found.Passed)
        {
            return Answer(BugFixAnswers.Refused<ResolutionVerdict>(new BugFixRefusal("AW-BUGFIX-015", RefusalKind.NoPassingRun, $"{candidate} has no passing run; judge only runs after the check passes")));
        }

        if (found.Judged)
        {
            return Answer(BugFixAnswers.Refused<ResolutionVerdict>(new BugFixRefusal("AW-BUGFIX-015", RefusalKind.Other, $"{candidate} was already judged; a verdict is never re-asked")));
        }

        found.Judged = true;
        return Answer(BugFixAnswers.Ok(new ResolutionVerdict(candidate, _decision, "fake-reason", ["the fake judged it"])));
    }

    private static BugFixRefusal Missing { get; } = new("AW-BUGFIX-002", RefusalKind.Other, "no bug fix in the state directory: run init first");

    private static BugFixRefusal Unknown(string candidate) => new("AW-BUGFIX-013", RefusalKind.Other, $"unknown candidate {candidate}");

    private static string Commit(string branch) => "commit-of-" + branch;

    private static ValueTask<BugFixAnswer<T>> Answer<T>(BugFixAnswer<T> answer) => ValueTask.FromResult(answer);

    private Bug? Find(string stateDirectory) => _bugs.GetValueOrDefault(stateDirectory);

    private sealed class Candidate(string id, string branch, string commit, int attempt)
    {
        public string Id { get; } = id;

        public string Branch { get; } = branch;

        public string Commit { get; } = commit;

        public int Attempt { get; } = attempt;

        public bool Ran { get; set; }

        public bool Passed { get; set; }

        public bool Judged { get; set; }

        public bool Evaluated => Ran && (!Passed || Judged);
    }

    private sealed class Bug
    {
        public int Attempt { get; set; }

        public List<Candidate> Candidates { get; } = [];

        public IEnumerable<Candidate> Current => Candidates.Where(c => c.Attempt == Attempt);
    }
}
