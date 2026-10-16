using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Testing;

/// <summary>Builds a requests-view row with deterministic defaults: A.FirstRequestId, an open re-scope filed by a session at the epoch, settled by A.FirstDecisionId.</summary>
public sealed class RequestRowBuilder
{
    private RequestRow _row = new(A.FirstRequestId, RequestKind.Rescope, Item: null, Repo: null, "Split the work into two items.",
        "session:cli", RequestState.Open, A.FirstDecisionId, KnownInstants.Epoch, Sequence: 1);

    /// <summary>Sets the id and its decision.</summary>
    /// <param name="request">The request.</param>
    /// <param name="decision">Its decision.</param>
    /// <returns>This builder.</returns>
    public RequestRowBuilder WithIds(RequestId request, DecisionId decision)
    {
        _row = _row with { Id = request, Decision = decision };
        return this;
    }

    /// <summary>Sets the kind.</summary>
    /// <param name="value">The kind.</param>
    /// <returns>This builder.</returns>
    public RequestRowBuilder OfKind(RequestKind value)
    {
        _row = _row with { Kind = value };
        return this;
    }

    /// <summary>Sets the item and repo.</summary>
    /// <param name="item">The item, or null.</param>
    /// <param name="repo">The repo, or null.</param>
    /// <returns>This builder.</returns>
    public RequestRowBuilder About(ItemId? item, RepoName? repo)
    {
        _row = _row with { Item = item, Repo = repo };
        return this;
    }

    /// <summary>Sets the state.</summary>
    /// <param name="value">The state.</param>
    /// <returns>This builder.</returns>
    public RequestRowBuilder InState(RequestState value)
    {
        _row = _row with { State = value };
        return this;
    }

    /// <summary>Builds the row.</summary>
    /// <returns>The row.</returns>
    public RequestRow Build() => _row;
}
