using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>
/// The issue facts over Context's IContextEngine (Linear hub spec §4.3): <c language="text">IssueList</c> under the current caller's scope, so a session sees only the teams mapped to its repo, and every name the
/// rows need (state, assignee, cycle, label from the team lookups; project from the stored catalog; the viewer from the source's identity) under the owner's scope, because a session is refused the lookups
/// and only decides which rows it sees. The feed is the <c language="text">linear</c> source's progress row: failing when its last tick ended with a code (the time cap, SND-CONTEXT-059, is a normal stop,
/// not a failure), rereading while it re-reads stored issues. Context knows how many issues the re-read has stored, not how many it will read, so the total of the re-read is null.
/// Any failure of Context is the call's error.
/// </summary>
/// <param name="scopes">The caller's and the owner's scopes.</param>
/// <param name="engine">Context's facade.</param>
internal sealed class ContextLinearIssueFacts(LinearScopes scopes, IContextEngine engine) : ILinearIssueFacts
{
    private const string LinearSource = "linear";
    private const string TimeCap = "SND-CONTEXT-059";
    private const string ProjectPrefix = "linear:project/";

    public async ValueTask<Result<LinearSourceFacts>> Source(CancellationToken cancellationToken)
    {
        var owner = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        if (owner.IsError)
        {
            return owner.Error;
        }

        var progress = await engine.Progress(owner.Value, cancellationToken).ConfigureAwait(false);
        if (progress.IsError)
        {
            return progress.Error;
        }

        if (progress.Value.FirstOrDefault(static p => string.Equals(p.Source.Value, LinearSource, StringComparison.Ordinal)) is not { } row)
        {
            return new LinearSourceFacts(Connected: false, Viewer: null, new LinearFeedView("ok", LastSyncedAt: null, Code: null, Reread: null));
        }

        // why: the viewer is the owner of the key; a Context that can't say leaves "you" unknown, which the list shows as "Finding your issues"
        var identity = await engine.Projects(new ProjectFactsRequest([]), owner.Value, cancellationToken).ConfigureAwait(false);
        var viewer = identity.IsSuccess && identity.Value.Owner is { } who ? new LinearPersonView(who.Id.Value, who.Name ?? who.Id.Value) : null;
        return new LinearSourceFacts(Connected: true, viewer, Feed(row));
    }

    public async ValueTask<Result<LinearIssueFactsPage>> Issues(LinearIssueFactsFilter filter, LinearIssuePosition? after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var caller = await scopes.Read(owner: false, cancellationToken).ConfigureAwait(false);
        var owner = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        if (caller.IsError || owner.IsError)
        {
            return caller.IsError ? caller.Error : owner.Error;
        }

        // why: an id that can't be one matches no issue, as an id nothing has does
        if (Unreadable(filter.Assignee) || Unreadable(filter.Project) || Unreadable(filter.Cycle))
        {
            return new LinearIssueFactsPage([], More: false);
        }

        var context = new LinearIssueFilter(filter.Team, Id(filter.Assignee), filter.StateTypes, Id(filter.Project), Id(filter.Cycle), filter.Limit);
        var page = await engine.IssueList(context, Cursor(after), caller.Value, cancellationToken).ConfigureAwait(false);
        if (page.IsError)
        {
            return page.Error;
        }

        var names = await Names(page.Value.Issues, owner.Value, cancellationToken).ConfigureAwait(false);
        return new LinearIssueFactsPage([.. page.Value.Issues.Select(i => Row(i, names))], page.Value.Next is not null);
    }

