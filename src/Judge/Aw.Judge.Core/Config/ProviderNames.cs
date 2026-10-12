namespace Aw.Judge.Core.Config;

/// <summary>The provider and scaffold names config.json may use; any other makes the file invalid.</summary>
public static class ProviderNames
{
    /// <summary>The agent CLIs, in their default priority order.</summary>
    public static IReadOnlyList<string> AgentClis { get; } = ["claude-cli", "codex-cli", "cursor-cli"];

    /// <summary>Every provider a question's chain may name.</summary>
    public static IReadOnlyList<string> All { get; } = ["rules", "jev", .. AgentClis];

    /// <summary>The prompt sorter's scaffolds.</summary>
    public static IReadOnlyList<string> Scaffolds { get; } = ["brief", "bugfix", "ui-evidence", "plan-first"];
}
