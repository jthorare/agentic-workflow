using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Linking;
using Aw.Context.Core.Projects;
using Aw.Context.Core.Sources;
using Aw.Context.Features.GetProjects;
using Aw.Context.Features.MapHeads;
using Aw.Context.Features.ReadIssues;
using Aw.Context.Features.RelinkPinAliases;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The small calls behind the facade that watchers and pages make: where the mapped repos stand, the stored projects, the pin-alias relink and the stored Linear issues.</summary>
internal sealed class ContextReads(
    IService<MapHeadsRequest, IReadOnlyList<RepoHead>> heads,
    IService<GetProjectFactsRequest, ProjectFactsAnswer> projects,
    IService<RelinkPinAliasesRequest, RelinkPinAliasesResult> relink,
    IContextLibrary library,
    IssueReads issues)
{
    /// <summary>The design-doc library's reads.</summary>
    public IContextLibrary Library { get; } = library;

    public ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(ContextScope scope, CancellationToken cancellationToken) => heads.Handle(new MapHeadsRequest(scope), cancellationToken);

    public ValueTask<Result<RelinkPinAliasesResult>> RelinkPinAliases(ContextScope scope, CancellationToken cancellationToken) => relink.Handle(new RelinkPinAliasesRequest(scope), cancellationToken);

    public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken) =>
        projects.Handle(new GetProjectFactsRequest(request, scope), cancellationToken);

    public ValueTask<Result<LinearIssuePage>> IssueList(LinearIssueFilter filter, LinearIssueCursor? after, ContextScope scope, CancellationToken cancellationToken) =>
        issues.List(filter, after, scope, cancellationToken);

    public ValueTask<Result<LinearIssueRow?>> IssueGet(RecordKey key, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Get(key, scope, cancellationToken);

    public ValueTask<Result<IReadOnlyList<LinearCommentRow>>> IssueComments(RecordKey key, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Comments(key, scope, cancellationToken);

    public ValueTask<Result<LinearTeamLookups?>> IssueLookups(string team, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Lookups(team, scope, cancellationToken);

    public ValueTask<Result<LinearSince>> IssueSince(DateTimeOffset marker, ExternalId viewer, ContextScope scope, CancellationToken cancellationToken) =>
        issues.Since(marker, viewer, scope, cancellationToken);
}
