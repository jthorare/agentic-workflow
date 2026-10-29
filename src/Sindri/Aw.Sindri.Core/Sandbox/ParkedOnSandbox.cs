using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sandbox;

/// <summary>One instance parked on a sandbox code.</summary>
/// <param name="Instance">The instance.</param>
/// <param name="Since">When it parked.</param>
/// <param name="Item">The item it runs for ('ITM-…'), or null when not read.</param>
/// <param name="Title">The item's title, or null when not read.</param>
public sealed record ParkedOnSandbox(WorkflowInstanceId Instance, DateTimeOffset Since, string? Item = null, string? Title = null);
