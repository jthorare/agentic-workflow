using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>What needs the human (GET /sindri/needs-you; API spec §3.1, §3.6).</summary>
/// <param name="IfNoneMatch">The Tag of an earlier answer; the same tag now answers NotModified with no body (#2789).</param>
public sealed record GetNeedsYouRequest(string? IfNoneMatch = null) : IQuery<GetNeedsYouResponse>;
