namespace Aw.Sindri.Features.CheckSandbox;

/// <summary>One Step the next Check again press would resume.</summary>
/// <param name="Instance">The parked instance.</param>
/// <param name="Item">The item it runs for, or null when the instance can't be read.</param>
/// <param name="Title">The item's title, or null when it can't be read.</param>
/// <param name="Since">When the instance parked.</param>
public sealed record SandboxResumeLine(Core.Workflow.Engine.WorkflowInstanceId Instance, string? Item, string? Title, DateTimeOffset Since);
