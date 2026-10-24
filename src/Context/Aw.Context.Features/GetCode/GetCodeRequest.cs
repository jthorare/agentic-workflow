using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetCode;

/// <summary>The code lens around some anchors.</summary>
/// <param name="Anchors">Node keys, aliases or labels.</param>
/// <param name="Draft">True to read the latest draft.</param>
/// <param name="MapVersion">A retained version, or null.</param>
/// <param name="Scope">The profile's context section and repos, and the caller.</param>
internal sealed record GetCodeRequest(IReadOnlyList<string> Anchors, bool Draft, string? MapVersion, ContextScope Scope);
