namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>
/// The run a state is on when an agent session runs its Step (engine P8): who runs it and the launch that carries it. Served
/// only while such a run exists, so every reader treats its absence as the ordinary case.
/// </summary>
/// <param name="Runtime">The runtime that runs the Step ('agent').</param>
/// <param name="Launch">The launch id (LCH-…) of the session; opaque.</param>
/// <param name="Session">The session the launch started (provider and id), once it knows it; read as optional.</param>
/// <param name="WaitingForSlot">While the launch waits on the session cap (SND-LAUNCH-020), how many run and the cap; null (omitted) otherwise, and once it starts.</param>
internal sealed record StatusRun(
    string Runtime,
    string Launch,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] SessionHandle? Session = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Sandbox.WaitingForSlotView? WaitingForSlot = null);
