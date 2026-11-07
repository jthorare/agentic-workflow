using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Linear;

namespace Aw.Sindri.Features.GetLinearSince;

/// <summary>'GET /sindri/linear/since?after=' (Linear hub spec 2026-11-04 §3): the strip above the issue list. Owner only.</summary>
/// <param name="After">A marker an earlier answer gave; null reads from the start. Anything else is SND-API-013.</param>
internal sealed record GetLinearSinceRequest(string? After = null) : IQuery<LinearSinceResponse>;
