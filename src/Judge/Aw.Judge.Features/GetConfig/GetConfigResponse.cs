using Aw.Judge.Core.Config;

namespace Aw.Judge.Features.GetConfig;

/// <summary>The GetConfig response.</summary>
/// <param name="Config">The effective config: the stored one, or the defaults when it is invalid.</param>
/// <param name="Warning">Why the defaults are used instead of the stored config, or null.</param>
public sealed record GetConfigResponse(JudgeConfig Config, string? Warning);
