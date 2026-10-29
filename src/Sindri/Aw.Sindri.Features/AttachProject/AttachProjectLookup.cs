using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.AttachProject;

/// <summary>
/// What an attach decides on (projects page spec §5.3): the attached projects from the views, and for a typed Linear url or key the stored project it
/// names (SND-PROJECT-003 when the store holds none) and whether the owner leads it.
/// </summary>
/// <param name="views">The attached projects.</param>
/// <param name="facts">The stored Linear projects.</param>
internal sealed class AttachProjectLookup(IViewReader views, IProjectFacts facts)
{
    /// <summary>Reads the attached projects and resolves the typed Linear project.</summary>
    /// <param name="typed">What the owner typed, or null for a hand-made project (no facts are read).</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The project's key (null for a hand-made one), whether the owner leads it, and the attached rows; or the error.</returns>
    public async ValueTask<Result<AttachContext>> Read(LinearProjectRef? typed, CancellationToken cancellationToken)
    {
        var attached = await views.List<ProjectAttachedRow>(ViewNames.ProjectAttached, cancellationToken).ConfigureAwait(false);
        if (attached.IsError)
        {
            return attached.Error;
        }

        if (typed is null)
        {
            return new AttachContext(Key: null, Led: false, attached.Value);
        }

        var read = await facts.Read([], cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var snapshot = read.Value;
        var found = snapshot.Catalog.FirstOrDefault(fact => Names(fact, typed));
        return found is null
            ? SindriErrors.ProjectNotSynced.With($"Sindri doesn't sync that project. It syncs these Linear teams: {string.Join(", ", snapshot.Teams)}. Add the team to `context.sources.linear.teams`.")
            : new AttachContext(found.Key, found.LeadIsOwner, attached.Value);
    }

    private static bool Names(LinearProjectFact fact, LinearProjectRef typed) =>
        typed.Key is { } key
            ? string.Equals(fact.Key, key, StringComparison.Ordinal)
            : fact.Url is { } url && LinearProjectRefs.Parse(url) is var parsed && !parsed.IsError && string.Equals(parsed.Value.Slug, typed.Slug, StringComparison.OrdinalIgnoreCase);
}
