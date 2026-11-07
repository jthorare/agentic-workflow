using Aw.Sindri.Features.GetLibraryDoc;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Writes one design doc for a session caller with the doc's text inside the untrusted fence (PR review page spec §10). A port for the same reason as <see cref="IReviewFencing"/>.</summary>
internal interface ILibraryDocFencing
{
    /// <summary>The doc as a session reads it.</summary>
    /// <param name="doc">The answer.</param>
    /// <returns>The JSON answer with the title, path and error text fenced.</returns>
    public IResult Doc(GetLibraryDocResponse doc);
}
