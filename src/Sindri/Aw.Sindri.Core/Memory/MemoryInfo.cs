namespace Aw.Sindri.Core.Memory;

/// <summary>A named store of facts (a memory).</summary>
/// <param name="Name">The unique name.</param>
/// <param name="Description">What the store is for, or null.</param>
/// <param name="CreatedBy">The actor that made it ('human', 'session:...', 'service:...').</param>
/// <param name="CreatedAt">When it was made.</param>
public sealed record MemoryInfo(MemoryName Name, string? Description, string CreatedBy, DateTimeOffset CreatedAt);
