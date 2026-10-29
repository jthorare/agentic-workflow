using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetProject;

/// <summary>One project (projects page spec §6.2): the page built as ListProjects builds it (with every finished one), then the card the id names; SND-PROJECT-404 when none does.</summary>
/// <param name="pages">Reads the views and the facts and builds the page.</param>
/// <param name="views">The 'project-attached' view, to map a typed Linear key to the attached project that holds it.</param>
/// <param name="status">How fresh the views are.</param>
/// <param name="callers">The caller; a read needs one.</param>
internal sealed class GetProjectService(IProjectPages pages, IViewReader views, IProjectorStatus status, ICallerContextAccessor callers) : IService<GetProjectRequest, GetProjectResponse>
{
    private const string LinearLabel = "Linear";

    public async ValueTask<Result<GetProjectResponse>> Handle(GetProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is null)
        {
            return SindriErrors.AuthRefused.With("a project needs a caller");
        }

        var typed = request.Project?.Trim() ?? string.Empty;
        var read = await pages.Read(all: true, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var attached = await views.List<ProjectAttachedRow>(ViewNames.ProjectAttached, cancellationToken).ConfigureAwait(false);
        if (attached.IsError)
        {
            return attached.Error;
        }

        var page = read.Value;
        var key = KeyOf(typed, attached.Value);
        var card = Find(page.Projects, key) ?? Find(page.Finished, key);
        var hidden = false;
        if (card is null)
        {
            card = Find(page.Hidden, key);
            hidden = card is not null;
        }

        return card is null
            ? SindriErrors.ProjectNotFound.With($"No project {typed}. `sindri projects` lists them.")
            : new GetProjectResponse(status.Current.AsOf, page.Viewer, page.Freshness, card, hidden);
    }

    // why: an attached project's card answers to its prj_ id, so a Linear key the owner typed is mapped through the attached row that holds it
    private static Func<ProjectCard, bool> KeyOf(string typed, IReadOnlyList<ProjectAttachedRow> attached)
    {
        if (typed.StartsWith("prj_", StringComparison.Ordinal))
        {
            return card => string.Equals(card.Key, typed, StringComparison.Ordinal);
        }

        var parsed = LinearProjectRefs.Parse(typed);
        if (parsed.IsError)
        {
            return _ => false;
        }

        if (parsed.Value.Key is { } linear)
        {
            var row = attached.FirstOrDefault(r => string.Equals(r.Linear, linear, StringComparison.Ordinal));
            return card => string.Equals(card.Key, linear, StringComparison.Ordinal) || (row is not null && string.Equals(card.Key, row.Id.Value, StringComparison.Ordinal));
        }

        var slug = parsed.Value.Slug!;   // why: Parse gives a key or a slug, never neither
        return card => card.Links.Any(l => string.Equals(l.Label, LinearLabel, StringComparison.Ordinal) && l.Url.EndsWith(slug, StringComparison.OrdinalIgnoreCase));
    }

    private static ProjectCard? Find(IReadOnlyList<ProjectCard> cards, Func<ProjectCard, bool> matches) => cards.FirstOrDefault(matches);
}
