using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A 'waitFor: { answer }' (spec §3.4), satisfied by 'workflow answer' with outcome 'answered'.</summary>
/// <param name="Id">The question's id.</param>
/// <param name="Question">The question the human is asked.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AnswerWait(AnswerId Id, string Question);
