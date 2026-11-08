using System.Text.Json.Serialization;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Features.Usage;

/// <summary>One lane's or the meta's standing.</summary>
/// <param name="Subject">The lane name, or "meta".</param>
/// <param name="State">ok, limited or unknown.</param>
/// <param name="Kind">The limit; set when limited.</param>
/// <param name="ResetsAt">When it resets; null when unknown.</param>
/// <param name="Source">Where the stop was seen.</param>
/// <param name="Reason">Why the state is unknown.</param>
/// <param name="Agent">The agent the lane is assigned to.</param>
/// <param name="Model">The model it is assigned; null when routing does not say.</param>
/// <param name="RemainingPct">The share the provider says is left; null when it says nothing.</param>
/// <param name="Windows">The provider's usage windows.</param>
/// <param name="SignalNote">Why there is no usage signal, or what it rests on.</param>
public sealed record UsageRow(
    string Subject,
    UsageState State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UsageKind? Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ResetsAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Source = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Agent = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Model = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? RemainingPct = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ProviderWindow>? Windows = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SignalNote = null);
