using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Features.FileRequest;

/// <summary>The filed request (U3-API-3): '{accepted:{commandId, sequence}, requestId, decisionId}'.</summary>
/// <param name="Accepted">The command and the ledger sequence of its 'request.filed'.</param>
/// <param name="RequestId">The new request.</param>
/// <param name="DecisionId">The decision a human settles it with.</param>
public sealed record RequestFiledResponse(CommandAccepted Accepted, RequestId RequestId, DecisionId DecisionId);
