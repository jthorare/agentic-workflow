using Aw.Judge.Core.Labels;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>The EvaluateQuestion request ('judge eval &lt;question&gt; [--provider] [--variant] [--labels] [--collapse] [--record f] [--replay f]').</summary>
/// <param name="Question">The question to score.</param>
/// <param name="Provider">The provider to score, called directly with no chain (--provider, default jev).</param>
/// <param name="Variant">The input variant (--variant, default 'as-is').</param>
/// <param name="Labels">Score against this label source, or null for any (--labels).</param>
/// <param name="Collapse">Fold every label but this one into 'stalled' (--collapse), or null.</param>
/// <param name="RecordFile">Append each provider result to this file (--record), or null.</param>
/// <param name="ReplayFile">Re-score the results in this file instead of calling the provider (--replay), or null.</param>
/// <param name="Providers">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider; it must keep the provider.</param>
public sealed record EvaluateQuestionRequest(string Question, ProviderName Provider, string Variant, LabelSource? Labels = null, string? Collapse = null, string? RecordFile = null, string? ReplayFile = null, IReadOnlyList<ProviderName>? Providers = null);
