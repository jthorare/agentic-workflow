using System.Text.Json.Serialization;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Claude Code's 'hooks' settings, as far as a Step's session uses them: the turn's end (and its end on an API error), a new prompt and the tool calls in between.</summary>
/// <param name="Stop">Runs when the agent's turn ends.</param>
/// <param name="StopFailure">Runs when an API error ends the turn: Stop does not run then.</param>
/// <param name="UserPromptSubmit">Runs when a prompt starts a turn.</param>
/// <param name="PreToolUse">Runs before each tool call: an agent woken without a prompt (a background task's notice) is working again, and a tool is now in flight.</param>
/// <param name="PostToolUse">Runs when a tool call returns.</param>
/// <param name="PostToolUseFailure">Runs when a tool call fails.</param>
internal sealed record ClaudeHooks(
    [property: JsonPropertyName("Stop")] IReadOnlyList<ClaudeHookGroup> Stop,
    [property: JsonPropertyName("StopFailure")] IReadOnlyList<ClaudeHookGroup> StopFailure,
    [property: JsonPropertyName("UserPromptSubmit")] IReadOnlyList<ClaudeHookGroup> UserPromptSubmit,
    [property: JsonPropertyName("PreToolUse")] IReadOnlyList<ClaudeHookGroup> PreToolUse,
    [property: JsonPropertyName("PostToolUse")] IReadOnlyList<ClaudeHookGroup> PostToolUse,
    [property: JsonPropertyName("PostToolUseFailure")] IReadOnlyList<ClaudeHookGroup> PostToolUseFailure);
