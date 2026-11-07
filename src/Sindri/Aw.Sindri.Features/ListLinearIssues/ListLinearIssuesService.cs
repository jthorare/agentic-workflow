using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;

namespace Aw.Sindri.Features.ListLinearIssues;

/// <summary>
/// The Work page's Linear list (Linear hub spec §3; contract with the app, 2026-11-06). Open means a state type other than completed and canceled. <b>mine</b> reads the viewer's open issues in one go
/// (Context's largest page, 200), filters priority, label and state in Sindri, orders them by priority (urgent first, none last) then newest update and pages them by offset; over 200 assigned open
/// issues the rest are not listed. <b>teams</b> keeps Context's newest-first order, passes team, project, cycle and the state types through, drops the viewer's own issues and the priority, label and
/// state misses in Sindri, and reads pages until the page is full; its cursor names the last row returned and how many rows came before it, which is all its total only on the last page.
/// </summary>
/// <param name="facts">The stored issues, source and feed.</param>
/// <param name="links">The items and pull requests linked to the rows.</param>
internal sealed class ListLinearIssuesService(ILinearIssueFacts facts, ILinearItemLinks links) : IService<ListLinearIssuesRequest, ListLinearIssuesResponse>
{
    internal const string Mine = "mine";
    internal const string Teams = "teams";

    private const int DefaultLimit = 50;
    private const int ContextPage = 200;
    private const int MaxPages = 50;
    private static readonly string[] _open = ["triage", "backlog", "unstarted", "started"];

