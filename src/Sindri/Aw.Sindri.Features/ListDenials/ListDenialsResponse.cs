using Aw.Sindri.Core.Sandbox.Denials;

namespace Aw.Sindri.Features.ListDenials;

/// <summary>The counters, one per launch, newest first.</summary>
/// <param name="Launches">The denial files.</param>
public sealed record ListDenialsResponse(IReadOnlyList<DenialFile> Launches);
