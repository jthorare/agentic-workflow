namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One question of the scoping interview (an entry of "fact:questions" whose options are objects).</summary>
/// <param name="Id">The question's id ("Q1").</param>
/// <param name="Question">The question in words.</param>
/// <param name="Dimension">What it is about ("how long data lives"); empty when not served.</param>
/// <param name="Options">The answers it offers.</param>
/// <param name="Other">True when the person may write their own answer.</param>
/// <param name="Skip">True when the person may leave the question unanswered.</param>
internal sealed record ScopingQuestion(string Id, string Question, string Dimension, IReadOnlyList<ScopingOption> Options, bool Other, bool Skip);
