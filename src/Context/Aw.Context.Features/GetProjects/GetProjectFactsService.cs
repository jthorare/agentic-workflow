using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Core.Sync;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetProjects;

/// <summary>
/// The stored projects with their open-issue counts, the owner and where the feeds stand (projects page spec §6.1). It reads the record store only: the audience rule is the one the query's lexical leg applies
/// (the profile's repos and Linear teams; a session sees only its own repo's), counts are SQL in the store, and nothing calls Linear.
/// 'FirstReadDone' is the projects feed having moved past its Recent phase; 'Counting' is the Linear issue feed not having (its one-time re-read for project links, spec §4.3), the same for every project.
/// </summary>
internal sealed class GetProjectFactsService(IRecordStore store, ISourceIdentityStore identities, IIndexSync sync) : IService<GetProjectFactsRequest, ProjectFactsAnswer>
{
    private const string UserPrefix = "linear:user/";

    public async ValueTask<Result<ProjectFactsAnswer>> Handle(GetProjectFactsRequest request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        var settings = SettingsReader.Read(scope.Settings);
        if (settings.IsError)
        {
            return settings.Error;
        }

        var audience = await AudienceOf(settings.Value, scope, cancellationToken).ConfigureAwait(false);
        var rows = audience.IsError ? audience.Error : await store.ProjectFacts(LinearSourceIds.Projects, new RecordAudience(audience.Value.Allowed, audience.Value.Records), cancellationToken).ConfigureAwait(false);
        var identity = rows.IsError ? rows.Error : await identities.Read(LinearSourceIds.Identity, cancellationToken).ConfigureAwait(false);
        var progress = identity.IsError ? identity.Error : await sync.Progress(scope, cancellationToken).ConfigureAwait(false);
        if (progress.IsError)
        {
            return progress.Error;
        }

        var firstReadDone = progress.Value.Any(p => p.Source == LinearSourceIds.Projects && p.Usable);
        var counting = progress.Value.Any(p => p.Source == LinearSourceIds.Issues && !p.Usable);
        var owner = identity.Value is { } known ? new UserRef(Id(known.UserRef), known.Name) : (UserRef?)null;
        var catalog = rows.Value.Select(row => Fact(row, owner, counting)).ToList();
        var byKey = catalog.ToDictionary(f => f.Key);
        var attached = request.Request.Attached.Distinct().Where(byKey.ContainsKey).Select(k => byKey[k]).ToList();
        return new ProjectFactsAnswer(owner, attached, catalog, settings.Value.LinearTeams, firstReadDone);
    }

    private static ProjectFact Fact(ProjectRow row, UserRef? owner, bool counting)
    {
        var lead = row.LeadRef is { } reference ? new UserRef(Id(reference), row.LeadName) : (UserRef?)null;
        var activity = row.NewestIssueAt is { } issue && issue > row.UpdatedAt ? issue : row.UpdatedAt;
        return new ProjectFact(
            row.Key, row.Title, Description: null, row.Url, row.StateRaw, row.Health, row.DueAt is { } due ? new DateTimeOffset(due.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
            lead, LeadIsOwner: lead is { } l && owner is { } o && l.Id.Equals(o.Id), row.UpdatedAt, row.ClosedAt, row.OpenIssues, OpenPrs: null, activity, Repos: null, counting);
    }

    private static ExternalId Id(string userRef) => ExternalId.From(userRef.StartsWith(UserPrefix, StringComparison.Ordinal) ? userRef[UserPrefix.Length..] : userRef);

    // why: a session reads only its own repo's audiences, a human every repo of the profile; the policy gives a repo the profile doesn't name nothing
    private static async ValueTask<Result<AudienceScope>> AudienceOf(ContextSettings settings, ContextScope scope, CancellationToken cancellationToken)
    {
        var names = scope.Repos.Select(r => r.Name).ToList();
        var policy = new ProfileAudiencePolicy(settings, names);
        IEnumerable<ContextRepoName> asked = scope.Caller.Kind == ContextCallerKind.Session ? (scope.Caller.Repo is { } own ? [own] : []) : names;
        var allowed = new HashSet<AudienceKey>();
        foreach (var repo in asked)
        {
            var one = await policy.For(repo, cancellationToken).ConfigureAwait(false);
            if (one.IsError)
            {
                return one.Error;
            }

            allowed.UnionWith(one.Value.Allowed);
        }

        return new AudienceScope(allowed);
    }
}
