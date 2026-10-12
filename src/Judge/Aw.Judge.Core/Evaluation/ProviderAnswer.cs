using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>One provider's answer at its place in the chain.</summary>
/// <param name="Name">The provider.</param>
/// <param name="Position">Its chain position.</param>
/// <param name="LatencyMs">How long it took.</param>
/// <param name="Result">What it answered.</param>
internal sealed record ProviderAnswer(ProviderName Name, int Position, long LatencyMs, ProviderResult Result);
