using Aw.Kernel;

namespace Aw.Context.Core.Records;

/// <summary>Asks one source who its credential belongs to (Linear's 'viewer'); the answer is what the 'source_identity' row holds.</summary>
public interface ISourceViewer
{
    /// <summary>Asks the source once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The user's id in the source and display name, or the source's error (down, refused key).</returns>
    public ValueTask<Result<SourceViewer>> Viewer(CancellationToken cancellationToken);
}
