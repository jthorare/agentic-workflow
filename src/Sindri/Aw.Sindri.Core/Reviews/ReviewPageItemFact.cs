using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One item of a claimed doc, with who claims it.</summary>
/// <param name="Key">The item id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Kind">phase, req, ac or task.</param>
/// <param name="Parent">The item it belongs to; null at the top.</param>
/// <param name="ClaimedHere">Whether this PR claims it.</param>
/// <param name="ClaimedBy">Other open or merged PRs that claim it.</param>
public sealed record ReviewPageItemFact([property: JsonPropertyName("id")] string Key, string Title, string Kind, string? Parent, bool ClaimedHere, IReadOnlyList<ReviewPageClaimedBy> ClaimedBy);
