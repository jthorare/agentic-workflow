using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Features.ContextCalls;

namespace Aw.Sindri.Host.Commands;

/// <summary>Plain text for the map verbs: words, never colour, so NO_COLOR and pipes read the same.</summary>
internal static class ContextMapText
{
    internal static string Counts(MapCounts c) => string.Create(CultureInfo.InvariantCulture,
        $"{c.Nodes} nodes, {c.Edges} edges, {c.Aliases} aliases, {c.Decisions} decisions, {c.Overrides} overrides");

    internal static string Preview(MapFilePreview p) => $"map file {p.Path}\nrepo {p.Repo.Value}, {Kind(p.Kind)}: {Counts(p.Counts)}\nhash {p.Hash}\n";

    internal static string Imported(ImportContextMapResponse r) => r.Imported is not { } i
        ? Preview(r.Preview)
        : string.Create(CultureInfo.InvariantCulture,
            $"Imported map v{i.Version.Number} (current) for {i.Version.Repo.Value}: {Counts(i.Counts)}\nhash {i.Hash}; recorded as event {r.Sequence}\n");

    internal static string Status(MapStatusView s) => string.Create(CultureInfo.InvariantCulture,
        $"map v{s.Version.Number} {Word(s.Version.Status)} · {s.Version.Repo.Value} · created {s.Version.Created:O}{(s.ScopedTo is { } repo ? $" · scoped to {repo.Value}" : string.Empty)}\n"
        + $"{Counts(s.Counts)}{Scope(s.ScopedTo)}; {s.Orphaned} orphaned overrides; {s.Retained} retained versions\n")
        + (s.Checks is { } checks ? $"structure: {checks}\n" : string.Empty)
        + (s.RulesOlder ? "built under older map rules; a refresh will rebuild it (sindri context map refresh)\n" : string.Empty)
        + (s.Coverage is { } coverage ? ContextLensText.Coverage(coverage) : string.Empty);

    /// <summary>'context map checks': per measure the value, both limits and the verdict, then the largest offenders (feature keys, so only here).</summary>
    internal static string Checks(MapChecksView view)
    {
        var report = view.Report;
        var title = view.Source switch
        {
            MapChecksSource.Draft => string.Create(CultureInfo.InvariantCulture, $"draft map v{view.Version}"),
            MapChecksSource.Build => "the map a build would save",
            MapChecksSource.Current => string.Create(CultureInfo.InvariantCulture, $"current map v{view.Version}"),
            _ => throw new ArgumentOutOfRangeException(nameof(view), view.Source, "an unknown map source"),
        };
        var text = new System.Text.StringBuilder().Append(title).Append(" (context.checks.mode: ").Append(view.Mode == ChecksMode.Warn ? "warn" : "refuse").Append(")\n");
        _ = text.Append(Measure(report.Isolated, "features with no link to another feature or a table")).Append(Measure(report.OverCap, string.Create(CultureInfo.InvariantCulture, $"features over {MapChecks.SizeCap} files")));
        if (report.NoFileEdges)
        {
            _ = text.Append("structure: no file edges (imports and data stores not extracted)\n");
        }

        foreach (var offender in report.Offenders)
        {
            _ = text.Append(string.Create(CultureInfo.InvariantCulture, $"  {offender.Measure}: {offender.Feature} ({offender.Files} files)\n"));
        }

        return text.ToString();
    }

    private static string Measure(CheckMeasure m, string what) => m.Skipped is { } why
        ? $"{m.Name}: not measured ({why})\n"
        : string.Create(CultureInfo.InvariantCulture, $"{m.Name}: {m.Count} of {m.Total} {what} = {MapChecksReport.Percent(m.Permille)}; degraded above {MapChecksReport.Percent(m.DegradedPermille)}, refused above {MapChecksReport.Percent(m.RefusePermille)}: {m.Verdict.ToString().ToLowerInvariant()}\n");

