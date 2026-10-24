namespace Aw.Judge.Features.SortPrompt;

/// <summary>The SortPrompt response.</summary>
/// <param name="Context">The note a firing scaffold adds to the prompt, or null when none fires (every scaffold ships off, so by default null).</param>
public sealed record SortPromptResponse(string? Context = null);
