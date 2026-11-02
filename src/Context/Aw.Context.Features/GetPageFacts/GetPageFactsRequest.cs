using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetPageFacts;

/// <summary>What the current map says, for a page write to check against.</summary>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetPageFactsRequest(ContextScope Scope);
