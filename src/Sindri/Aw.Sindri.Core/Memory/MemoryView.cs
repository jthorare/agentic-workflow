namespace Aw.Sindri.Core.Memory;

/// <summary>A memory as an answer shows it.</summary>
/// <param name="Name">The name.</param>
/// <param name="Description">What it is for, or null.</param>
/// <param name="CreatedBy">The actor that made it.</param>
/// <param name="CreatedAt">When it was made.</param>
/// <param name="Facts">How many facts it holds.</param>
public sealed record MemoryView(string Name, string? Description, string CreatedBy, DateTimeOffset CreatedAt, int Facts)
{
    /// <summary>Shows a stored memory.</summary>
    /// <param name="memory">The memory.</param>
    /// <param name="facts">How many facts it holds.</param>
    /// <returns>The view.</returns>
    public static MemoryView From(MemoryInfo memory, int facts)
    {
        ArgumentNullException.ThrowIfNull(memory);
        return new MemoryView(memory.Name.Value, memory.Description, memory.CreatedBy, memory.CreatedAt, facts);
    }
}
