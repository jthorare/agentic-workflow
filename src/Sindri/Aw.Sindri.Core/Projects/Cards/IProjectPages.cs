using Aw.Kernel;

namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>The Projects page as ListProjects and GetProject read it (projects page spec §6.2 steps 1-4): the port both slices take, because a slice never uses another's types.</summary>
public interface IProjectPages
{
    /// <summary>Reads the owner's views and Context's facts and builds the page. Context failing is not a failure: the page then holds attached and hand-made cards by name only.</summary>
    /// <param name="all">Whether every finished project shows, not just the last 14 days.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The page, or the error of reading the owner's views.</returns>
    public ValueTask<Result<ProjectsPage>> Read(bool all, CancellationToken cancellationToken);
}
