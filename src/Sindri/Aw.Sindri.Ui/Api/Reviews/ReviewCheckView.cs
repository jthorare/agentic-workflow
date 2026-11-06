namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>One required check on this head.</summary>
/// <param name="Id">The check's id.</param>
/// <param name="Title">What it asks.</param>
/// <param name="Need">The kind of evidence: ci, covers, changedWith, body.</param>
/// <param name="Status">fail, needsTick, running, claimed or pass.</param>
/// <param name="Detail">Why it stands so; null for none.</param>
/// <param name="FixTask">True when Send as fix task is offered.</param>
internal sealed record ReviewCheckView(string Id, string Title, string Need, string Status, string? Detail, bool FixTask);
