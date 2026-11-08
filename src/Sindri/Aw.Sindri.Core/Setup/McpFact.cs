namespace Aw.Sindri.Core.Setup;

/// <summary>Where the per-repo MCP registration stands.</summary>
/// <param name="Repos">Repos the approved profile names (0 until a profile with a repo is approved).</param>
/// <param name="Registered">Of those, the ones registered in every installed provider.</param>
/// <param name="Providers">The installed providers' names, joined ("Claude Code, Codex"); empty when none is installed.</param>
public sealed record McpFact(int Repos, int Registered, string Providers)
{
    /// <summary>One line per repo: whether it is registered, and in which providers.</summary>
    public SetupItems Items { get; init; } = SetupItems.None;
}
