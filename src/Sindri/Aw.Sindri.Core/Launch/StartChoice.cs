namespace Aw.Sindri.Core.Launch;

/// <summary>The repo and agent of the last one-off start (agents spec §15.4).</summary>
/// <param name="Repo">The repo name, or null when there is none.</param>
/// <param name="Agent">The agent id, or null when there is none.</param>
public sealed record StartChoice(string? Repo, string? Agent);
