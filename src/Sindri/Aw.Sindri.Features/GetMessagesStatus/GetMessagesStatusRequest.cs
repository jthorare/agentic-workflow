using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetMessagesStatus;

/// <summary>The Messages sync status (GET /sindri/messages/status, 'sindri messages status'; messages spec §8.2, §5.3).</summary>
public sealed record GetMessagesStatusRequest : IQuery<GetMessagesStatusResponse>;
