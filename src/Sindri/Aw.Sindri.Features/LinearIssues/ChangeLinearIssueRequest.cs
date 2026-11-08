using System.Text.Json;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Edit a stored Linear issue (POST /sindri/linear/issues/{key}/change; Linear hub spec 2026-11-04 §3, §5.3, D6): desk class, owner only. Nothing goes to the Sindri ledger: Context sends the change and writes the answer through to the store.</summary>
/// <param name="Key">The issue's human key, 'ENG-12'.</param>
/// <param name="Opened">The values of the changed fields as the sheet opened them (field name to value): the field-conflict check compares them with Linear's.</param>
/// <param name="Set">The new values of the changed fields. Names: title, description, stateId, assigneeId, priority, projectId, cycleId, labelIds, parentKey; null clears assigneeId, projectId, cycleId and parentKey.</param>
internal sealed record ChangeLinearIssueRequest(string Key, IReadOnlyDictionary<string, JsonElement> Opened, IReadOnlyDictionary<string, JsonElement> Set) : ICommand<LinearChangeResult>;
