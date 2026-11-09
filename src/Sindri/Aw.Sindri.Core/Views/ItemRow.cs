using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items.Vocabulary;

namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'items' view: one work item, its text already scrubbed and escaped by the projection.</summary>
/// <param name="Id">The item.</param>
/// <param name="Title">Its title.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="State">Its state.</param>
/// <param name="Step">The recipe step it is at, or null (the engine sets it later).</param>
/// <param name="Version">Its version: 1 when created, one more per state change.</param>
/// <param name="CreatedBy">The actor that created it, verbatim from the envelope (README X61: never parsed).</param>
/// <param name="CreatedAt">When it was created.</param>
/// <param name="Since">When it entered its current state.</param>
/// <param name="UpdatedAt">When the row last changed.</param>
/// <param name="Link">The tracker record it is linked to, or null.</param>
/// <param name="Alias">The human key of the last record this item was linked under, or null (an item the key moved away from keeps it in its row; the alias view names the owner).</param>
/// <param name="Body">Its body, or null (shown only by item detail).</param>
/// <param name="WaitingOn">The pending decision it waits on ('DEC-…', API P4), or null.</param>
/// <param name="Instance">The workflow instance running it (API P11), or null.</param>
/// <param name="Owner">Reserved for multi-user (README X61); always null for now.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record ItemRow(
    ItemId Id,
    string Title,
    RepoName? Repo,
    ItemState State,
    string? Step,
    long Version,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset Since,
    DateTimeOffset UpdatedAt,
    RecordKey? Link,
    ItemAlias? Alias,
    string? Body,
    string? WaitingOn,
    string? Instance,
    string? Owner,
    long Sequence);
