using Aw.Judge.Core.Providers;

namespace Aw.Judge.Features.CheckImages;

/// <summary>The CheckImages request: 'judge check images'.</summary>
/// <param name="Providers">The --providers / AW_JUDGE_PROVIDERS allowlist, or null for every provider.</param>
/// <param name="JevImagesEnvironment">AW_JUDGE_JEV_IMAGES as the process has it, or null when unset: only for the last lines, which say whether images go to Jev.</param>
public sealed record CheckImagesRequest(IReadOnlyList<ProviderName>? Providers = null, string? JevImagesEnvironment = null);
