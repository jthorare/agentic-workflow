using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Host.Endpoints;

/// <summary>The body of POST /sindri/launches/{launchId}/limit-hit: '{kind, peak}'; the launch comes from the route and the proof from X-Sindri-Launch-Secret.</summary>
/// <param name="Kind">Which limit was reached.</param>
/// <param name="Peak">The peak: a count for pids, bytes for memory and disk.</param>
internal sealed record LimitHitBody(LimitHitKind Kind, long Peak);
