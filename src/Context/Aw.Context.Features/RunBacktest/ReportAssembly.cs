using System.Globalization;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>Turns the scores into the report (spec §9.1, §9.4). Pure.</summary>
internal static class ReportAssembly
{
    internal static BacktestReport Assemble(BacktestRequest request, (DateTimeOffset Started, LeftOut LeftOut) run, Collected collected)
    {
        var kept = collected.Kept;
        var briefScore = collected.Scores.TryGetValue(Arm.Brief, out var brief) ? BacktestScoring.Aggregate(brief, tokens: 0) : null;
        var rows = new List<ArmReport>();
        foreach (var arm in Arms.Available.Concat(Arms.Ranker).Where(request.Arms.Contains).Concat(Arms.NotAvailable))   // the arms not available yet always print their row (spec §9.3)
        {
            if (Arms.PendingStage(arm) is { } stage)
            {
                rows.Add(new ArmReport(Arms.Name(arm), "source", Leaky: false, stage, Score: null, PassLine: null, []));
                continue;
            }

            var scores = collected.Scores[arm];
            var score = BacktestScoring.Aggregate(scores, collected.Tokens.GetValueOrDefault(arm));
            rows.Add(new ArmReport(Arms.Name(arm), Arms.Ranker.Contains(arm) ? "ranker" : "source", arm == Arm.GraphReviewed, NotAvailable: null, score, PassLine.Evaluate(score, arm == Arm.Brief ? null : briefScore, kept.Count), scores));
        }

        var overall = PassLine.Overall(rows);
        return new BacktestReport(BacktestReport.CurrentSchemaVersion, run.Started, Window(request), kept.Count, kept.Count < PassBars.MinSamples,
            kept.Count == 0 ? 0 : Math.Round((double)kept.Count(s => s.Leaky) / kept.Count, 4, MidpointRounding.ToEven), run.LeftOut, kept, rows, overall, Degraded(run.LeftOut, collected));
    }

    private static List<string> Degraded(LeftOut left, Collected collected)
    {
        var notes = new List<string>();
        if (left.SourceDown > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture, $"SND-CONTEXT-011: {left.SourceDown} sample(s) dropped because the brief source was unavailable (fix: sindri context sources status)"));
        }

        if (collected.CappedTeams.Count > 0)
        {
            notes.Add($"issue listing capped: {string.Join(", ", collected.CappedTeams)} reached the source's per-search limit and the source has no next page, so issues past it are not in this corpus (fix: narrow --since)");
        }

        if (collected.WithoutClusters > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture,
                $"clusters unavailable: {collected.WithoutClusters} of {collected.Kept.Count} sample maps were built without clusters, so the map-reading arms ran on thinner maps (fix: install graphify, or sindri doctor)"));
        }

        if (collected.RecognizerTimeouts > 0)
        {
            notes.Add(string.Create(CultureInfo.InvariantCulture,
                $"recognizers timed out: {collected.RecognizerTimeouts} of {collected.Kept.Count} sample maps lost profile recognizer matches to the one-second match timeout and were not cached (fix: simplify the pattern in context.extract.recognizers)"));
        }

        if (collected.AdjudicationCapped)
        {
            notes.Add("SND-CONTEXT-083: adjudication stopped at the 1.5M-token cap; the rest are unjudged and adjudicated precision is not measured");
        }

        if (collected.AdjudicationError is { } code)
        {
            notes.Add($"{code}: adjudication incomplete: the model could not answer, so the surfaces are unjudged and adjudicated precision is not measured; no further model call was made");
        }

        return notes;
    }

    private static string Window(BacktestRequest request)
    {
        var range = request.Since is null && request.Until is null
            ? "all"
            : string.Create(CultureInfo.InvariantCulture, $"{request.Since:yyyy-MM-dd}..{request.Until:yyyy-MM-dd}");
        return request.Limit is { } limit ? string.Create(CultureInfo.InvariantCulture, $"{range}, first {limit}") : range;
    }
}
