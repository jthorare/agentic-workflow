using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Linear;

/// <summary>The source ids Linear's data goes under: the owner's identity is stored under <see cref="Identity"/>, the projects feed ticks as <see cref="Projects"/>.</summary>
public static class LinearSourceIds
{
    /// <summary>'linear': the source whose credential the 'source_identity' row describes.</summary>
    public static readonly SourceId Identity = SourceId.From("linear");

    /// <summary>'linear-projects': the projects feed, which has its own cursor, lock and progress row.</summary>
    public static readonly SourceId Projects = SourceId.From("linear-projects");

    /// <summary>The record reference of a Linear user, what a 'led-by' link points at.</summary>
    /// <param name="id">The user's id in Linear.</param>
    /// <returns>'linear:user/&lt;id&gt;'.</returns>
    public static string UserRef(ExternalId id) => "linear:user/" + id.Value;
}
