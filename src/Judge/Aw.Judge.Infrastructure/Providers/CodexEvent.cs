namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// One line of `codex exec --json`: `{"type":"item.completed","item":{"type":"agent_message","text":"{…}"}}`;
/// a rejected run prints `{"type":"error"}` or `{"type":"turn.failed"}`.
/// </summary>
/// <param name="Type">The event type.</param>
/// <param name="Item">The item, for item events.</param>
internal sealed record CodexEvent(string? Type = null, CodexItem? Item = null);
