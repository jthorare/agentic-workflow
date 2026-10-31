using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetPinSuggest;

/// <summary>The pin-suggest read.</summary>
/// <param name="Query">Which map, whose repo, and the one thing to suggest.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetPinSuggestRequest(PinSuggestQuery Query, ContextScope Scope);
