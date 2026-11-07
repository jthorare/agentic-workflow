using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One required check on this head.</summary>
/// <param name="Key">The check's id.</param>
/// <param name="Title">What it asks.</param>
/// <param name="Need">ci, changedWith, covers, body or manual.</param>
/// <param name="Status">pass, fail, running, skipped, claimed, needsTick or ticked.</param>
/// <param name="Detail">Why it stands so.</param>
/// <param name="FixTask">True on a fail: Send as fix task is offered.</param>
public sealed record ReviewPageCheck(
    [property: JsonPropertyName("id")] string Key,
    string Title,
    string Need,
    string Status,
    string? Detail,
    bool FixTask);
