using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Sindri.Ui.Api;

/// <summary>The P3 routes (<see cref="SindriApiClient"/>, continued): the queue, an item, its timeline, CreateItem and CloseItem.</summary>
internal sealed partial class SindriApiClient
{
    /// <inheritdoc />
    public Task<Result<QueueView>> Queue(Uri api, CancellationToken cancellationToken) => Get<QueueView>(api, "/sindri/queue", cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ItemListView>> ListItems(Uri api, string state, CancellationToken cancellationToken) =>
        Get<ItemListView>(api, "/sindri/items?state=" + Uri.EscapeDataString(state), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ItemView>> Item(Uri api, string itemId, CancellationToken cancellationToken) =>
        Get<ItemView>(api, "/sindri/items/" + Uri.EscapeDataString(itemId), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<TimelineView>> Timeline(Uri api, string itemId, long after, CancellationToken cancellationToken) =>
        Get<TimelineView>(api, string.Create(CultureInfo.InvariantCulture, $"/sindri/items/{Uri.EscapeDataString(itemId)}/timeline?after={after}"), cancellationToken, view: true);

    /// <inheritdoc />
    public Task<Result<ItemCreatedView>> CreateItem(Uri api, CreateItemRequest request, CancellationToken cancellationToken) =>
        Call<ItemCreatedView>(api, HttpMethod.Post, "/sindri/items", cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));

    /// <inheritdoc />
    public Task<Result<CommandAcceptedView>> CloseItem(Uri api, string itemId, CloseItemRequest request, CancellationToken cancellationToken) =>
        Call<CommandAcceptedView>(api, HttpMethod.Post, "/sindri/items/" + Uri.EscapeDataString(itemId) + "/close", cancellationToken, new CallOptions(Content: AwJson.Serialize(request, Json)));
}
