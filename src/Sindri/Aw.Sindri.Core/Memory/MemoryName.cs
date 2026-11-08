using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Memory;

/// <summary>A memory store's name: lowercase letters, digits and '-', 1 to 48 characters, starting with a letter or digit ('team-notes').</summary>
public readonly partial record struct MemoryName
{
    /// <summary>Gets the name as typed.</summary>
    public string Value { get; init; }

    /// <summary>Parses a name.</summary>
    /// <param name="text">The name as typed.</param>
    /// <returns>The name, or SND-MEM-006. The error does not repeat text that is not a name.</returns>
    public static Result<MemoryName> Parse(string text) =>
        text is not null && Shape.IsMatch(text)
            ? new MemoryName { Value = text }
            : SindriErrors.MemoryNameInvalid.With("A memory name is lowercase letters, digits and '-', 1 to 48 characters, starting with a letter or digit.");

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,47}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    /// <summary>The name as typed back.</summary>
    /// <returns>The name.</returns>
    public override string ToString() => Value;
}
