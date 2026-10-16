using Aw.Kernel.Testing;
using Aw.Sindri.Core.Decisions;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>Builds a decisions-view row with deterministic defaults: A.FirstDecisionId, pending, an accept-request decision for a re-scope, filed by a session at the epoch.</summary>
public sealed class DecisionRowBuilder
{
    private HumanDecisionRow _row = new(A.FirstDecisionId, A.FirstRequestId, DecisionKind.AcceptRequest, RequestKind.Rescope, Item: null,
        ItemVersion: 0, Repo: null, "Split the work into two items.", DecisionState.Pending, "session:cli", KnownInstants.Epoch,
        DecidedBy: null, DecidedAt: null, Choice: null, Note: null, Cleared: null, Sequence: 1);

    /// <summary>Sets the id and its request.</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="request">Its request.</param>
    /// <returns>This builder.</returns>
    public DecisionRowBuilder WithIds(DecisionId decision, RequestId request)
    {
        _row = _row with { Id = decision, Request = request };
        return this;
    }

    /// <summary>Sets the item, its version and the repo.</summary>
    /// <param name="item">The item, or null.</param>
    /// <param name="version">The item's version at fold time.</param>
    /// <param name="repo">The repo, or null.</param>
    /// <returns>This builder.</returns>
    public DecisionRowBuilder About(ItemId? item, long version, RepoName? repo)
    {
        _row = _row with { Item = item, ItemVersion = version, Repo = repo };
        return this;
    }

    /// <summary>Marks it decided.</summary>
    /// <param name="choice">The option chosen.</param>
    /// <param name="by">Who decided.</param>
    /// <param name="at">When.</param>
    /// <param name="cleared">The line that cleared it.</param>
    /// <returns>This builder.</returns>
    public DecisionRowBuilder Decided(DecisionOption choice, string by, DateTimeOffset at, string cleared)
    {
        _row = _row with { State = DecisionState.Decided, Choice = choice, DecidedBy = by, DecidedAt = at, Cleared = cleared };
        return this;
    }

    /// <summary>Marks it voided.</summary>
    /// <param name="at">When its item was closed.</param>
    /// <returns>This builder.</returns>
    public DecisionRowBuilder Voided(DateTimeOffset at)
    {
        _row = _row with { State = DecisionState.Voided, DecidedAt = at, Cleared = EmptyText.Voided() };
        return this;
    }

    /// <summary>Builds the row.</summary>
    /// <returns>The row.</returns>
    public HumanDecisionRow Build() => _row;
}