    public async ValueTask<Result<LinearIssueDetailFacts?>> Detail(string key, CancellationToken cancellationToken)
    {
        var parsed = LinearIssueKey.Parse(key);
        var pair = parsed.IsError ? parsed.Error : await Scopes(cancellationToken).ConfigureAwait(false);
        if (pair.IsError)
        {
            return pair.Error;
        }

        var (caller, owner) = pair.Value;
        var read = await engine.IssueGet(parsed.Value, caller, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        if (read.Value is not { } detail)
        {
            return Results.Success<LinearIssueDetailFacts?>(value: null);
        }

        var names = await Names([detail.Issue], owner, cancellationToken).ConfigureAwait(false);
        var team = detail.Issue.TeamKey is { } teamKey && names.Teams.TryGetValue(teamKey, out var found) ? found : null;
        return Results.Success<LinearIssueDetailFacts?>(new LinearIssueDetailFacts(Row(detail.Issue, names), detail.Issue.Description ?? string.Empty, detail.Removed, [.. detail.Comments.Select(c => Comment(c, team))]));
    }

    private async ValueTask<Result<(ContextScope Caller, ContextScope Owner)>> Scopes(CancellationToken cancellationToken)
    {
        var caller = await scopes.Read(owner: false, cancellationToken).ConfigureAwait(false);
        var owner = await scopes.Read(owner: true, cancellationToken).ConfigureAwait(false);
        return caller.IsError ? caller.Error : owner.IsError ? owner.Error : (caller.Value, owner.Value);
    }

    private static LinearCommentView Comment(LinearCommentRow comment, LinearTeamLookups? team)
    {
        var member = comment.AuthorId is { } id ? team?.Members.FirstOrDefault(m => m.Id == id) : null;
        var author = comment.AuthorId is { } who ? new LinearPersonView(who.Value, member?.DisplayName ?? member?.Name ?? who.Value) : new LinearPersonView(string.Empty, "Unknown");
        return new LinearCommentView(comment.Id.Value, author, comment.CreatedAt, comment.Body, comment.Sindri);
    }

    private static LinearFeedView Feed(Aw.Context.Core.Sync.SourceProgress row)
    {
        var failing = row.LastCode is { } code && !string.Equals(code, TimeCap, StringComparison.Ordinal);
        return failing
            ? new LinearFeedView("failing", row.LastSync, row.LastCode, Reread: null)
            : row.ReReading is { } stored ? new LinearFeedView("rereading", row.LastSync, Code: null, new LinearRereadView(stored, Total: null)) : new LinearFeedView("ok", row.LastSync, Code: null, Reread: null);
    }

    private static LinearIssueCursor? Cursor(LinearIssuePosition? after) =>
        after is not null && RecordKey.TryFrom(after.Record, out var key) ? new LinearIssueCursor(after.UpdatedAt, key) : null;

    private static bool Unreadable(string? text) => text is not null && !ExternalId.TryFrom(text, out _);

    private static ExternalId? Id(string? text) => text is not null && ExternalId.TryFrom(text, out var id) ? id : null;

    private async ValueTask<Lookups> Names(IReadOnlyList<LinearIssueRow> issues, ContextScope owner, CancellationToken cancellationToken)
    {
        Dictionary<string, LinearTeamLookups> teams = new(StringComparer.Ordinal);
        foreach (var team in issues.Select(static i => i.TeamKey).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var lookups = await engine.IssueLookups(team, owner, cancellationToken).ConfigureAwait(false);
            if (lookups.IsSuccess && lookups.Value is { } found)
            {
                teams[team] = found;
            }
        }

        Dictionary<string, string> projects = new(StringComparer.Ordinal);
        if (issues.Any(static i => i.Fields.ProjectId is not null))
        {
            var catalog = await engine.Projects(new ProjectFactsRequest([]), owner, cancellationToken).ConfigureAwait(false);
            foreach (var project in catalog.IsSuccess ? catalog.Value.Catalog : [])
            {
                projects[project.Key.Value] = project.Title;
            }
        }

        return new Lookups(teams, projects);
    }

    private static LinearIssueRowView Row(LinearIssueRow issue, Lookups names)
    {
        var f = issue.Fields;
        var team = issue.TeamKey is { } key && names.Teams.TryGetValue(key, out var found) ? found : null;
        return new LinearIssueRowView(
            f.Identifier,
            issue.Key.Value,
            issue.Url,
            issue.Title,
            new LinearTeamView(f.TeamId?.Value ?? string.Empty, issue.TeamKey ?? string.Empty),
            State(issue, team),
            f.Priority,
            Assignee(f, team),
            f.ProjectId is { } projectId ? new LinearNamedView(projectId.Value, names.Projects.GetValueOrDefault(ProjectPrefix + projectId.Value, projectId.Value)) : null,
            Cycle(f, team),
            Labels(f, team),
            f.UpdatedAt,
            Link: null,
            Pr: null);
    }

    private static List<LinearLabelView> Labels(LinearIssueFields f, LinearTeamLookups? team) =>
        [.. f.LabelIds.Select(id =>
        {
            var label = team?.Labels.FirstOrDefault(x => x.Id == id);
            return new LinearLabelView(id.Value, label?.Name ?? id.Value, label?.Color);
        })];

    private static LinearStateView State(LinearIssueRow issue, LinearTeamLookups? team)
    {
        var f = issue.Fields;
        var state = f.StateId is { } id ? team?.States.FirstOrDefault(s => s.Id == id) : null;
        return new LinearStateView(f.StateId?.Value ?? string.Empty, First(state?.Name, issue.StateRaw, f.StateType), First(f.StateType, state?.Type, "unstarted"));
    }

    private static string First(params string?[] options) => options.FirstOrDefault(static o => o is not null) ?? string.Empty;

    private static LinearPersonView? Assignee(LinearIssueFields f, LinearTeamLookups? team)
    {
        if (f.AssigneeId is not { } id)
        {
            return null;
        }

        var member = team?.Members.FirstOrDefault(m => m.Id == id);
        return new LinearPersonView(id.Value, member?.DisplayName ?? member?.Name ?? id.Value);
    }

    // why: the lookups hold the current cycle, the next and the last three; an older cycle keeps its id and a bare "Cycle" for a name
    private static LinearCycleView? Cycle(LinearIssueFields f, LinearTeamLookups? team)
    {
        if (f.CycleId is not { } id)
        {
            return null;
        }

        var cycle = team?.Cycles.FirstOrDefault(c => c.Id == id);
        return cycle is null ? new LinearCycleView(id.Value, 0, "Cycle") : new LinearCycleView(id.Value, cycle.Number, cycle.Name ?? string.Create(CultureInfo.InvariantCulture, $"Cycle {cycle.Number}"));
    }

    private sealed record Lookups(Dictionary<string, LinearTeamLookups> Teams, Dictionary<string, string> Projects);
}
