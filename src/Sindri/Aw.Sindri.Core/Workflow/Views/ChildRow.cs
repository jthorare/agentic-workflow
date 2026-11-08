using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>One child instance (E-20).</summary>
/// <param name="Id">The opaque id of the child instance (the route key).</param>
/// <param name="Address">The address.</param>
/// <param name="ItemId">The item id.</param>
/// <param name="Title">The title.</param>
/// <param name="Glyph">The mark.</param>
/// <param name="State">The state.</param>
/// <param name="Chip">The state chip.</param>
/// <param name="Round">The round.</param>
/// <param name="Agent">The agent target.</param>
/// <param name="AgeSeconds">Age in seconds.</param>
/// <param name="EngineOwned">The badge.</param>
/// <param name="MirroredPark">A park mirrored on the parent.</param>
/// <param name="Actions">Action ids.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChildRow(
    WorkflowInstanceId Id,
    InstanceAddress Address,
    ItemKey? ItemId,
    string Title,
    StateGlyph Glyph,
    string State,
    StateChip Chip,
    Round? Round,
    string? Agent,
    long AgeSeconds,
    EngineOwnedBadge? EngineOwned,
    EngineOwnedBadge? MirroredPark,
    IReadOnlyList<ActionId> Actions);
