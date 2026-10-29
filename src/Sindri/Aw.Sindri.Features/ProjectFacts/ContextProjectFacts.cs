using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects.Facts;

namespace Aw.Sindri.Features.ProjectFacts;

/// <summary>
/// The facts port over Context's IContextEngine (projects page spec §6.1): Projects for the stored projects, Progress for the freshness line,
/// both under the current caller's scope (mapped as ContextCalls' ContextScopes maps it; slices don't share types), so the audience rule applies as for any record read. Reads only stored data.
/// Any failure (no approved profile, Context down) is SND-PROJECT-002 carrying the cause's code, the page's "by name only" state.
/// </summary>
/// <param name="approvals">The approved profiles.</param>
/// <param name="callers">The current caller.</param>
/// <param name="engine">Context's facade.</param>
internal sealed class ContextProjectFacts(IApprovedProfile approvals, ICallerContextAccessor callers, IContextEngine engine) : IProjectFacts
{
    private const string UserPrefix = "linear:user/";

    public async ValueTask<Result<ProjectFactsSnapshot>> Read(IReadOnlyList<string> attached, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attached);
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error);
        }

        // why: an attached key that isn't a record key can't be in the store; Context leaves unknown keys out, so this does too
        RecordKey[] keys = [.. attached.Select(static k => RecordKey.TryFrom(k, out var key) ? (RecordKey?)key : null).OfType<RecordKey>()];
        var answer = await engine.Projects(new ProjectFactsRequest(keys), scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return Unavailable(answer.Error);
        }

        var a = answer.Value;
        return new ProjectFactsSnapshot(Person(a.Owner), [.. a.Projects.Select(Fact)], [.. a.Catalog.Select(Fact)], a.Teams, a.FirstReadDone);
    }

    public async ValueTask<Result<IReadOnlyList<ProjectSourceFreshness>>> Freshness(CancellationToken cancellationToken)
    {
        var scope = await Scope(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error);
        }

        var progress = await engine.Progress(scope.Value, cancellationToken).ConfigureAwait(false);
        if (progress.IsError)
        {
            return Unavailable(progress.Error);
        }

        return progress.Value.Where(static p => Shown(p.Source)).Select(static p => new ProjectSourceFreshness(p.Source.Value, p.Usable, p.LastSync, p.LastCode)).ToList();
    }

    private async ValueTask<Result<ContextScope>> Scope(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError || approved.Value is not { } loaded)
        {
            return approved.IsError ? approved.Error : SindriErrors.ProfileNotApproved.With("no profile is approved (fix: sindri profile approve)");
        }

        // why: the projects read needs the audience rule and the repo, not each repo's index globs, so the entries carry name and path only
        ProfileRepoEntry[] repos = [.. (loaded.Profile.Repos ?? []).Select(static r => new ProfileRepoEntry(r.Name, r.Path))];
        return new ContextScope(loaded.Profile.Context, ScopeRules.Usable(repos), Caller(callers.Current));
    }

    private static ContextCaller Caller(CallerContext? caller) => caller switch
    {
        { Class: CallerClass.Human } => new ContextCaller(ContextCallerKind.Human, Repo: null, ContextClient.Http),
        { Class: CallerClass.Session } => new ContextCaller(ContextCallerKind.Session, caller.Repo is { } repo && ContextRepoName.TryFrom(repo.Value, out var name) ? name : null, ContextClient.Http),
        _ => new ContextCaller(ContextCallerKind.Service, Repo: null, ContextClient.Service),
    };

    private static Error Unavailable(Error cause) => SindriErrors.ProjectFactsUnavailable.With($"{cause.Code}: {cause.Message}");

    // why: the page's freshness line names the project feed, the issue feed and GitHub (spec §3.1); other feeds aren't the page's
    private static bool Shown(SourceId source) =>
        string.Equals(source.Value, "linear-projects", StringComparison.Ordinal) || string.Equals(source.Value, "linear", StringComparison.Ordinal)
        || source.Value.StartsWith("github", StringComparison.Ordinal);

    private static LinearProjectFact Fact(ProjectFact f) =>
        new(f.Key.Value, f.Title, f.Description, f.Url, f.StateRaw, f.Health, f.DueAt, Person(f.Lead), f.LeadIsOwner, f.UpdatedAt, f.ClosedAt, f.OpenIssues, f.Counting, f.LastActivity);

    // why: Context's ExternalId is Linear's bare user id; Sindri's ProjectPerson.Ref is the 'linear:user/<id>' form its other records use
    private static ProjectPerson? Person(UserRef? user) => user is { } u ? new ProjectPerson(UserPrefix + u.Id.Value, u.Name) : null;
}
