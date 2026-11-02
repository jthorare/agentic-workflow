namespace Aw.Sindri.Ui.Api.Sandbox;

/// <summary>A session row's sandbox (wire contract item 1); left out of a row with no Sindri launch.</summary>
/// <param name="State">'on', or 'unavailable' when the launch is sandboxed but its plan file does not read ("details unavailable").</param>
/// <param name="Attended">True for a session the owner opened with 'sindri open'.</param>
/// <param name="Folder">The first writable path, the worktree; null when unavailable.</param>
/// <param name="Memory">Memory used and allowed; null when unavailable.</param>
internal sealed record SessionSandboxView(string State, bool Attended, string? Folder, SandboxMemoryView? Memory);
