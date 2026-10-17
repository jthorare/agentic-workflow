using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>Open an item's agent session (POST /sindri/items/{itemId}/open, 'sindri open'; API spec §3.2, UI spec E-1): asks for a launch; the launcher service does it.</summary>
/// <param name="Item">The item, by id or alias.</param>
/// <param name="Mode">Open, attach or resume: the catalog variant.</param>
/// <param name="Target">The agent the person picked, or null to take the remembered one (checked by pattern only; the launcher checks the catalog).</param>
/// <param name="ItemVersion">The version the human saw; a stale one is SND-ITEM-409.</param>
public sealed record OpenItemRequest(ItemRef Item, LaunchMode Mode, AgentId? Target, long ItemVersion) : ICommand<LaunchRequested>, IClassifiedCommand
{
    /// <inheritdoc/>
    public string Variant => Mode switch
    {
        LaunchMode.Open => "open",
        LaunchMode.Attach => "attach",
        LaunchMode.Resume => "resume",
        _ => "unknown",
    };

    /// <inheritdoc/>
    public bool CarriesProtectedFields => false;
}
