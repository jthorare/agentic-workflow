using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.ListDecisions;

/// <summary>A decision as the list shows it (U3-API-3, -6, -7, -8; UI spec §4.7).</summary>
/// <param name="Id">The decision.</param>
/// <param name="Kind">What it decides.</param>
/// <param name="Class">The action class of deciding it ('decision').</param>
/// <param name="Label">Its label ('decision · re-scope').</param>
/// <param name="Item">The item it concerns, or null.</param>
/// <param name="ItemVersion">The item's version when it was folded.</param>
/// <param name="Repo">Its repo, or null.</param>
/// <param name="Question">'Accept this re-scope request?' and the request's text.</param>
/// <param name="Options">What can be chosen.</param>
/// <param name="Recommended">The option to offer first.</param>
/// <param name="State">Where it stands.</param>
/// <param name="FiledBy">Who filed the request.</param>
/// <param name="FiledAt">When it was filed.</param>
/// <param name="DecidedBy">Who decided it, or null.</param>
/// <param name="DecidedAt">When, or null.</param>
/// <param name="Choice">The option chosen, or null.</param>
/// <param name="Note">The note given with it, or null.</param>
/// <param name="RecordedOnly">True when its answer is recorded and not yet acted on.</param>
/// <param name="Notice">The sentence saying so, or the voided sentence for a voided decision, else null.</param>
/// <param name="Actions">'Decide' while pending, none otherwise.</param>
/// <param name="Cli">The command line that decides it ('sindri decide DEC-… accept').</param>
/// <param name="Cleared">The timeline line that cleared it (decided or voided), or null while pending.</param>
public sealed record DecisionView(
    DecisionId Id,
    DecisionKind Kind,
    ActionClass Class,
    string Label,
    ItemId? Item,
    long ItemVersion,
    RepoName? Repo,
    string Question,
    IReadOnlyList<OptionView> Options,
    DecisionOption Recommended,
    DecisionState State,
    string FiledBy,
    DateTimeOffset FiledAt,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    DecisionOption? Choice,
    string? Note,
    bool RecordedOnly,
    string? Notice,
    IReadOnlyList<ActionId> Actions,
    string Cli,
    string? Cleared);
