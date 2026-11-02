using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sandbox.Credentials;

/// <summary>Where a 'separate-login' target keeps its own agent home (sandbox spec SB-8): the login, the hooks and the transcripts Steps of that target use, never the owner's.</summary>
public static class AgentHomes
{
    /// <summary>The agent home of <paramref name="target"/> under Sindri's home.</summary>
    /// <param name="sindriHome">Sindri's own home (AW_SINDRI_HOME).</param>
    /// <param name="target">The agent, such as claude-code or codex.</param>
    /// <returns>The path; nothing is created.</returns>
    public static string For(string sindriHome, AgentId target) => $"{sindriHome.TrimEnd('/')}/agent-homes/{target.Value}";
}
