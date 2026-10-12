namespace Aw.Judge.Infrastructure.Providers;

/// <summary>A codex event's item; an 'agent_message' item's text is the model's answer.</summary>
/// <param name="Type">The item type.</param>
/// <param name="Text">The text.</param>
internal sealed record CodexItem(string? Type = null, string? Text = null);
