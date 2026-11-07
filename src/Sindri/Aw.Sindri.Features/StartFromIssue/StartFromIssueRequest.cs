using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.StartFromIssue;

/// <summary>'Scope it' on a Linear issue (Linear hub spec 2026-11-04 §6.1; owner only): makes an item linked to the issue and starts workflow.scoping on it.</summary>
/// <param name="Key">The issue's human key, 'ENG-12'.</param>
internal sealed record StartFromIssueRequest(string Key) : ICommand<StartFromIssueResponse>;
