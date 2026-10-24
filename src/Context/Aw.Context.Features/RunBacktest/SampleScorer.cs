using System.Globalization;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;

using Aw.Kernel;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Scores every sample on every running arm (spec §9.1): the brief, the as-of map and docs at the cut, then each arm through the planner.</summary>
internal sealed class SampleScorer(IBriefSource briefs, AsOfMapBuilder maps, IAsOfDocs docs, ArmRunner arms, IAdjudicator adjudicator)
{
    /// <summary>Scores the window.</summary>
    /// <param name="request">The request (arms, scope, progress).</param>
    /// <param name="world">The call's settings, repos and scrubbers.</param>
    /// <param name="loaded">The corpus.</param>
    /// <param name="cancellationToken">Cancels the run; the exception propagates and no report is written.</param>
    /// <returns>The scores; SND-CONTEXT-080 with the drop counts when every sample was dropped; or the error of a failing store, cache or context.db, which stops the run with its own code.</returns>
    public async ValueTask<Result<Collected>> Score(BacktestRequest request, BriefScope world, LoadedCorpus loaded, CancellationToken cancellationToken)
    {
        var running = request.Arms.Where(a => Arms.PendingStage(a) is null).ToList();
        var kept = new List<CorpusSample>();
        var scores = running.ToDictionary(a => a, _ => new List<SampleScore>());
        var dropped = new Dictionary<Outcome, int>();
        var spend = new Spend(world.Settings.Adjudicator);
        var timedOut = new List<string>();
        for (var i = 0; i < loaded.Samples.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sample = loaded.Samples[i];
            var step = string.Create(CultureInfo.InvariantCulture, $"sample {i + 1}/{loaded.Samples.Count} {sample.IssueKey}");
            var outcome = await One(request, (world, loaded.Issues[sample.IssueKey], sample), (running, scores, step), (spend, timedOut), cancellationToken).ConfigureAwait(false);
            if (outcome.IsError)
            {
                return outcome.Error;   // a refused store (005) stops the run: every other sample would fail the same way
            }

            if (outcome.Value is Outcome.Scored or Outcome.ScoredWithoutClusters)
            {
                kept.Add(sample);
            }

            dropped[outcome.Value] = dropped.GetValueOrDefault(outcome.Value) + 1;
        }

        return kept.Count == 0
            ? NothingScored(dropped)
            : new Collected(kept, scores, dropped.GetValueOrDefault(Outcome.CutUnreadable), dropped.GetValueOrDefault(Outcome.NoBrief))
            {
                Tokens = spend.ByArm,
                AdjudicationCapped = spend.Capped,
                AdjudicationError = spend.Error,
                RecognizerTimeouts = timedOut.Count,
                SourceDown = dropped.GetValueOrDefault(Outcome.SourceDown),
                CappedTeams = loaded.CappedTeams,
                WithoutClusters = running.Exists(static a => a != Arm.Docs) ? dropped.GetValueOrDefault(Outcome.ScoredWithoutClusters) : 0,   // the docs arm reads no map
            };
    }

    // Every sample dropped: no report of nothing, the counts of why.
    private static Error NothingScored(Dictionary<Outcome, int> dropped) =>
        ContextErrors.NoEligibleSample.With(string.Create(CultureInfo.InvariantCulture,
            $"no backtest sample could be scored: {dropped.GetValueOrDefault(Outcome.SourceDown)} dropped for an unavailable brief source (SND-CONTEXT-011), {dropped.GetValueOrDefault(Outcome.NoBrief)} with no brief, {dropped.GetValueOrDefault(Outcome.CutUnreadable)} whose cut git can't read (fix: sindri context sources status; check the checkout)"));

    private async ValueTask<Result<Outcome>> One(BacktestRequest request, (BriefScope World, BriefIssue Issue, CorpusSample Sample) what,
        (List<Arm> Running, Dictionary<Arm, List<SampleScore>> Scores, string Step) how, (Spend Spend, List<string> TimedOut) tally, CancellationToken cancellationToken)
    {
        var spend = tally.Spend;
        var (world, issue, sample) = what;
        var brief = await briefs.Brief(world, issue, cancellationToken).ConfigureAwait(false);
        if (brief.IsError || brief.Value is null)
        {
            request.Progress?.Report($"{how.Step}: dropped, {(brief.IsError ? "brief source unavailable" : "no brief")}");
            return brief.IsError ? Outcome.SourceDown : Outcome.NoBrief;
        }

        var opened = await Open(world.Repos.First(r => r.Name == sample.Repo), sample, (world, request.Scope), cancellationToken).ConfigureAwait(false);
        if (opened.IsError)
        {
            return CutDrop(opened.Error, request.Progress, how.Step);
        }

        request.Progress?.Report($"{how.Step}: map {(opened.Value.Graph.Cached ? "cached" : "built")}");
        NoteTimeout(tally.TimedOut, opened.Value.Graph, sample);
        await Ask(request, (how.Running, sample, how.Scores), (brief.Value, opened.Value, spend), cancellationToken).ConfigureAwait(false);
        return opened.Value.Graph.Clustered ? Outcome.Scored : Outcome.ScoredWithoutClusters;
    }

