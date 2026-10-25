namespace Aw.Sindri.Ui.Api.Context;

/// <summary>One hit of GET /sindri/context/search. An untrusted label or snippet arrives fenced and is shown as plain text.</summary>
/// <param name="Key">The node's key.</param>
/// <param name="Kind">Its kind.</param>
/// <param name="Label">Its label.</param>
/// <param name="Repo">Its repo.</param>
/// <param name="Area">The feature it sits in.</param>
/// <param name="Score">Its rank score.</param>
/// <param name="Matched">What matched: "title", "path" or "text".</param>
/// <param name="Snippet">The matching text.</param>
/// <param name="Trust">"trusted" or "untrusted".</param>
internal sealed record MapSearchHitView(
    string Key,
    string Kind,
    string Label,
    string? Repo,
    string? Area,
    double Score,
    string Matched,
    string? Snippet,
    string Trust);
