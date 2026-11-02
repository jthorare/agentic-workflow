using Aw.Context.Core.Engine;

namespace Aw.Context.Features.RelinkPinAliases;

/// <summary>Makes the pin-alias links of every stored record against the current map.</summary>
/// <param name="Scope">The profile's settings and repos, and the caller.</param>
internal sealed record RelinkPinAliasesRequest(ContextScope Scope);
