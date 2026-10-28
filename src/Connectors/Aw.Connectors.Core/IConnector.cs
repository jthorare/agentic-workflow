using Aw.Kernel;

namespace Aw.Connectors.Core;

/// <summary>A source of records read in pages from a cursor (owned index spec §5.2). The Host adapts it to Context's record feed.</summary>
public interface IConnector
{
    /// <summary>What this connector is.</summary>
    /// <returns>The description.</returns>
    public ConnectorDescription Describe();

    /// <summary>Reads the records changed since <paramref name="after"/>.</summary>
    /// <param name="after">Where to start, or null for a first run.</param>
    /// <param name="limit">Stop after the page that brings the run to this many records.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The page, or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<ConnectorPage>> Pull(SourceCursor? after, int limit, CancellationToken cancellationToken);

    /// <summary>Reads the current version of the records named by <paramref name="keys"/>.</summary>
    /// <param name="keys">Record keys.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>Version by key; a key the source no longer has is absent.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, string>>> Versions(IReadOnlyList<string> keys, CancellationToken cancellationToken);

    /// <summary>Checks the token and the reach with one cheap call.</summary>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>Success, or the error that says what is wrong.</returns>
    public ValueTask<Result<Unit>> Probe(CancellationToken cancellationToken);
}
