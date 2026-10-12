using Aw.Judge.Core.Config;

namespace Aw.Judge.Features.SetConfig;

/// <summary>The SetConfig response.</summary>
/// <param name="Config">The config as saved.</param>
public sealed record SetConfigResponse(JudgeConfig Config);
