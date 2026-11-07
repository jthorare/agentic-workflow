using Aw.Context.Core.Audience;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;
using Aw.Context.Features.GetProjects;
using Aw.Context.Features.MapHeads;
using Aw.Context.Features.ReadIssues;
using Aw.Context.Features.RelinkPinAliases;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The small calls behind the facade that watchers and pages make: where the mapped repos stand, the stored projects, the pin-alias relink, the stored Linear issues and the open pull requests.</summary>
internal sealed class ContextReads(
    IService<MapHeadsRequest, IReadOnlyList<RepoHead>> heads,
    IService<GetProjectFactsRequest, ProjectFactsAnswer> projects,
    IService<RelinkPinAliasesRequest, RelinkPinAliasesResult> relink,
    IContextLibrary library,
    IssueReads issues,
    IRecordStore records,
    LibraryHost? libraryHost = null)
{
    /// <summary>The design-doc library's reads.</summary>
    public IContextLibrary Library { get; } = library;

    public async ValueTask<Result<IReadOnlyList<StoredRecord>>> OpenPulls(AudienceScope audience, CancellationToken cancellationToken)
    {
        const int pageSize = 500;
        var pulls = new List<StoredRecord>();
        RecordKey? after = null;
        while (true)
        {
            var page = await records.List(new RecordFilter(SourceId.From("github"), Repo: null, RecordKind.Pr, States: [RecordState.Open, RecordState.Draft]), after, pageSize, cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            pulls.AddRange(page.Value.Where(r => audience.Allows(r.Audience, r.Key)));
            if (page.Value.Count < pageSize)
            {
                return pulls;
            }

            after = page.Value[^1].Key;
        }
    }

    public ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(ContextScope scope, CancellationToken cancellationToken) => heads.Handle(new MapHeadsRequest(scope), cancellationToken);

    public ValueTask<Result<RelinkPinAliasesResult>> RelinkPinAliases(ContextScope scope, CancellationToken cancellationToken) => relink.Handle(new RelinkPinAliasesRequest(scope), cancellationToken);

    public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken) =>
        projects.Handle(new GetProjectFactsRequest(request, scope), cancellationToken);

    public ValueTask<Result<LinearIssuePage>> IssueList(LinearIssueFilter filter, LinearIssueCursor? after, ContextScope scope, CancellationToken cancellationToken) =>
        issues.List(filter, after, scope, cancellationToken);

    public ValueTask<Result<LinearIssueRow?>> IssueGet(RecordKey key, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Get(key, scope, cancellationToken);

    public ValueTask<Result<LinearIssueDetail?>> IssueGet(LinearIssueKey key, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Get(key, scope, cancellationToken);

    public ValueTask<Result<LinearIssueCounts>> IssueCounts(LinearIssueFilter filter, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Counts(filter, scope, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LinearCommentRow>>> IssueComments(RecordKey key, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Comments(key, scope, cancellationToken);

    public ValueTask<Result<LinearTeamLookups?>> IssueLookups(string team, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Lookups(team, scope, cancellationToken);

    public ValueTask<Result<string?>> WorkspaceUrlKey(ContextScope scope, CancellationToken cancellationToken) =>
        issues.WorkspaceUrlKey(scope, cancellationToken);

    public ValueTask<Result<LinearSince>> IssueSince(DateTimeOffset marker, ExternalId viewer, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Since(marker, viewer, scope, cancellationToken);

    // why: the library's path rules need the machine's home folders, which the host passes in (Core reads no environment); a host that gave none checks the shape only
    public Result<ContextSettings> CheckLibrary(ContextSettings settings) =>
        settings.Library is null || libraryHost is null ? settings : LibraryPaths.Resolve(settings.Library, libraryHost).Map(_ => settings);
}
