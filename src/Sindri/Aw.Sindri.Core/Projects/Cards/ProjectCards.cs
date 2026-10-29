namespace Aw.Sindri.Core.Projects.Cards;

/// <summary>
/// Builds the Projects page from the owner state in views.db and Context's facts (projects page spec §6.2 steps 3-4), pure: led minus hidden, plus attached;
/// overrides applied; ordered (§3); the page sentence (§3.1); on a Context error, attached and hand-made cards by name only.
/// </summary>
public static class ProjectCards
{
    /// <summary>The finished fold keeps a project this long after it finished, unless 'all' is asked.</summary>
    public static readonly TimeSpan FinishedWindow = TimeSpan.FromDays(14);

    /// <summary>Builds the page.</summary>
    /// <param name="inputs">The owner state, Context's facts and the sources' state.</param>
    /// <param name="now">The clock's now, for the 14-day window and "N min ago".</param>
    /// <param name="all">Whether every finished project shows, not just the last 14 days.</param>
    /// <returns>The page.</returns>
    public static ProjectsPage Build(ProjectPageInputs inputs, DateTimeOffset now, bool all) =>
        throw new NotSupportedException("Ph4: ProjectCards.Build");
}
