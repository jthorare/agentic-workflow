namespace Aw.Sindri.Core.Reviews;

/// <summary>What the library says about the PR: its claims, the doc they name, the project's docs and the test plan rows.</summary>
/// <param name="Error">Why the library couldn't be read, in words; null when it could.</param>
/// <param name="Claims">The PR's Covers: refs, each with how it resolves.</param>
/// <param name="Doc">The claimed doc; null when none.</param>
/// <param name="ProjectDocs">The project's docs, for a PR that names none.</param>
/// <param name="TestRows">The TP rows that verify a claimed item.</param>
/// <param name="Hidden">How many claimed docs the caller may not read (a session sees its audience only).</param>
public sealed record ReviewPageLibraryFacts(string? Error, IReadOnlyList<ReviewPageClaim> Claims, ReviewPageDocFact? Doc, IReadOnlyList<ReviewPageProjectDoc> ProjectDocs, IReadOnlyList<ReviewPageTestRow> TestRows, int Hidden);
