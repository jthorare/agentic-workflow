using Aw.Kernel;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.Api;

/// <summary>The memory calls (<see cref="SindriApiClient"/>, continued).</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<MemoryList>> ListMemories(Uri api, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<MemoryCreated>> CreateMemory(Uri api, CreateMemoryBody body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<MemorySearchView>> SearchMemory(Uri api, MemorySearch search, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<MemoryFactWritten>> WriteMemoryFact(Uri api, string name, WriteMemoryFactBody body, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Result<MemoryForgotten>> ForgetMemoryFacts(Uri api, MemoryForget forget, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
