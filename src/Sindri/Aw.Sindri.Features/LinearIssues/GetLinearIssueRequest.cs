using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>'GET /sindri/linear/issues/{key}' (Linear hub spec §3; query): one stored issue with its description and newest comments.</summary>
/// <param name="Key">The issue's human key, 'ENG-12'.</param>
internal sealed record GetLinearIssueRequest(string Key) : IQuery<GetLinearIssueResponse>;
