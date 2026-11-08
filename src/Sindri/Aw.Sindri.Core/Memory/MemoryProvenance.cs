namespace Aw.Sindri.Core.Memory;

/// <summary>Where a fact came from. Every part is optional.</summary>
/// <param name="Workflow">The workflow instance that wrote it, or null.</param>
/// <param name="Run">The agent run that wrote it, or null.</param>
/// <param name="Item">The item it was written for, or null.</param>
/// <param name="Session">The agent session that wrote it, or null.</param>
public sealed record MemoryProvenance(string? Workflow, string? Run, string? Item, string? Session)
{
    /// <summary>Gets a provenance with no part set.</summary>
    public static MemoryProvenance None { get; } = new(Workflow: null, Run: null, Item: null, Session: null);
}
