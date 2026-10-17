using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Features.OpenItem;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/items/{itemId}/open: '{mode, target?, itemVersion}'; the item comes from the route (UI spec E-1).</summary>
/// <param name="Mode">'open', 'attach' or 'resume' (required: a missing or unknown one is a 400).</param>
/// <param name="Target">The agent the person picked, an agent id (pattern only), or absent for the remembered one.</param>
/// <param name="ItemVersion">The version the human saw (required: a missing one is a 400).</param>
internal sealed record OpenItemBody(LaunchMode Mode, long ItemVersion, AgentId? Target = null)
{
    /// <summary>The request for <paramref name="item"/>.</summary>
    /// <param name="item">The route's item.</param>
    /// <returns>The request.</returns>
    internal OpenItemRequest ToRequest(ItemRef item) => new(item, Mode, Target, ItemVersion);
}
