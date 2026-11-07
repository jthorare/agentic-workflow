using Aw.Kernel;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>Where one design doc comes from.</summary>
internal interface ILibraryDocFacts
{
    /// <summary>The doc with that id the caller may read.</summary>
    /// <param name="id">The doc id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The doc at the lowest path when several scopes share the id, null when none does, or why the library couldn't be read.</returns>
    public ValueTask<Result<LibraryDocView?>> Find(string id, CancellationToken cancellationToken);
}
