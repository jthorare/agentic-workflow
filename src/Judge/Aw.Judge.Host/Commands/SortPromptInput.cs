namespace Aw.Judge.Host.Commands;

/// <summary>The 'prompt-sort' stdin record (hook contract: prompt-sort.sh writes '{prompt, sessionId}'); other keys are ignored.</summary>
/// <param name="Prompt">The user's prompt.</param>
/// <param name="SessionId">The session, or empty/null when the hook had none (then AW_SESSION_ID is used).</param>
/// <param name="Images">The absolute paths of the images the prompt names (prompt-sort.sh adds them with imagesBestEffort: true only when it found some), or null.</param>
internal sealed record SortPromptInput(string Prompt, string? SessionId = null, IReadOnlyList<string>? Images = null);
