using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Where each record source's sync stands (GET /sindri/index/status; owned-index spec §5.8). Any caller: it reads the phase and the last tick, no record text.</summary>
public sealed record GetIndexStatusRequest : IQuery<IndexStatusView>;