    private static void NoteTimeout(List<string> timedOut, AsOfGraph graph, CorpusSample sample)
    {
        if (graph.RecognizersTimedOut)
        {
            timedOut.Add(sample.IssueKey);
        }
    }

    // Only git failing to read the cut drops a sample; a refused store (005), context.db or the cache failing would fail every other sample the same way, so the real code stops the run.
    private static Result<Outcome> CutDrop(Error error, IProgress<string>? progress, string step)
    {
        if (!string.Equals(error.Code, ContextErrors.ExtractionUnreadable.Code, StringComparison.Ordinal))
        {
            return error;
        }

        progress?.Report($"{step}: dropped, git can't read the cut");
        return Outcome.CutUnreadable;
    }

    private async ValueTask<Result<Opened>> Open(ContextRepo repo, CorpusSample sample, (BriefScope World, ContextScope Scope) given, CancellationToken cancellationToken)
    {
        var (world, scope) = given;
        var built = await maps.Build(new AsOfRequest(repo, sample, scope), cancellationToken).ConfigureAwait(false);
        if (built.IsError)
        {
            return built.Error;
        }

        var source = await docs.Open(new AsOfDocsRequest(repo, sample.Cut, world.Settings.DocGlobs, world.Scrubbers), cancellationToken).ConfigureAwait(false);
        return source.IsError ? source.Error : new Opened(built.Value, source.Value, world.Scrubbers.Egress);
    }

    private async ValueTask Ask(BacktestRequest request, (List<Arm> Running, CorpusSample Sample, Dictionary<Arm, List<SampleScore>> Scores) run, (Brief Brief, Opened Opened, Spend Spend) text,
        CancellationToken cancellationToken)
    {
        var (running, sample, scores) = run;
        var graph = text.Opened.Graph.Graph;
        var truth = BacktestScoring.TruthNodes(graph, sample.TruthFiles);
        var visible = new VisibleMap(new MapSnapshot(new MapVersion(0, sample.Repo, sample.Cut, KeyVersion: 1, sample.CreatedAt, MapVersionStatus.Current), graph, []), graph);
        var inputs = new ArmInputs(request.Scope, sample, Question(text.Brief.Text), visible, text.Opened.Docs);
        foreach (var arm in running)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await arms.Run(arm, inputs, cancellationToken).ConfigureAwait(false);
            var score = BacktestScoring.Score(sample, Arms.Name(arm), truth, result, armLeaky: arm == Arm.GraphReviewed);
            scores[arm].Add(await Adjudicated(score, (arm, truth, graph), text, cancellationToken).ConfigureAwait(false));
            request.Progress?.Report($"arm {Arms.Name(arm)} {sample.IssueKey}");
        }
    }

    // With the adjudicator on, the surfaces no PR touched are judged within the run's token cap; the rest stay unjudged and precision is not measured (083).
    private async ValueTask<SampleScore> Adjudicated(SampleScore score, (Arm Arm, IReadOnlyList<string> Truth, Core.Graph.GraphSnapshot Graph) what, (Brief Brief, Opened Opened, Spend Spend) text,
        CancellationToken cancellationToken)
    {
        var spend = text.Spend;
        if (!spend.Enabled || score.Unmapped)
        {
            return score;
        }

        var surfaces = AdjudicatedScoring.Surfaces(score.PredictedNodes, what.Truth, what.Graph);
        var judged = spend.Stopped
            ? new Adjudication(surfaces.ToDictionary(s => s.Key, _ => Verdict.Unjudged, StringComparer.Ordinal), 0, Capped: spend.Capped)
            : await adjudicator.Judge(new AdjudicationRequest(text.Brief.Text, surfaces, text.Opened.Egress, PassBars.MaxTokens - spend.Total), cancellationToken).ConfigureAwait(false);
        spend.Add(what.Arm, judged);
        return score with { AdjudicatedPrecision = AdjudicatedScoring.Precision(score.PredictedNodes, what.Truth, judged.Verdicts) };
    }

    private static string Question(string brief)
    {
        var clean = new string([.. brief.Where(c => !char.IsControl(c) || c is '\n' or '\t')]);
        return clean;
    }

    private enum Outcome
    {
        Scored,
        ScoredWithoutClusters,
        NoBrief,
        SourceDown,
        CutUnreadable,
    }

    private sealed record Opened(AsOfGraph Graph, IContextSource Docs, Core.Trust.IEgressScrubber Egress);
}
