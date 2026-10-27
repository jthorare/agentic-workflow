using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>One provider answer for one labelled item, as scored and as recorded.</summary>
/// <param name="ItemId">The eval item.</param>
/// <param name="Label">The label it is scored against.</param>
/// <param name="Answer">What the provider answered.</param>
/// <param name="LatencyMs">How long it took.</param>
internal sealed record EvalResult(string ItemId, string Label, ProviderResult Answer, long LatencyMs);
