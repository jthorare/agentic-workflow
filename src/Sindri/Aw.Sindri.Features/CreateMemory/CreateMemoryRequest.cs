namespace Aw.Sindri.Features.CreateMemory;

/// <summary>Make a memory store ('sindri memory create &lt;name&gt; [--description T]'; POST /sindri/memories; MCP memory_create).</summary>
/// <param name="Name">The name: lowercase letters, digits and '-'.</param>
/// <param name="Description">What the store is for, or null.</param>
public sealed record CreateMemoryRequest(string Name, string? Description);
