using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>What needs the human (GET /sindri/needs-you; API spec §3.1, §3.6).</summary>
public sealed record GetNeedsYouRequest() : IQuery<GetNeedsYouResponse>;
