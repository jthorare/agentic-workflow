namespace Aw.Sindri.Features.LinearIssues;

/// <summary>How an edit ended when it ended with an answer: exactly one of <paramref name="Applied"/> (200) and <paramref name="Conflict"/> (409, nothing was sent). Every other ending is an error.</summary>
/// <param name="Applied">The edit is in Linear.</param>
/// <param name="Conflict">Linear changed a field since the sheet opened.</param>
internal sealed record LinearChangeResult(LinearChangeApplied? Applied, LinearChangeConflict? Conflict);
