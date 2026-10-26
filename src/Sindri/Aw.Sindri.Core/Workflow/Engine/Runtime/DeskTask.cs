using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The open desk task (engine spec rev 8.1 §5.3, §6.4): the run 'workflow complete --run' must name, the Step's declared
/// outcomes and outputs (what a report may say), the input facts, and when the wait's timeout fires.
/// </summary>
/// <param name="Run">The open run's token.</param>
/// <param name="State">The active state the Step runs in.</param>
/// <param name="Step">The Step block.</param>
/// <param name="Outcomes">The Step's declared outcomes.</param>
/// <param name="Outputs">The Step's declared outputs, each with the outcomes it is required on.</param>
/// <param name="Inputs">The instance's input facts.</param>
/// <param name="TimeoutAt">When the state's timeout fires; null without one.</param>
/// <param name="Injection">The sections the Step's inputs filled, by metadata only (no text); empty without inputs.</param>
public sealed record DeskTask(
    RunToken Run,
    string State,
    BlockId Step,
    IReadOnlyList<OutcomeName> Outcomes,
    IReadOnlyList<OutputSpec> Outputs,
    IReadOnlyDictionary<FactKey, InputFact> Inputs,
    DateTimeOffset? TimeoutAt,
    IReadOnlyList<InjectionEntry>? Injection = null)
{
    /// <summary>The injection plan's sections, never null.</summary>
    public IReadOnlyList<InjectionEntry> Injection { get; init; } = Injection ?? [];

    /// <summary>How long each Step input took to fill, in declared order; null when the run kept none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<InputTiming>? Timings { get; init; }
}
