namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One question the design review's ask step puts to the person (an entry of the instance's "fact:escalation").</summary>
/// <param name="Id">The question's id ("q1"); the answer is sent under it.</param>
/// <param name="Kind">ambiguousFix or productQuestion, as served; empty when not served.</param>
/// <param name="Question">The question in words.</param>
/// <param name="Options">The answers it offers; empty for a free answer.</param>
/// <param name="Lens">The review that raised it; empty when not served.</param>
/// <param name="Finding">The id of the finding behind it; empty when not served (kept, not shown: an id says nothing to a person).</param>
internal sealed record DeskQuestion(string Id, string Kind, string Question, string[] Options, string Lens, string Finding);
