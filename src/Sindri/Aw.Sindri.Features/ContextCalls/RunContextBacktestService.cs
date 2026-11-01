using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The work of a backtest job (context spec §9.1): runs the engine's backtest under the starting caller, passes its progress lines on to the job, and answers
/// the small result. The engine writes the full report to the eval file store and takes the box-wide heavy-job lock (SND-CONTEXT-040 when it stays busy).
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="engine">The context engine.</param>
/// <param name="jobs">The running job: its progress takes the engine's lines.</param>
internal sealed class RunContextBacktestService(ContextScopes scopes, IContextEngine engine, IJobContextAccessor jobs) : IService<RunContextBacktestRequest, RunContextBacktestResult>
{
    internal const int MaxDegraded = 20;
    private const int MaxNote = 240;

    public async ValueTask<Result<RunContextBacktestResult>> Handle(RunContextBacktestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var arms = Arms.Parse(request.Arms ?? "all");
        if (arms.IsError)
        {
            return arms.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var run = await engine.EvalBacktest(new BacktestRequest(scope.Value with { Job = jobs.Current?.Job.Value }, arms.Value, request.Since, request.Until, request.Limit, jobs.Current?.Progress), cancellationToken).ConfigureAwait(false);
        return run.IsError ? run.Error : Summarize(run.Value);
    }

    /// <summary>Counts, one line per arm, the pass line and the report path: the samples, scores and notes' full text stay in the report file.</summary>
    internal static RunContextBacktestResult Summarize(BacktestRun run)
    {
        var report = run.Report;
        return new RunContextBacktestResult(report.SampleCount, report.SmallSample, report.LeftOut.Total,
            [.. report.Arms.Select(static a => Line(a))],
            report.Overall, run.Path, [.. report.Degraded.Take(MaxDegraded).Select(static d => d.Length <= MaxNote ? d : d[..MaxNote])]);
    }

    private static BacktestArmLine Line(ArmReport arm) => new(arm.Arm, arm.NotAvailable, arm.PassLine?.Overall, arm.Leaky,
        arm.Score is { } s ? new BacktestArmScore(s.Samples, s.NodeRecall, s.NodePrecision, s.FileRecallAt20, s.LeakyShare, s.Tokens) : null)
    {
        PinAnchored = string.Equals(arm.Dimension, "ranker", StringComparison.Ordinal) && arm.Score is not null ? arm.PinAnchored ?? new PinAnchoredScore(0, 0, 0) : null,
    };
}
