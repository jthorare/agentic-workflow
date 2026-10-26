using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The workflow status layout as fields (E-23); text is what the CLI prints.</summary>
/// <param name="Header">The header line's parts.</param>
/// <param name="Next">The next sentence; null when the layout has none (a finished instance).</param>
/// <param name="Answer">The answer the layout offers.</param>
/// <param name="Child">The child a park mirrors.</param>
/// <param name="Drift">A recorded drift.</param>
/// <param name="Inputs">The Step's inputs.</param>
/// <param name="Timeout">The timeout line.</param>
/// <param name="Budget">The budget line.</param>
/// <param name="Paused">The paused line.</param>
/// <param name="Commands">The instance's verbs.</param>
/// <param name="Text">The CLI's text form.</param>
/// <param name="Injection">The sections the open run's Step inputs filled, by metadata only; empty without inputs.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StatusFields(
    StatusHeader Header,
    string? Next,
    StatusAnswer? Answer,
    StatusChild? Child,
    StatusDrift? Drift,
    IReadOnlyList<StatusInput> Inputs,
    StatusTimeout? Timeout,
    StatusBudget? Budget,
    StatusPaused? Paused,
    IReadOnlyList<StatusCommand> Commands,
    string Text,
    IReadOnlyList<StatusInjection>? Injection = null)
{
    /// <summary>The open agent run (P8); null (not written) for a desk run, a block run or none, so the other statuses read as before.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public StatusRun? Run { get; init; }

    /// <summary>The typed code of the park the instance is in ('SND-WF-010' for a refused dispatch or a missing runtime) where the engine knows one; null (not written) otherwise. Carried from where the park is made, never read from its text.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ReasonCode { get; init; }

    /// <summary>When a person took the instance over (the time it entered 'taken-over'); null (not written) when it is not taken over.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? TakenOverAt { get; init; }

    /// <summary>Who took the instance over (the takeover's actor, such as 'human:thor'); null (not written) when it is not taken over.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TakenOverBy { get; init; }

    /// <summary>How long each Step input of the open run took to fill, in declared order; null (not written) when none was kept.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<StatusTiming>? InputTimings { get; init; }

    /// <summary>The open run's injection sections (metadata only), never null; empty without inputs.</summary>
    public IReadOnlyList<StatusInjection> Injection { get; init; } = Injection ?? [];
}
