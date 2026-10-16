using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;

namespace Aw.Sindri.Core.Views;

/// <summary>A row of the 'decisions' view: one decision a human settles; the options come from DecisionKinds by <paramref name="Kind"/>.</summary>
/// <param name="Id">The decision.</param>
/// <param name="Request">The request it settles.</param>
/// <param name="Kind">What it decides.</param>
/// <param name="RequestKind">The kind of its request (the label and question read it).</param>
/// <param name="Item">The item it concerns, or null.</param>
/// <param name="ItemVersion">The item's version when the decision was folded (0 without an item).</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Text">The request's text, scrubbed.</param>
/// <param name="State">Where it stands.</param>
/// <param name="FiledBy">The actor that filed the request.</param>
/// <param name="FiledAt">When it was filed.</param>
/// <param name="DecidedBy">Who decided it, or null while pending or voided.</param>
/// <param name="DecidedAt">When it was decided, or null.</param>
/// <param name="Choice">The option chosen, or null.</param>
/// <param name="Note">The note given with the choice, scrubbed, or null.</param>
/// <param name="Cleared">The timeline line that cleared it (decided or voided), or null while pending.</param>
/// <param name="Sequence">The ledger sequence that last changed the row.</param>
public sealed record HumanDecisionRow(
    DecisionId Id,
    RequestId Request,
    DecisionKind Kind,
    RequestKind RequestKind,
    ItemId? Item,
    long ItemVersion,
    RepoName? Repo,
    string Text,
    DecisionState State,
    string FiledBy,
    DateTimeOffset FiledAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    DecisionOption? Choice,
    string? Note,
    string? Cleared,
    long Sequence);
