using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Workflow.Views;

/// <summary>The frame a recorded change left the instance in.</summary>
/// <param name="State">The state the frame is in.</param>
/// <param name="Run">The open run token; null when no Step run is open.</param>
/// <param name="Runtime">Who runs it, desk or agent, when the row says.</param>
public sealed record RecordedFrame(string State, string? Run, StepRuntime? Runtime);
