
namespace Aw.Sindri.Core.Profile;

/// <summary>One toolchain the detector found, and the entries it suggests for it.</summary>
/// <param name="Name">The toolchain's name for the owner: Node, Python, Go or dotnet.</param>
/// <param name="Evidence">The first project file that showed it.</param>
/// <param name="Entries">The entries this toolchain adds.</param>
public sealed record DetectedToolchain(string Name, string Evidence, SandboxRepoSettings Entries)
{
    /// <summary>The owner-facing line: the toolchain, the file that showed it and what is offered.</summary>
    /// <returns>One line, no line break.</returns>
    public string Line()
    {
        List<string> parts = [];
        AddList(parts, "groups", Entries.Groups);
        AddList(parts, "hosts", Entries.Hosts);
        AddList(parts, "cache paths", Entries.CachePaths);
        AddList(parts, "prepare (npm ci) in", Entries.Prepare?.Dirs?.Select(static d => d.Dir ?? string.Empty).ToList());
        return $"{Name} ({Evidence}): {string.Join("; ", parts)}";
    }

    private static void AddList(List<string> parts, string label, IReadOnlyList<string>? values)
    {
        if (values is { Count: > 0 })
        {
            parts.Add($"{label} {string.Join(", ", values)}");
        }
    }
}
