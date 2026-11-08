using Aw.Kernel;

namespace Aw.Sindri.Core.Memory;

/// <summary>A memory store's name: lowercase letters, digits and '-', 1 to 48 characters, starting with a letter or digit ('team-notes').</summary>
public readonly record struct MemoryName
{
    /// <summary>Gets the name as typed.</summary>
    public string Value { get; init; }

    /// <summary>Parses a name.</summary>
    /// <param name="text">The name as typed.</param>
    /// <returns>The name, or SND-MEM-006. The error does not repeat text that is not a name.</returns>
    public static Result<MemoryName> Parse(string text) => throw new NotSupportedException();

    /// <summary>The name as typed back.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Value;
}
