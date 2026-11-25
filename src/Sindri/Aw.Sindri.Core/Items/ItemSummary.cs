using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Core.Items;

/// <summary>An item as the queue and the item list show it (U2-API-1): no version, body or link (U2-API-6, -7).</summary>
/// <param name="Id">The item.</param>
/// <param name="Title">Its title, scrubbed.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="State">Its state.</param>
/// <param name="Step">Its recipe step, or null.</param>
/// <param name="Since">When it entered its state.</param>
/// <param name="PrimaryAction">The row's primary action, a member of Actions, or null.</param>
/// <param name="Actions">The action ids it offers.</param>
/// <param name="Owner">Reserved for multi-user (README X61); always null for now.</param>
/// <param name="Parked">True when the item is parked (#2788).</param>
public sealed record ItemSummary(
    ItemId Id,
    string Title,
    RepoName? Repo,
    ItemState State,
    string? Step,
    DateTimeOffset Since,
    ActionId? PrimaryAction,
    IReadOnlyList<ActionId> Actions,
    string? Owner,
    bool Parked = false);
