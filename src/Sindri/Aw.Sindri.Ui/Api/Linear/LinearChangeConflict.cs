namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>409 SND-LINEAR-409 of the change route: Linear changed the fields since the sheet opened; nothing was sent.</summary>
/// <param name="ChangedAt">When Linear changed it ("ENG-12 changed in Linear at 10:01").</param>
/// <param name="Fields">The fields that differ.</param>
/// <param name="Issue">The live read.</param>
internal sealed record LinearChangeConflict(DateTimeOffset ChangedAt, IReadOnlyList<LinearFieldConflict> Fields, LinearIssueOne Issue) : LinearWriteOutcome;
