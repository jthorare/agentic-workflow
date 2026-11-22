namespace Aw.Sindri.Core.Launch;

/// <summary>Whether an agent can run for the owner here (agents spec §15.4, SND-AGENT-013).</summary>
/// <param name="Installed">True when its executable is on PATH.</param>
/// <param name="SignedIn">True when the agent has a credential (its sign-in variable or its own login file).</param>
/// <param name="SignInCommand">What the owner runs to fix it ('claude' for Claude Code, 'codex login' for Codex); a sentence for an entry Sindri has no command for.</param>
public sealed record AgentReadiness(bool Installed, bool SignedIn, string SignInCommand);
