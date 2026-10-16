using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Core.Text;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>Binds the request and decision routes' raw values (P1 binding rule): a '{decisionId}' segment and the '?state=' and '?pending=' filters, each a 400 SND-CLI-003 when malformed. '?item=' goes through ItemRoute.Item.</summary>
internal static class RequestRoute
{
    /// <summary>The decision in a '{decisionId}' segment.</summary>
    /// <param name="decisionId">The segment.</param>
    /// <returns>The id, or SND-CLI-003.</returns>
    internal static Result<DecisionId> Decision(string decisionId) =>
        DecisionId.TryParse(decisionId, provider: null, out var id) ? id : SindriErrors.InvalidInput.With("'" + Echo(decisionId) + "' isn't a decision id (DEC-…)");

    /// <summary>The '?state=' filter, or null when absent.</summary>
    /// <param name="state">The query value.</param>
    /// <returns>The state, or SND-CLI-003.</returns>
    internal static Result<RequestState?> State(string? state) => state switch
    {
        null => (RequestState?)null,
        "open" => RequestState.Open,
        "accepted" => RequestState.Accepted,
        "declined" => RequestState.Declined,
        "voided" => RequestState.Voided,
        _ => SindriErrors.InvalidInput.With("'" + Echo(state) + "' isn't a request state (open, accepted, declined or voided)"),
    };

    /// <summary>The '?pending=' filter, or null when absent.</summary>
    /// <param name="pending">The query value.</param>
    /// <returns>True or false, or SND-CLI-003.</returns>
    internal static Result<bool?> Pending(string? pending) => pending switch
    {
        null => (bool?)null,
        "true" => true,
        "false" => false,
        _ => SindriErrors.InvalidInput.With("'" + Echo(pending) + "' isn't true or false"),
    };

    /// <summary>The '?item=' filter, or null when absent.</summary>
    /// <param name="item">The query value.</param>
    /// <returns>The reference, or SND-CLI-003.</returns>
    internal static Result<ItemRef?> Item(string? item) => item is null ? (ItemRef?)null : ItemRoute.Item(item).Map(static r => (ItemRef?)r);

    private static string Echo(string value) => SafeText.Line(SafeText.Escape(value), 80);
}
