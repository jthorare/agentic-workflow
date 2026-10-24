using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.BugFix;

namespace Aw.Sindri.Tests.BugFix;

/// <summary>The real bugfix-state, through the adapter, honours the port's contract the in-memory fake does; with AW_JUDGE_BIN naming no file its judge is refused as unavailable, which the last test pins (a judge on PATH is never asked).</summary>
[Lane(TestLane.Integration)]
public sealed class BugFixStateToolContractTests : BugFixStateContract, IDisposable
{
    private readonly RealBugFixState _real = new();

    protected override string StateDirectory => _real.State;

    protected override string EmptyDirectory => _real.Empty;

    protected override FrozenCheck Check => RealBugFixState.FrozenCheck;

    public void Dispose() => _real.Dispose();

    protected override IBugFixState Create() => _real.Tool();

    protected override CandidateSource Commit(bool fixes) => _real.Commit(fixes);

    [Fact]
    public async Task Judge_is_refused_as_unavailable_when_AW_JUDGE_BIN_names_no_file()
    {
        var port = Create();
        var source = Commit(fixes: true);
        _ = await port.StartAttempt(StateDirectory, AttemptMode.Whole, TestContext.Current.CancellationToken);
        _ = await port.RecordCandidate(StateDirectory, source, TestContext.Current.CancellationToken);
        _ = await port.Recheck(StateDirectory, "c1", source.Worktree, Check, TestContext.Current.CancellationToken);

        var verdict = await port.Judge(StateDirectory, "c1", TestContext.Current.CancellationToken);

        Expect.Equal(BugFixAnswerStatus.Refused, verdict.Status);
        Expect.Equal("AW-BUGFIX-016", verdict.Refusal!.Code);
    }
}
