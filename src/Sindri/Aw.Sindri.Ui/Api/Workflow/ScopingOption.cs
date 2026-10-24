namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One answer the scoping interview offers for a question (an entry of options in "fact:questions").</summary>
/// <param name="Id">The option's id ("Q1.a").</param>
/// <param name="Answer">The answer in words; it is also what the person's choice records as text.</param>
/// <param name="Rationale">Why it is offered; empty when not served.</param>
/// <param name="Recommended">True for the one answer the interview recommends.</param>
internal sealed record ScopingOption(string Id, string Answer, string Rationale, bool Recommended);
