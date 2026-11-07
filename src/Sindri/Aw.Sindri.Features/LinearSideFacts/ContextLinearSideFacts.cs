using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.LinearSideFacts;

/// <summary>
/// The side-facts port over Context's IContextIssues (Linear hub spec 2026-11-04 §4.2, §4.3), under the current caller's scope (mapped as ContextProjectFacts maps it; slices don't share types). Reads stored data only.
/// The strip's viewer is the owner of the Linear key, as Context's project facts name it; actor and state names come from the team's stored lookups, the title from the stored issue.
/// </summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade.</param>
/// <param name="clock">Decides which cycle is running.</param>
internal sealed class ContextLinearSideFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine, TimeProvider clock) : ILinearSideFacts
{
    public async ValueTask<Result<LinearLookupsResponse?>> Lookups(string team, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var found = await engine.IssueLookups(team, scope.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        if (found.Value is not { } stored)
        {
            return Results.Success<LinearLookupsResponse?>(value: null);
        }

        var now = clock.GetUtcNow();
        // why: a picked team never synced has no id, name or sync time (and empty lists); inactive members stay, because older issues are still assigned to them
        return new LinearLookupsResponse(
            new LinearTeamRef(stored.TeamId?.Value, stored.TeamKey, stored.Name),
            stored.SyncedAt,
            [.. stored.States.Select(static s => new LinearStateOption(s.Id.Value, s.Name, s.Type, s.Position))],
            [.. stored.Members.Select(static m => new LinearMemberOption(m.Id.Value, m.Name, m.Active))],
            [.. stored.Labels.Select(static l => new LinearLabelOption(l.Id.Value, l.Name, l.Color))],
            [.. stored.Cycles.Select(c => new LinearCycleOption(c.Id.Value, c.Number, c.Name ?? $"Cycle {c.Number.ToString(CultureInfo.InvariantCulture)}", c.StartsAt, c.EndsAt, c.StartsAt <= now && now < c.EndsAt))]);
    }

    public async ValueTask<Result<bool>> TeamsPicked(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        ProfileRepoEntry[] repos = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, repos);
        return settings.IsError ? settings.Error : settings.Value.LinearTeams.Count > 0 || settings.Value.Linear?.Teams.Count > 0;
    }

    public async ValueTask<Result<IReadOnlyList<LinearSinceItem>?>> Since(DateTimeOffset after, CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        // why: Sindri reads Context through its facade only, so the viewer is the project facts' owner, as the issue list finds it
        var facts = await engine.Projects(new ProjectFactsRequest([]), scope.Value, cancellationToken).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        if (facts.Value.Owner is not { } who)
        {
            return Results.Success<IReadOnlyList<LinearSinceItem>?>(value: null);
        }

        var since = await engine.IssueSince(after, who.Id, scope.Value, cancellationToken).ConfigureAwait(false);
        return since.IsError ? since.Error : await Items(since.Value, scope.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<IReadOnlyList<LinearSinceItem>?>> Items(LinearSince since, ContextScope scope, CancellationToken cancellationToken)
    {
        var teams = new Dictionary<string, LinearTeamLookups?>(StringComparer.Ordinal);
        var titles = new Dictionary<RecordKey, string>();
        var items = new List<LinearSinceItem>();
        foreach (var change in since.Changes)
        {
            var item = await Change(change, teams, titles, scope, cancellationToken).ConfigureAwait(false);
            if (item.IsError)
            {
                return item.Error;
            }

            items.Add(item.Value);
        }

        foreach (var mention in since.Mentions)
        {
            var item = await Mention(mention, teams, titles, scope, cancellationToken).ConfigureAwait(false);
            if (item.IsError)
            {
                return item.Error;
            }

            items.Add(item.Value);
        }

        return Results.Success<IReadOnlyList<LinearSinceItem>?>([.. items.OrderBy(static i => i.At)]);
    }

    private async ValueTask<Result<LinearSinceItem>> Change(LinearSinceChange change, Dictionary<string, LinearTeamLookups?> teams, Dictionary<RecordKey, string> titles, ContextScope scope, CancellationToken cancellationToken)
    {
        var lookups = await Team(teams, change.Identifier, scope, cancellationToken).ConfigureAwait(false);
        var title = await Title(titles, change.Issue, scope, cancellationToken).ConfigureAwait(false);
        if (lookups.IsError || title.IsError)
        {
            return lookups.IsError ? lookups.Error : title.Error;
        }

        var assigned = string.Equals(change.Field, "assignee", StringComparison.Ordinal);
        // why: Context keeps no actor for an assignment or a state move, so those items carry none
        return new LinearSinceItem(assigned ? "assigned" : "state", change.Identifier, title.Value, Actor: null, change.At,
            assigned ? null : StateName(lookups.Value, change.Old), assigned ? null : StateName(lookups.Value, change.New));
    }

    private async ValueTask<Result<LinearSinceItem>> Mention(LinearSinceMention mention, Dictionary<string, LinearTeamLookups?> teams, Dictionary<RecordKey, string> titles, ContextScope scope, CancellationToken cancellationToken)
    {
        var lookups = await Team(teams, mention.Identifier, scope, cancellationToken).ConfigureAwait(false);
        var title = await Title(titles, mention.Issue, scope, cancellationToken).ConfigureAwait(false);
        if (lookups.IsError || title.IsError)
        {
            return lookups.IsError ? lookups.Error : title.Error;
        }

        return new LinearSinceItem("mentioned", mention.Identifier, title.Value, Author(lookups.Value, mention.Comment.AuthorId), mention.Comment.CreatedAt, From: null, To: null);
    }

    private async ValueTask<Result<LinearTeamLookups?>> Team(Dictionary<string, LinearTeamLookups?> cache, string identifier, ContextScope scope, CancellationToken cancellationToken)
    {
        var dash = identifier.LastIndexOf('-');
        var key = dash > 0 ? identifier[..dash] : identifier;
        if (cache.TryGetValue(key, out var known))
        {
            return known;
        }

        var read = await engine.IssueLookups(key, scope, cancellationToken).ConfigureAwait(false);
        if (!read.IsError)
        {
            cache[key] = read.Value;
        }

        return read;
    }

    private async ValueTask<Result<string>> Title(Dictionary<RecordKey, string> cache, RecordKey issue, ContextScope scope, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(issue, out var known))
        {
            return known;
        }

        var read = await engine.IssueGet(issue, scope, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return cache[issue] = read.Value?.Title ?? issue.Value;
    }

    private static string? StateName(LinearTeamLookups? lookups, ExternalId? id) =>
        id is { } state ? lookups?.States.FirstOrDefault(s => s.Id.Value == state.Value)?.Name : null;

    private static LinearPerson? Author(LinearTeamLookups? lookups, ExternalId? id) =>
        id is { } author ? new LinearPerson(author.Value, lookups?.Members.FirstOrDefault(m => m.Id.Value == author.Value)?.Name ?? author.Value) : null;

    private async ValueTask<Result<ContextScope>> Scope(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        ProfileRepoEntry[] repos = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        return new ContextScope(loaded.Profile.Context, ScopeRules.Usable(repos), Caller(callers.Current));
    }

    private static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, ContextClient.Http),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };
}