    /// <summary>0 all pass, 1 degraded (or a would-be refusal under 'warn'), 3 would be refused.</summary>
    internal static int ChecksExit(MapChecksView view) => view.Report.Verdict switch
    {
        CheckVerdict.Refuse => view.Mode == ChecksMode.Refuse ? 3 : 1,
        CheckVerdict.Degraded => 1,
        CheckVerdict.Pass => view.Report.NoFileEdges ? 1 : 0,
        _ => throw new ArgumentOutOfRangeException(nameof(view), view.Report.Verdict, "an unknown verdict"),
    };

    /// <summary>The publish state as lines under the map's own status: the state in the spec's short form, then the sentences (code index spec 6.4).</summary>
    internal static string Publish(MapPublishStatusView p) => $"publish: {PublishShort(p)}\n" + string.Concat(p.Lines.Select(static l => "  " + l + "\n"));

    private static string PublishShort(MapPublishStatusView p)
    {
        var version = string.Create(CultureInfo.InvariantCulture, $"v{p.Version}");
        var at = p.Until?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        return p.State switch
        {
            MapPublishStatusRules.Held => $"held: {version}, until {at}" + (p.WaitingOn is { Count: > 0 } on ? ", on " + string.Join(", ", on) : string.Empty),
            MapPublishStatusRules.Overdue => $"overdue: {version}, was due {at}",
            MapPublishStatusRules.PublishedWithoutWaiting => $"published: {version} without waiting ({WithoutWaiting(p.Reason)})",
            MapPublishStatusRules.PublishedStale => $"published stale: {version} (reason {Kebab(p.Reason)}, {string.Join(", ", p.StaleRepos.Select(static r => r.Repo))} stale)",
            MapPublishStatusRules.PublishedByHand => $"published by hand: {version}",
            _ => $"published: {version}",
        };
    }

    private static string WithoutWaiting(string? reason) => reason switch
    {
        "FirstMap" => "first map",
        "NoIndex" => "no code index",
        "WaitOff" => "waiting is off",
        "IndexBroken" => "code index broken",
        _ => Kebab(reason),
    };

    private static string Kebab(string? reason) => reason is null
        ? "unknown"
        : string.Concat(reason.Select(static (c, i) => char.IsUpper(c) ? (i == 0 ? string.Empty : "-") + char.ToLowerInvariant(c) : c.ToString()));

    /// <summary>The status document with the publish document's fields beside its own (state, word, reason, stale, staleRepos, until, lines; its version is 'publishedVersion', as 'version' is the map's).</summary>
    internal static ReadOnlyMemory<byte> WithPublish(ReadOnlyMemory<byte> status, ReadOnlyMemory<byte> publish)
    {
        using var map = JsonDocument.Parse(status);
        using var published = JsonDocument.Parse(publish);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var property in map.RootElement.EnumerateObject())
            {
                property.WriteTo(writer);
            }

            foreach (var property in published.RootElement.EnumerateObject())
            {
                if (property.NameEquals("version"))
                {
                    writer.WritePropertyName("publishedVersion");
                    property.Value.WriteTo(writer);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    internal static string Exported(MapExported e) => string.Create(CultureInfo.InvariantCulture,
        $"Exported map v{e.Version} to {e.Path}: {Counts(e.Counts)}\n");

    // A session's counts are its repo's slice (spec §3), so they say so: doctor's are the whole map's, and the two lines are read side by side.
    private static string Scope(ContextRepoName? repo) => repo is { } r
        ? $" in {r.Value}'s scope (its code, what it implements and the linked intent; not the whole map)"
        : string.Empty;

    private static string Kind(MapDocumentKind kind) => kind == MapDocumentKind.Export ? "export" : "authored";

    private static string Word(MapVersionStatus status) => status switch
    {
        MapVersionStatus.Current => "current",
        MapVersionStatus.Retained => "retained",
        MapVersionStatus.Draft => "draft",
        MapVersionStatus.Building => "building",
        _ => "unknown",
    };
}
