using Aw.Judge.Core.Config;

namespace Aw.Judge.Features.SetJevImages;

/// <summary>The SetJevImages response.</summary>
/// <param name="Config">The config as saved.</param>
public sealed record SetJevImagesResponse(JudgeConfig Config);
