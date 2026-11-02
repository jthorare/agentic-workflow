using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Evals;
using Aw.Judge.Core.Labels;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>An eval report as JSON (the CLI's stdout and the .json report) and as Markdown (the .md report).</summary>
internal static class EvalReportText
{
    internal const string AnyLabels = "any";

    internal static JsonObject ToNode(EvalReport report)
    {
        var node = new JsonObject
        {
            ["question"] = report.Question,
            ["provider"] = report.Provider,
            ["variant"] = report.Variant,
            ["labelSource"] = Source(report.Labels),
            ["n"] = report.N,
            ["decided"] = report.Decided,
            ["accuracy"] = report.Accuracy,
            ["decisiveRate"] = report.DecisiveRate,
            ["accuracyDecisive"] = report.AccuracyDecisive,
            ["p50LatencyMs"] = report.P50LatencyMs,
            ["sweep"] = new JsonArray([.. report.Sweep.Select(s => (JsonNode)new JsonObject
            {
                ["threshold"] = s.Threshold,
                ["coverage"] = s.Coverage,
                ["accuracyCovered"] = s.AccuracyCovered,
            })]),
        };
        if (report.PerSource is { } sources)
        {
            node["perSource"] = SourcesNode(sources);
        }

        if (report.SourceWarning is { } warning)
        {
            node["sourceWarning"] = warning;
        }

        return node;
    }

    private static JsonObject SourcesNode(SourcedReports sources) => new()
    {
        ["outcome"] = ToNode(sources.Outcome),
        ["adjudicator"] = ToNode(sources.Adjudicator),
        ["agreement"] = new JsonObject
        {
            ["shared"] = sources.Agreement.Shared,
            ["agreed"] = sources.Agreement.Agreed,
            ["rate"] = EvalScoring.Ratio(sources.Agreement.Agreed, sources.Agreement.Shared),
        },
    };

    internal static string Render(EvalReport report)
    {
        List<string> lines =
        [
            $"# judge eval: {report.Question} / {report.Provider} / {report.Variant}",
            "",
            $"Label source: {Source(report.Labels)}",
            "",
            Summary(report),
            "",
            "| Threshold | Coverage | Accuracy (covered) |",
            "|---|---|---|",
            .. report.Sweep.Select(s => string.Create(CultureInfo.InvariantCulture, $"| {s.Threshold} | {Percent(s.Coverage)} | {Percent(s.AccuracyCovered)} |")),
            "",
        ];
        if (report.PerSource is { } sources)
        {
            var agreement = sources.Agreement;
            lines.AddRange(
            [
                "## Per label source (shared items)",
                "",
                string.Create(CultureInfo.InvariantCulture, $"Outcome vs adjudicator agreement: {agreement.Agreed}/{agreement.Shared} ({Percent(EvalScoring.Ratio(agreement.Agreed, agreement.Shared))})"),
                "",
                $"- outcome: {Summary(sources.Outcome)}",
                $"- adjudicator: {Summary(sources.Adjudicator)}",
                "",
            ]);
        }

        if (report.SourceWarning is { } warning)
        {
            lines.AddRange([$"WARNING: {warning}", ""]);
        }

        return new StringBuilder().AppendJoin('\n', lines).ToString();
    }

    internal static string FileStem(EvalReport report, long timestamp) =>
        string.Create(CultureInfo.InvariantCulture, $"{report.Question}-{report.Provider}-{report.Variant}-{timestamp}");

    private static string Source(LabelSource? source) => source is { } value ? LabelSources.Name(value) : AnyLabels;

    private static string Percent(double value) => string.Create(CultureInfo.InvariantCulture, $"{value * 100:F1}%");

    private static string Summary(EvalReport r) => string.Create(
        CultureInfo.InvariantCulture,
        $"n={r.N} decided={r.Decided} accuracy={Percent(r.Accuracy)} decisive={Percent(r.DecisiveRate)} accuracy-on-decisive={Percent(r.AccuracyDecisive)} p50={r.P50LatencyMs}ms");
}
