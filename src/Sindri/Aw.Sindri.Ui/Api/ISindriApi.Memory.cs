using Aw.Kernel;
using Aw.Sindri.Ui.Api.Memory;

namespace Aw.Sindri.Ui.Api;

/// <summary>The memory calls (<see cref="ISindriApi"/>, continued). No route refuses an agent session; each write is sent once.</summary>
internal partial interface ISindriApi
{
    /// <summary>GET /sindri/memories.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The stores, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<MemoryList>> ListMemories(Uri api, CancellationToken cancellationToken);

    /// <summary>POST /sindri/memories.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="body">The new store.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The store, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<MemoryCreated>> CreateMemory(Uri api, CreateMemoryBody body, CancellationToken cancellationToken);

    /// <summary>GET /sindri/memories/{name}/search?query=&amp;level=&amp;limit=.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="search">The store, the query, the level and the limit.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The facts, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<MemorySearchView>> SearchMemory(Uri api, MemorySearch search, CancellationToken cancellationToken);

    /// <summary>POST /sindri/memories/{name}/facts.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="name">The store.</param>
    /// <param name="body">The fact.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The fact's key and what happened to it, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<MemoryFactWritten>> WriteMemoryFact(Uri api, string name, WriteMemoryFactBody body, CancellationToken cancellationToken);

    /// <summary>DELETE /sindri/memories/{name}/facts?fact=&amp;tag=&amp;expired=.</summary>
    /// <param name="api">The base URL.</param>
    /// <param name="forget">The store and what to forget.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many were deleted, the API's refusal, or SND-UI-002/003.</returns>
    public Task<Result<MemoryForgotten>> ForgetMemoryFacts(Uri api, MemoryForget forget, CancellationToken cancellationToken);
}
