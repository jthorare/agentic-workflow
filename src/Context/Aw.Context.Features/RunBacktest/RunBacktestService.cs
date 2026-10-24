using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Locks;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.RunBacktest;

/// <summary>
/// The backtest (spec §9.1, S-7A): the eligible samples (an issue and the merge that closed it), each cut at the last commit before the issue existed; the
/// sample's as-of map, built from git and cached; every requested arm run through the real planner; scoring against the files the merge changed; the §9.4 pass
/// line; and one report in files. A human or service job under one heavy-job slot. No model is called here (the adjudicator, when on, is a later pass).
/// </summary>
internal sealed class RunBacktestService(CorpusLoader corpus, SampleScorer scorer, IEvalStore store, IHeavyJobLock heavy, IScrubberFactory scrubberFactory, TimeProvider clock)
    : IService<BacktestRequest, BacktestRun>
{
    public async ValueTask<Result<BacktestRun>> Handle(BacktestRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = request.Scope;
        var settings = SettingsReader.Read(scope.Settings);
        var repos = settings.Bind(s => ScopeRules.Resolve(s, scope.Repos, scope.Caller));
        var scrubbers = settings.Bind(s => scrubberFactory.For(s.ExtraPatterns));
        if (repos.IsError || scrubbers.IsError || repos.Value.Count == 0)
        {
            return repos.IsError ? repos.Error : scrubbers.IsError ? scrubbers.Error
                : ContextErrors.SettingsInvalid.With("context.repos: the profile maps no repo to test against (fix: add a repo to the profile and approve it)");
        }

        var slot = await heavy.Take(request.Progress, cancellationToken).ConfigureAwait(false);
        if (slot.IsError)
        {
            return slot.Error;
        }

        await using (slot.Value.ConfigureAwait(false))
        {
            return await Run(request, new BriefScope(settings.Value, repos.Value, scrubbers.Value), cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<Result<BacktestRun>> Run(BacktestRequest request, BriefScope world, CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();
        var loaded = await corpus.Load(request, world, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        request.Progress?.Report(string.Create(CultureInfo.InvariantCulture, $"corpus: {loaded.Value.Samples.Count} samples"));
        var scored = await scorer.Score(request, world, loaded.Value, cancellationToken).ConfigureAwait(false);
        if (scored.IsError)
        {
            return scored.Error;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var left = loaded.Value.Corpus.LeftOut with { ExtractFailed = scored.Value.ExtractFailed, NoBrief = scored.Value.NoBrief, SourceDown = scored.Value.SourceDown };
        var report = ReportAssembly.Assemble(request, (started, left), scored.Value);
        var written = await store.WriteReport(EvalReportKind.Backtest, report, started, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new BacktestRun(report, written.Value);
    }
}
