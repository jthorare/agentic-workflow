using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>The source ids Linear's data goes under: the issue feed ticks as <see cref="Issues"/>, the projects feed as <see cref="Projects"/>, and the owner's identity is stored under <see cref="Identity"/>.</summary>
public static class LinearSourceIds
{
    /// <summary>'linear': the issue feed's source id.</summary>
    public static readonly SourceId Issues = SourceId.From("linear");

    /// <summary>'linear': the source whose credential the 'source_identity' row describes (the issue feed's id).</summary>
    public static readonly SourceId Identity = Issues;

    /// <summary>'linear-projects': the projects feed, which has its own cursor, lock and progress row (projects page spec §4.1).</summary>
    public static readonly SourceId Projects = SourceId.From("linear-projects");

    /// <summary>The record reference of a Linear user, what a 'led-by' link points at.</summary>
    /// <param name="id">The user's id in Linear.</param>
    /// <returns>'linear:user/&lt;id&gt;'.</returns>
    public static string UserRef(ExternalId id) => "linear:user/" + id.Value;
}
