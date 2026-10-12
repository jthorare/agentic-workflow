using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.AskQuestion;

/// <summary>The AskQuestion request ('judge &lt;question&gt; &lt; input.json').</summary>
/// <param name="Question">The question's name, e.g. 'wake-gate'.</param>
/// <param name="Input">The input text as read from stdin; anything that isn't the question's input record escalates as 'invalid-input'.</param>
public sealed record AskQuestionRequest(string Question, string Input)
{
    /// <summary>The calling session (AW_SESSION_ID), stored with the decision's details.</summary>
    public SessionId? SessionId { get; init; }

    /// <summary>The providers allowlist (--providers or AW_JUDGE_PROVIDERS), or null for every provider; rules is always kept.</summary>
    public IReadOnlyList<ProviderName>? AllowedProviders { get; init; }
}
