namespace Aw.Sindri.Core.Reviews;

/// <summary>What Context stores about a PR, read for the caller (spec §6.2): the record and the library's view of it.</summary>
/// <param name="Title">The record's title; null when the PR has no stored record.</param>
/// <param name="Author">The record's author.</param>
/// <param name="Draft">Whether the record is a draft.</param>
/// <param name="Url">The record's link.</param>
/// <param name="UpdatedAt">The record's newest activity.</param>
/// <param name="IssueKeys">The Linear issue keys the record links to.</param>
/// <param name="Library">The library's facts about the PR.</param>
public sealed record ReviewPageStored(string? Title, string? Author, bool Draft, string? Url, DateTimeOffset? UpdatedAt, IReadOnlyList<string> IssueKeys, ReviewPageLibraryFacts Library);
