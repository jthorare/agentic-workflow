using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.SortPrompt;

/// <summary>The SortPrompt request ('judge prompt-sort' with '{prompt, sessionId}' on stdin, the prompt-sort.sh hook path).</summary>
/// <param name="Prompt">The user's prompt, as typed.</param>
/// <param name="Session">The session, or null when the hook had none (no session state is kept then).</param>
/// <param name="Providers">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider (Jev is left out of the sort when the list excludes it).</param>
public sealed record SortPromptRequest(string Prompt, SessionId? Session = null, IReadOnlyList<ProviderName>? Providers = null)
{
    /// <summary>The absolute paths of the images the prompt names (the hook's 'images' field), read best effort: one that can't be read or that Jev can't take is dropped and noted on the sort's decision row.</summary>
    public IReadOnlyList<string> Images { get; init; } = [];
}
