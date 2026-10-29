namespace Aw.Sindri.Core.Sandbox;

/// <summary>
/// What a session row says about its sandbox (sandbox spec §8 Surfaces): served as 'sandbox' on 'GET /sindri/sessions' and '/sessions/{provider}/{id}', left out for a session
/// that was not sandboxed.
/// </summary>
/// <param name="State">On when the launch's plan file reads; unavailable when the launch was sandboxed but the file does not.</param>
/// <param name="Attended">True for a session the owner opened.</param>
/// <param name="Folder">The folder the session writes to (the worktree), or null when the plan is not readable.</param>
/// <param name="Memory">The memory in use and the ceiling, or null when the plan is not readable.</param>
public sealed record SessionSandbox(SessionSandboxState State, bool Attended, string? Folder, SessionMemory? Memory);
