namespace Aw.Judge.Host.Commands;

/// <summary>The 'prompt-sort' stdin record (hook contract: prompt-sort.sh writes '{prompt, sessionId}'); other keys are ignored.</summary>
/// <param name="Prompt">The user's prompt.</param>
/// <param name="SessionId">The session, or empty/null when the hook had none (then AW_SESSION_ID is used).</param>
internal sealed record SortPromptInput(string Prompt, string? SessionId = null);
