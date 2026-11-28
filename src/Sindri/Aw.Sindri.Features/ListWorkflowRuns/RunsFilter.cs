using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Features.ListWorkflowRuns;

/// <summary>
/// The filter of a runs read (#4334): '?status=', '?since=', '?until=' and '?before=' on GET /sindri/workflows/{slug}/runs, the same arguments of MCP workflow_runs, and 'sindri wf runs --status --since --until --before'.
/// The store applies it in its query, before the page cap.
/// </summary>
/// <param name="Statuses">The statuses kept; none keeps every status.</param>
/// <param name="Since">Runs that started at or after this instant, or null.</param>
/// <param name="Until">Runs that started before this instant, or null.</param>
/// <param name="Before">Runs older than this run (the 'next' of the page before), or null.</param>
internal sealed record RunsFilter(IReadOnlyList<WorkflowRunStatus> Statuses, DateTimeOffset? Since, DateTimeOffset? Until, WorkflowRunId? Before)
{
    private const string Words = "running, ended, succeeded, failed, cancelled, queued or waiting";

    // why: 'running' is a run in flight, as the runs table's filter means it; 'ended' is the table's word for a run that succeeded
    private static readonly Dictionary<string, WorkflowRunStatus[]> _words = new(StringComparer.Ordinal)
    {
        ["running"] = [WorkflowRunStatus.Queued, WorkflowRunStatus.Running, WorkflowRunStatus.Waiting],
        ["ended"] = [WorkflowRunStatus.Succeeded],
        ["succeeded"] = [WorkflowRunStatus.Succeeded],
        ["failed"] = [WorkflowRunStatus.Failed],
        ["cancelled"] = [WorkflowRunStatus.Cancelled],
        ["queued"] = [WorkflowRunStatus.Queued],
        ["waiting"] = [WorkflowRunStatus.Waiting],
    };

    private static readonly System.Buffers.SearchValues<char> _timeChars = System.Buffers.SearchValues.Create("0123456789:.");

    private static readonly string[] _instants = ["yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK", "yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mmK", "yyyy-MM-dd"];

    /// <summary>Gets the filter that keeps every run.</summary>
    public static RunsFilter None { get; } = new([], Since: null, Until: null, Before: null);

    /// <summary>Reads the four filter values as a caller sent them; each may be null or empty for none.</summary>
    /// <param name="status">A comma-separated list of 'running' (queued, running or waiting), 'ended' or 'succeeded', 'failed', 'cancelled', 'queued' or 'waiting'.</param>
    /// <param name="since">An ISO-8601 instant ('2026-11-23T07:00:00Z'; a date alone is its midnight UTC).</param>
    /// <param name="until">An ISO-8601 instant, not before <paramref name="since"/>.</param>
    /// <param name="before">A run id, '&lt;slug&gt;@&lt;revision&gt;:&lt;occurrence&gt;'.</param>
    /// <returns>The filter, or SND-GRAPH-134 naming the value it can't read.</returns>
    public static Result<RunsFilter> Parse(string? status, string? since, string? until, string? before)
    {
        var statuses = ReadStatuses(status);
        if (statuses.IsError)
        {
            return statuses.Error;
        }

        var window = Window(since, until);
        if (window.IsError)
        {
            return window.Error;
        }

        var cursor = Cursor(before);
        return cursor.IsError ? cursor.Error : new RunsFilter(statuses.Value, window.Value.Since, window.Value.Until, cursor.Value);
    }

    private static Result<(DateTimeOffset? Since, DateTimeOffset? Until)> Window(string? since, string? until)
    {
        var from = Instant("since", since);
        if (from.IsError)
        {
            return from.Error;
        }

        var to = Instant("until", until);
        if (to.IsError)
        {
            return to.Error;
        }

        if (from.Value is { } early && to.Value is { } late && early > late)
        {
            return SindriErrors.GraphRunsFilterInvalid.With($"'since' ({Text(early)}) is after 'until' ({Text(late)}); swap them or drop one");
        }

        (DateTimeOffset? Since, DateTimeOffset? Until) window = (from.Value, to.Value);
        return window;
    }

    private static Result<WorkflowRunStatus[]> ReadStatuses(string? status)
    {
        if (string.IsNullOrEmpty(status))
        {
            return Array.Empty<WorkflowRunStatus>();
        }

        var kept = new List<WorkflowRunStatus>();
        foreach (var word in status.Split(',', StringSplitOptions.TrimEntries))
        {
            if (!_words.TryGetValue(word, out var statuses))
            {
                return SindriErrors.GraphRunsFilterInvalid.With($"'{word}' is not a run status ({Words})");
            }

            foreach (var one in statuses)
            {
                if (!kept.Contains(one))
                {
                    kept.Add(one);
                }
            }
        }

        return kept.ToArray();
    }

    private static Result<DateTimeOffset?> Instant(string name, string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (DateTimeOffset?)null;
        }

        text = RawPlus(text);
        return DateTimeOffset.TryParseExact(text, _instants, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at.ToUniversalTime()
            : SindriErrors.GraphRunsFilterInvalid.With($"'{name}': '{text}' is not an ISO-8601 instant (such as 2026-11-23T07:00:00Z, or 2026-11-23 for its midnight UTC)");
    }

    // why: a query string reads '+' as a space, so '?since=2026-11-25T10:00:00+01:00' sent raw arrives as '...00:00 01:00' (#4466). A single space right before an hh:mm offset at the end, after a time of day, is that '+'; a space anywhere else stays refused.
    private static string RawPlus(string text)
    {
        const int Tail = 6;   // ' hh:mm'
        if (text.Length <= Tail || text[^Tail] != ' ' || !IsOffset(text.AsSpan(text.Length - Tail + 1)))
        {
            return text;
        }

        var head = text[..^Tail];
        var time = head.LastIndexOf('T');
        return time > 0 && time < head.Length - 1 && head.AsSpan(time + 1).IndexOfAnyExcept(_timeChars) < 0 ? head + "+" + text[^(Tail - 1)..] : text;
    }

    private static bool IsOffset(ReadOnlySpan<char> hhmm) => hhmm.Length == 5 && char.IsAsciiDigit(hhmm[0]) && char.IsAsciiDigit(hhmm[1]) && hhmm[2] == ':' && char.IsAsciiDigit(hhmm[3]) && char.IsAsciiDigit(hhmm[4]);

    private static Result<WorkflowRunId?> Cursor(string? before)
    {
        if (string.IsNullOrEmpty(before))
        {
            return (WorkflowRunId?)null;
        }

        return WorkflowRunId.TryFrom(before, out var id)
            ? id
            : SindriErrors.GraphRunsFilterInvalid.With($"'before': '{before}' is not a run id ('<slug>@<revision>:<occurrence>', the 'next' of the page before)");
    }

    private static string Text(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
