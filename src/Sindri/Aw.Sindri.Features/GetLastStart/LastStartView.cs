namespace Aw.Sindri.Features.GetLastStart;

/// <summary>What the last 2xx one-off start used.</summary>
/// <param name="Repo">The repo name, or null when no start was recorded (or the file can't be read).</param>
/// <param name="Agent">The agent id, or null likewise.</param>
public sealed record LastStartView(string? Repo, string? Agent);
