using Aw.Kernel;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Where one design doc comes from.</summary>
public interface ILibraryDocFacts
{
    /// <summary>The doc with that id the caller may read.</summary>
    /// <param name="doc">The doc's front matter id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The doc at the lowest path when several scopes share the id, null when none does, or why the library couldn't be read.</returns>
    public ValueTask<Result<LibraryDocView?>> Find(string doc, CancellationToken cancellationToken);
}
