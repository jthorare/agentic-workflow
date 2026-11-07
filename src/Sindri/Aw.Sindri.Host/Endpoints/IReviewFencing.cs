using Aw.Sindri.Features.GetLibrary;
using Aw.Sindri.Features.ListReviews;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>
/// Writes the Reviews list for a session caller with the forge-sourced text inside the untrusted fence (PR review page spec §10). A port for the same
/// reason as <see cref="IProjectFencing"/>: the fence lives in the MCP namespace, which already depends on this one (ARCH-CYCLE-001).
/// </summary>
internal interface IReviewFencing
{
    /// <summary>The list as a session reads it.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The JSON answer with PR titles, authors and unreadable folders fenced.</returns>
    public IResult List(ListReviewsResponse list);

    /// <summary>The library page as a session reads it.</summary>
    /// <param name="library">The page.</param>
    /// <returns>The JSON answer with file paths and finding texts fenced, and no unreadable folders.</returns>
    public IResult Library(GetLibraryResponse library);
}
