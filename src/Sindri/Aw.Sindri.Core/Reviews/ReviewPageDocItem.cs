using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One item of the doc map.</summary>
/// <param name="Key">The item id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Kind">phase, req, ac or task.</param>
/// <param name="ClaimedHere">Whether this PR claims it.</param>
/// <param name="ClaimedBy">Other PRs that claim it.</param>
/// <param name="Unclaimed">A requirement no PR claims.</param>
/// <param name="FixTask">Whether Send as fix task is offered.</param>
/// <param name="Items">Its children.</param>
public sealed record ReviewPageDocItem(
    [property: JsonPropertyName("id")] string Key,
    string Title,
    string Kind,
    bool ClaimedHere,
    IReadOnlyList<ReviewPageClaimedBy> ClaimedBy,
    bool Unclaimed,
    bool FixTask,
    IReadOnlyList<ReviewPageDocItem> Items);
