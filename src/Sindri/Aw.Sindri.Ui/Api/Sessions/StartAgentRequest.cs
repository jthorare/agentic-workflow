namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>The body of POST /sindri/sessions (agents spec §15.8, A7): one one-off agent session.</summary>
/// <param name="Repo">The profile repo's name.</param>
/// <param name="Agent">The agent: claude-code or codex.</param>
/// <param name="Prompt">The first prompt (at most 8,000 characters); null starts the agent at its own prompt.</param>
/// <param name="Worktree">True to make a one-off worktree and branch first.</param>
/// <param name="ActId">The idempotency key, 8 to 32 characters of a-z0-9; kept by the form until a 2xx.</param>
internal sealed record StartAgentRequest(string Repo, string Agent, string? Prompt, bool Worktree, string ActId);
