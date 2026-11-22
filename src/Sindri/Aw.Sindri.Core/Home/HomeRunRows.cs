using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Core.Home;

/// <summary>The 'run-failed' rows (home spec §3.2): one per live run that ended failed and that the owner has not seen, from the 'home-runs' collector.</summary>
public static class HomeRunRows
{
    /// <summary>The collector's name, which is each row's source.</summary>
    public const string SourceName = "home-runs";

    /// <summary>The 'failed' reason weight (home spec §4.2 defaults).</summary>
    public const int Weight = 4;

    /// <summary>The longest error line a title carries.</summary>
    public const int ErrorChars = 80;

    private const string Prefix = "sindri:run-failed:";

    private const string UnknownTrigger = "unknown trigger";

    /// <summary>The row id of a failed run: 'sindri:run-failed:&lt;run id&gt;'.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The row id.</returns>
    public static HomeRowId IdOf(WorkflowRunId run) => HomeRowId.From(Prefix + run.Value);

    /// <summary>The run a 'run-failed' row id names.</summary>
    /// <param name="id">The row id.</param>
    /// <returns>The run, or null when the id is not a 'run-failed' row's.</returns>
    public static WorkflowRunId? RunOf(HomeRowId id) =>
        id.Value.StartsWith(Prefix, StringComparison.Ordinal) && WorkflowRunId.TryFrom(id.Value[Prefix.Length..], out var run) ? run : null;

    /// <summary>The row for a failed run: the title names the workflow and the first line of its error (scrubbed, cut to <see cref="ErrorChars"/>), the facts are 'failed', the run and what started it.</summary>
    /// <param name="run">The failed run.</param>
    /// <param name="readAt">When the collector read the store.</param>
    /// <param name="scrubber">Scrubs the error and trigger text before they reach the ledger.</param>
    /// <returns>The row to raise, with no edges.</returns>
    public static HomeRaise Failed(WorkflowRun run, DateTimeOffset readAt, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(scrubber);
        var slug = run.Id.Value[..run.Id.Value.IndexOf('@', StringComparison.Ordinal)];
        var error = FirstLine(run.Error is { } text ? scrubber.Scrub(text) : null);
        var title = error is null ? $"{slug}: run {run.Id.Occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture)} failed" : $"{slug} failed: {error}";
        IReadOnlyList<HomeLink> links = run.ItemId is { } item
            ? [new HomeLink("run", HomeRowId.From("sindri:run:" + run.Id.Value), slug), new HomeLink("item", HomeRowId.From("sindri:item:" + item.Value), Title: null)]
            : [new HomeLink("run", HomeRowId.From("sindri:run:" + run.Id.Value), slug)];
        var row = new HomeRow(
            IdOf(run.Id),
            Version: 1,
            HomeKind.RunFailed,
            HomeGroup.NeedsYou,
            title,
            [new HomeFact(HomeFactKind.Why, "failed"), new HomeFact(HomeFactKind.Ref, run.Id.Value), new HomeFact(HomeFactKind.From, scrubber.Scrub(StartedBy(run.Trigger)))],
            WaitsOn: "you",
            run.EndedAt ?? run.StartedAt,
            new HomeRank(Blocks: 0, Weight, SinceRank: 0, Position: 0),
            BlocksList: [],
            links,
            HomeState.Open,
            SnoozeUntil: null,
            DoneAt: null,
            Actions: ["retry-run", "open-run", "hand-to-agent"],
            new HomeSource(SourceName, readAt, Connection: null));
        return new HomeRaise(row, Edges: []);
    }

    private static string? FirstLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var line = text.AsSpan().Trim();
        var end = line.IndexOfAny('\r', '\n');
        line = (end < 0 ? line : line[..end]).TrimEnd();
        return line.Length <= ErrorChars ? line.ToString() : string.Concat(line[..(ErrorChars - 1)].TrimEnd(), "…");
    }

    // What started the run, in the owner's words: a manual run names who pressed it, any other names its trigger kind.
    private static string StartedBy(string trigger)
    {
        try
        {
            if (JsonNode.Parse(trigger) is JsonObject node && node["kind"] is JsonValue kind && kind.TryGetValue<string>(out var name))
            {
                return node["by"] is JsonValue by && by.TryGetValue<string>(out var who) && !string.IsNullOrWhiteSpace(who) ? $"{name} by {who}" : name;
            }
        }
        catch (JsonException)   // boundary: a stored trigger is a JSON object (WorkflowRun.TriggerProblem), but a row is still made from a bad one
        {
            return UnknownTrigger;
        }

        return UnknownTrigger;
    }
}
