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

    /// <summary>When a person took the instance over (the time it entered 'taken-over'); null (not written) when it is not taken over.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? TakenOverAt { get; init; }

    /// <summary>The open run's injection sections (metadata only), never null; empty without inputs.</summary>
    public IReadOnlyList<StatusInjection> Injection { get; init; } = Injection ?? [];
}
