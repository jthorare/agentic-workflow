namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>An agent the form can start .</summary>
/// <param name="Id">The wire id.</param>
/// <param name="Label">The name in the choice.</param>
/// <param name="Short">The name in the foot line: "Started claude in sindri."</param>
internal sealed record AgentChoice(string Id, string Label, string Short)
{
    /// <summary>Claude Code.</summary>
    internal static AgentChoice ClaudeCode { get; } = new("claude-code", "Claude Code", "claude");

    /// <summary>Codex.</summary>
    internal static AgentChoice Codex { get; } = new("codex", "Codex", "codex");

    /// <summary>Cursor: in the agent catalog, so the form offers it (api, #3380).</summary>
    internal static AgentChoice Cursor { get; } = new("cursor", "Cursor", "cursor");

    /// <summary>The choices, in order.</summary>
    internal static IReadOnlyList<AgentChoice> All { get; } = [ClaudeCode, Codex, Cursor];
}
