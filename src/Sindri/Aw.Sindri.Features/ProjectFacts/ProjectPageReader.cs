using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ProjectFacts;

/// <summary>
/// The one read behind ListProjects and GetProject (their <see cref="IProjectPages" />) (projects page spec §6.2 steps 1-4): the owner's two views, Context's facts for the attached
/// Linear keys and their freshness, whether the approved profile configures Linear, then <see cref="ProjectCards.Build"/>. Context failing is not a
/// failure here: the page builds from the owner's state alone. Only the views failing is.
/// </summary>
/// <param name="views">The 'project-attached' and 'project-hidden' views.</param>
/// <param name="facts">Context's stored Linear projects.</param>
/// <param name="approvals">The approved profile.</param>
/// <param name="engine">The context engine, which checks the profile's context section.</param>
/// <param name="clock">The clock.</param>
internal sealed class ProjectPageReader(IViewReader views, IProjectFacts facts, IApprovedProfile approvals, IContextEngine engine, TimeProvider clock) : IProjectPages
{
    public async ValueTask<Result<ProjectsPage>> Read(bool all, CancellationToken cancellationToken)
    {
        var attached = await views.List<ProjectAttachedRow>(ViewNames.ProjectAttached, cancellationToken).ConfigureAwait(false);
        if (attached.IsError)
        {
            return attached.Error;
        }

        var hidden = await views.List<ProjectHiddenRow>(ViewNames.ProjectHidden, cancellationToken).ConfigureAwait(false);
        if (hidden.IsError)
        {
            return hidden.Error;
        }

        var keys = attached.Value.Select(r => r.Linear).OfType<string>().ToList();
        var snapshot = await facts.Read(keys, cancellationToken).ConfigureAwait(false);
        var freshness = await facts.Freshness(cancellationToken).ConfigureAwait(false);
        var (linear, tokenMissing) = await LinearState(cancellationToken).ConfigureAwait(false);
        return ProjectCards.Build(new ProjectPageInputs(attached.Value, hidden.Value, snapshot, freshness, linear, tokenMissing), clock.GetUtcNow(), all);
    }

    // why: a profile that can't be read or checked reads as "no Linear" here; doctor and setup report that problem, the page only picks its sentence
    private async ValueTask<(bool Configured, bool TokenMissing)> LinearState(CancellationToken cancellationToken)
    {
        var latest = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError || latest.Value is not { } loaded)
        {
            return (false, false);
        }

        ProfileRepoEntry[] entries = [.. (loaded.Profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path))];
        var settings = engine.CheckSettings(loaded.Profile.Context, entries);
        if (settings.IsError)
        {
            return (false, false);
        }

        var teamsOnly = settings.Value.Linear is null && settings.Value.LinearTeams.Count > 0;
        return (settings.Value.Linear is not null || teamsOnly, teamsOnly);
    }
}
