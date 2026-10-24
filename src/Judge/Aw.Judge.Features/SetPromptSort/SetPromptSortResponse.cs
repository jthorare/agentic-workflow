using Aw.Judge.Core.Config;

namespace Aw.Judge.Features.SetPromptSort;

/// <summary>The SetPromptSort response.</summary>
/// <param name="Config">The config as saved.</param>
public sealed record SetPromptSortResponse(JudgeConfig Config);
