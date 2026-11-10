using Aw.Kernel;

namespace Aw.Sindri.Core.Toolkits;

/// <summary>Where toolkit calls are kept (the 'toolkit_call' table of ledger.db). Rows hold scrubbed, capped text only.</summary>
public interface IToolkitCalls
{
    /// <summary>Stores one call.</summary>
    /// <param name="call">The call, already scrubbed and capped.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit.</returns>
    public ValueTask<Result<Unit>> Add(ToolkitCall call, CancellationToken cancellationToken);

    /// <summary>Lists calls, newest first.</summary>
    /// <param name="toolkit">Only this toolkit's calls, or all when null.</param>
    /// <param name="limit">The most calls to return.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The calls.</returns>
    public ValueTask<Result<IReadOnlyList<ToolkitCall>>> List(string? toolkit, int limit, CancellationToken cancellationToken);

    /// <summary>Deletes every call that started before a moment.</summary>
    /// <param name="before">The cut-off.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>How many rows went.</returns>
    public ValueTask<Result<int>> DeleteBefore(DateTimeOffset before, CancellationToken cancellationToken);
}