    public async ValueTask<Result<ListLinearIssuesResponse>> Handle(ListLinearIssuesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = Parse(request);
        if (query.IsError)
        {
            return query.Error;
        }

        var q = query.Value;
        var source = await facts.Source(cancellationToken).ConfigureAwait(false);
        if (source.IsError)
        {
            return source.Error;
        }

        var s = source.Value;
        if (!s.Connected)
        {
            return Page(s, q, [], next: null, total: 0);
        }

        var read = q.Group == Mine ? await ReadMine(q, s.Viewer, cancellationToken).ConfigureAwait(false) : await ReadTeams(q, s.Viewer, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var rows = await Linked(read.Value.Issues, cancellationToken).ConfigureAwait(false);
        return Page(s, q, rows, read.Value.Next, read.Value.Total);
    }

    private static ListLinearIssuesResponse Page(LinearSourceFacts source, Query q, IReadOnlyList<LinearIssueRowView> rows, string? next, int? total) =>
        new(source.Connected, source.Viewer, source.Feed, q.Group, rows, next, total);

    private async ValueTask<Result<Found>> ReadMine(Query q, LinearPersonView? viewer, CancellationToken cancellationToken)
    {
        // why: until the viewer step has run nobody's issues are "mine", so the group is empty rather than wrong
        if (viewer is null || (q.Assignee is not null && !string.Equals(q.Assignee, viewer.Id, StringComparison.Ordinal)))
        {
            return new Found([], Next: null, Total: 0);
        }

        var offset = q.After is null ? 0 : MineOffset(q.After);
        if (offset is null)
        {
            return SindriErrors.InvalidQuery.With($"after '{q.After}' is not a cursor of the mine group");
        }

        var page = await facts.Issues(new LinearIssueFactsFilter(q.Team, viewer.Id, q.StateTypes, q.Project, q.Cycle, ContextPage), after: null, cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        // why: Linear's own order puts "no priority" (0) last, after low (4)
        var matching = page.Value.Issues.Where(q.Matches)
            .OrderBy(static i => i.Priority == 0 ? int.MaxValue : i.Priority)
            .ThenByDescending(static i => i.UpdatedAt)
            .ThenBy(static i => i.Record, StringComparer.Ordinal)
            .ToList();
        var shown = matching.Skip(offset.Value).Take(q.Limit).ToList();
        var end = offset.Value + shown.Count;
        return new Found(shown, end < matching.Count ? "m" + end.ToString(CultureInfo.InvariantCulture) : null, matching.Count);
    }

    private async ValueTask<Result<Found>> ReadTeams(Query q, LinearPersonView? viewer, CancellationToken cancellationToken)
    {
        (int Skipped, LinearIssuePosition? Position) start = q.After is null ? (0, null) : TeamsStart(q.After);
        if (start.Skipped < 0)
        {
            return SindriErrors.InvalidQuery.With($"after '{q.After}' is not a cursor of the teams group");
        }

        var scan = await Scan(q, viewer, start.Position, cancellationToken).ConfigureAwait(false);
        if (scan.IsError)
        {
            return scan.Error;
        }

        var (rows, more) = scan.Value;
        var count = start.Skipped + rows.Count;
        return new Found(rows, more && rows.Count > 0 ? TeamsCursor(count, rows[^1]) : null, more ? null : count);
    }

    // Reads store pages from position until the page is full; the flag says there may be more after it.
    private async ValueTask<Result<(List<LinearIssueRowView> Rows, bool More)>> Scan(Query q, LinearPersonView? viewer, LinearIssuePosition? position, CancellationToken cancellationToken)
    {
        var filter = new LinearIssueFactsFilter(q.Team, q.Assignee, q.StateTypes, q.Project, q.Cycle, ContextPage);
        List<LinearIssueRowView> rows = [];
        var more = false;
        for (var pages = 0; pages < MaxPages && !more; pages++)
        {
            var page = await facts.Issues(filter, position, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            more = Fill(rows, page.Value.Issues.Where(i => (viewer is null || i.Assignee?.Id != viewer.Id) && q.Matches(i)), q.Limit);

            // why: a full page that ends the store's page may still have matches after it; Sindri can't know without reading on, so it offers the next page
            more |= rows.Count == q.Limit && page.Value.More;
            if (page.Value.Issues.Count == 0 || !page.Value.More)
            {
                break;
            }

            position = Position(page.Value.Issues[^1]);
        }

        return (rows, more);
    }

    // Adds matches until the page is full; true when one more match was left over.
    private static bool Fill(List<LinearIssueRowView> rows, IEnumerable<LinearIssueRowView> matches, int limit)
    {
        foreach (var issue in matches)
        {
            if (rows.Count == limit)
            {
                return true;
            }

            rows.Add(issue);
        }

        return false;
    }

    private async ValueTask<IReadOnlyList<LinearIssueRowView>> Linked(IReadOnlyList<LinearIssueRowView> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        // why: a link read that fails must not hide the issues; the rows then show without their item and PR
        var found = await links.For([.. rows.Select(static r => r.Record)], cancellationToken).ConfigureAwait(false);
        return found.IsError ? rows : [.. rows.Select(r => found.Value.TryGetValue(r.Record, out var l) ? r with { Link = l.Link, Pr = l.Pr } : r)];
    }

    private static LinearIssuePosition Position(LinearIssueRowView row) => new(row.UpdatedAt, row.Record);

    private static string TeamsCursor(int count, LinearIssueRowView last) =>
        string.Create(CultureInfo.InvariantCulture, $"t{count}.{last.UpdatedAt.UtcTicks}.{last.Record}");

    // why: an unreadable cursor is reported as a negative count, so the caller has one check
    private static (int Skipped, LinearIssuePosition? Position) TeamsStart(string after)
    {
        var parts = after.Length > 1 && after[0] == 't' ? after[1..].Split('.', 3) : [];
        return parts.Length == 3
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && ticks <= DateTimeOffset.MaxValue.UtcTicks
            && parts[2].Length > 0
                ? (count, new LinearIssuePosition(new DateTimeOffset(ticks, TimeSpan.Zero), parts[2]))
                : (-1, null);
    }

    private static int? MineOffset(string after) =>
        after.Length > 1 && after[0] == 'm' && int.TryParse(after[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) ? offset : null;

    private static Result<Query> Parse(ListLinearIssuesRequest r)
    {
        var group = Text(r.Group) ?? Mine;
        if (group is not (Mine or Teams))
        {
            return SindriErrors.InvalidQuery.With($"group is 'mine' or 'teams', not '{group}'");
        }

        var limit = Number(r.Limit, DefaultLimit, 1, ContextPage);
        var priority = Number(r.Priority, fallback: null, 0, 4);
        if (limit is null || (Text(r.Priority) is not null && priority is null))
        {
            return limit is null ? SindriErrors.InvalidQuery.With($"limit is a number from 1 to {ContextPage}, not '{r.Limit}'") : SindriErrors.InvalidQuery.With($"priority is a number from 0 to 4, not '{r.Priority}'");
        }

        var state = Text(r.State);
        var open = state is null || string.Equals(state, "open", StringComparison.Ordinal);
        return new Query(group, Text(r.Team), Text(r.Project), Text(r.Cycle), open ? null : state, open ? _open : null, Text(r.Assignee), priority, Text(r.Label), Text(r.After), limit.Value);
    }

    // The number in the text if it is whole and within the range, the fallback when there is no text, else null.
    private static int? Number(string? text, int? fallback, int min, int max) =>
        Text(text) is not { } t ? fallback : int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= min && n <= max ? n : null;

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Found(IReadOnlyList<LinearIssueRowView> Issues, string? Next, int? Total);

    private sealed record Query(string Group, string? Team, string? Project, string? Cycle, string? StateId, IReadOnlyList<string>? StateTypes, string? Assignee, int? Priority, string? Label, string? After, int Limit)
    {
        internal bool Matches(LinearIssueRowView issue) =>
            (StateId is null || string.Equals(issue.State.Id, StateId, StringComparison.Ordinal))
            && (Priority is null || issue.Priority == Priority)
            && (Label is null || issue.Labels.Any(l => string.Equals(l.Id, Label, StringComparison.Ordinal)));
    }
}
