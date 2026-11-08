
namespace Aw.Sindri.Features.LinearIssues;

/// <summary>409 of the edit route: Linear changed fields since the sheet opened; nothing was sent.</summary>
/// <param name="ChangedAt">When Linear last changed the issue.</param>
/// <param name="Fields">The fields that differ.</param>
/// <param name="Issue">The issue as Linear has it now.</param>
internal sealed record LinearChangeConflict(DateTimeOffset ChangedAt, IReadOnlyList<LinearFieldConflict> Fields, GetLinearIssueResponse Issue);
