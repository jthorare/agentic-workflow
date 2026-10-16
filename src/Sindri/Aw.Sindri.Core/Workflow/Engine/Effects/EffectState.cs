using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Engine.Effects;

/// <summary>Where an effect stands (engine spec §5.7, §6.3 'outbox list'; engine P6): each state is one ledger row of the effect.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EffectState>))]
public enum EffectState
{
    /// <summary>Emitted after the transition's ledger event; waiting out 'workflow.dispatchDelay' (the undo window).</summary>
    [JsonStringEnumMemberName("pending")]
    Pending = 1,

    /// <summary>The recheck held and the effect's activity performed it (or found it already performed).</summary>
    [JsonStringEnumMemberName("dispatched")]
    Dispatched = 2,

    /// <summary>The recheck refused it (SND-RECIPE-005, SND-RECIPE-006 or SND-WF-011): nothing was performed.</summary>
    [JsonStringEnumMemberName("refused")]
    Refused = 3,

    /// <summary>A cancel of the instance aborted it within the dispatch delay: nothing was performed, and no recheck ran.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled = 4,
}
