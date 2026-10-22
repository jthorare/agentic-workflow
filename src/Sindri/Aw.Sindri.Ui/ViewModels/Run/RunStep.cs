using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>One step row inside a stage card.</summary>
/// <param name="Id">The step's state id.</param>
/// <param name="Name">The step's name in words.</param>
/// <param name="State">Where the step is.</param>
/// <param name="Kind">Who does it, as far as it is served.</param>
/// <param name="Round">The loop round, when the step is in a loop.</param>
internal sealed record RunStep(string Id, string Name, RunNodeState State, RunStepKind Kind, Round? Round);
