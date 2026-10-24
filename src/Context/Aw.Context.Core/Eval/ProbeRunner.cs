using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Kernel;

namespace Aw.Context.Core.Eval;

/// <summary>
/// Runs a probe file (spec §9.2 item 3, Phase 3): asks each question through a delegate (in Sindri, the CLI's GET
/// /sindri/context/query), scores the answers, and compares the means with the baseline of the same file (its newest complete run that did not regress).
/// </summary>
public static partial class ProbeRunner
{

    /// <summary>Runs <paramref name="file"/>.</summary>
    /// <param name="file">The checked probe file.</param>
    /// <param name="ask">Answers one probe's question; an error is scored 0 for that probe and makes the run incomplete (never a regression, never a baseline).</param>
    /// <param name="baseline">The baseline of the same file (<see cref="IProbeReportStore.Baseline"/>), or null.</param>
    /// <param name="runAt">The time now.</param>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>The report (<see cref="ProbeReport.Incomplete"/> when any probe failed, every probe included).</returns>
    public static async ValueTask<Result<ProbeReport>> Run(ProbeFile file, Func<ProbeCase, CancellationToken, ValueTask<Result<ContextResult>>> ask, ProbeReport? baseline,
        DateTimeOffset runAt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(ask);
        var scores = new List<ProbeScore>();
        string? mapVersion = null;
        foreach (var probe in file.Set.Probes)
        {
            var answer = await ask(probe, cancellationToken).ConfigureAwait(false);
            if (answer.IsError)
            {
                scores.Add(new ProbeScore(probe.Id, 0, 0, [], [], [], answer.Error.Code));
                continue;
            }

            mapVersion ??= answer.Value.MapVersion;
            scores.Add(ProbeScoring.Score(probe, answer.Value));
        }

        var totals = new ProbeTotals(ProbeScoring.Round(scores.Average(s => s.PrecisionAt5)), ProbeScoring.Round(scores.Average(s => s.CitationCorrectness)), scores.Count, runAt,
            scores.Sum(s => s.AbsentReturned.Count));
        var before = baseline?.Totals;
        var report = new ProbeReport(ProbeReport.CurrentSchemaVersion, file.Path, file.Hash, mapVersion, scores, totals, before, Regressed: false);
        return report.Incomplete || baseline is null ? report : report with { Regressed = Worse(report, baseline) };
    }

    /// <summary>Lower means than the baseline's, or a probe naming more absent nodes than the same probe did there.</summary>
    private static bool Worse(ProbeReport now, ProbeReport before) =>
        now.Totals.PrecisionAt5 < before.Totals.PrecisionAt5 || now.Totals.CitationCorrectness < before.Totals.CitationCorrectness
        || now.Probes.Any(p => before.Probes.FirstOrDefault(b => b.Id == p.Id) is { } was && p.AbsentReturned.Count > was.AbsentReturned.Count);

    /// <summary>Checks a probe file's content: at least one probe, ids unique slugs, non-empty questions, schema version 1.</summary>
    /// <param name="set">The parsed file.</param>
    /// <returns>Null when valid, else SND-CONTEXT-035 naming the key path.</returns>
    public static Error? Check(ProbeSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (set.SchemaVersion is { } version && version != 1)
        {
            return Invalid("schemaVersion", "this build reads probe files of version 1");
        }

        if (set.Probes is null || set.Probes.Count == 0)
        {
            return Invalid("probes", "needs at least one probe");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < set.Probes.Count; i++)
        {
            var probe = set.Probes[i];
            var at = string.Create(CultureInfo.InvariantCulture, $"probes[{i}]");
            var problem = probe switch
            {
                null => Invalid(at, "is empty"),
                { Expect: null } => Invalid(at + ".expect", "is required (an object, its lists optional)"),
                _ when !Slug.IsMatch(probe.Id.Value) => Invalid(at + ".id", "is a slug: lower-case letters, digits and '-', 1 to 100 characters"),
                _ when !seen.Add(probe.Id.Value) => Invalid(at + ".id", $"'{probe.Id.Value}' is already the id of an earlier probe"),
                _ when string.IsNullOrWhiteSpace(probe.Question) => Invalid(at + ".question", "is not empty"),
                _ when !QueryRules.FitsQueryUrl(probe.Question) => Invalid(at + ".question", $"is over {QueryRules.MaxEncodedText} characters once percent-encoded for the query (shorten it)"),
                _ when QueryRules.Terms(probe.Question).Count == 0 => Invalid(at + ".question", "needs at least one word (letters or digits)"),
                _ => null,
            };
            if (problem is not null)
            {
                return problem;
            }
        }

        return null;
    }

    private static Error Invalid(string keyPath, string problem) => ContextErrors.ProbeFileInvalid.With($"{keyPath}: {problem}; nothing was run");

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Slug { get; }
}
