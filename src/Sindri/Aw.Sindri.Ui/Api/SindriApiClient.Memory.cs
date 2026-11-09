using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.Api;

/// <summary>The memory calls (<see cref="SindriApiClient"/>, continued). Each write is sent once.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<MemoryList>> ListMemories(Uri api, CancellationToken cancellationToken) =>
        Get<MemoryList>(api, MemoryRoutes.Root, cancellationToken);

    /// <inheritdoc />
    public Task<Result<MemoryCreated>> CreateMemory(Uri api, CreateMemoryBody body, CancellationToken cancellationToken) =>
        Call<MemoryCreated>(api, HttpMethod.Post, MemoryRoutes.Root, cancellationToken, new CallOptions(Content: AwJson.Serialize(body, Json)));

    /// <inheritdoc />
    public Task<Result<MemorySearchView>> SearchMemory(Uri api, MemorySearch search, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(search);
        return Get<MemorySearchView>(api, MemoryRoutes.Search(search), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result<MemoryFactWritten>> WriteMemoryFact(Uri api, string name, WriteMemoryFactBody body, CancellationToken cancellationToken) =>
        Call<MemoryFactWritten>(api, HttpMethod.Post, MemoryRoutes.Facts(name), cancellationToken, new CallOptions(Content: AwJson.Serialize(body, Json)));

    /// <inheritdoc />
    public Task<Result<MemoryForgotten>> ForgetMemoryFacts(Uri api, MemoryForget forget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(forget);
        return Call<MemoryForgotten>(api, HttpMethod.Delete, MemoryRoutes.Forget(forget), cancellationToken);
    }
}
