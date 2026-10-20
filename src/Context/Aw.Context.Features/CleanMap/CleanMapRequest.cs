using Aw.Context.Core.Engine;
using Aw.Context.Core.Review;

namespace Aw.Context.Features.CleanMap;

/// <summary>Settles every undecided item of the latest draft so it can be promoted.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
/// <param name="MaxTokens">The most tokens the agent may spend in this pass.</param>
internal sealed record CleanMapRequest(ContextScope Scope, int MaxTokens = CleanupPolicy.MaxTokens);
