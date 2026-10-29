using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListLinearProjects;

/// <summary>The From Linear tab (projects page spec §3.2, §6.2): the stored catalog minus what the owner leads, has attached or is finished, by name.</summary>
/// <param name="views">The 'project-attached' view.</param>
/// <param name="facts">Context's stored Linear projects.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class ListLinearProjectsService(IViewReader views, IProjectFacts facts, IProjectorStatus status, ICallerContextAccessor callers) : IService<ListLinearProjectsRequest, ListLinearProjectsResponse>
{
    public async ValueTask<Result<ListLinearProjectsResponse>> Handle(ListLinearProjectsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("the From Linear tab needs a caller");
        }

        var attached = await views.List<ProjectAttachedRow>(ViewNames.ProjectAttached, cancellationToken).ConfigureAwait(false);
        if (attached.IsError)
        {
            return attached.Error;
        }

        var keys = attached.Value.Select(static r => r.Linear).OfType<string>().ToList();
        var snapshot = await facts.Read(keys, cancellationToken).ConfigureAwait(false);

        // why: Context failing is not a failure of the tab: it shows the line in place of the rows, as the page does for its cards
        if (snapshot.IsError)
        {
            return new ListLinearProjectsResponse(status.Current.AsOf, snapshot.Error.Message.Split('\n', 2)[0].Trim(), []);
        }

        var taken = keys.ToHashSet(StringComparer.Ordinal);
        var rows = snapshot.Value.Catalog
            .Where(p => !p.LeadIsOwner && !taken.Contains(p.Key) && !Finished(p))
            .Select(Choice)
            .OrderBy(static c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static c => c.Key, StringComparer.Ordinal)
            .ToList();
        return new ListLinearProjectsResponse(status.Current.AsOf, Error: null, rows);
    }

    private static bool Finished(LinearProjectFact fact) =>
        fact.ClosedAt is not null || ProjectCards.StateOf(fact.StateRaw) is ProjectState.Done or ProjectState.Canceled;

    private static LinearProjectChoice Choice(LinearProjectFact fact) =>
        new(fact.Key, fact.Title, fact.Lead?.Name, ProjectCards.StateOf(fact.StateRaw), fact.OpenIssues, Link(fact.Url));

    private static string? Link(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? Permalinks.Allowed(parsed)?.AbsoluteUri : null;
}
