using System.Text.Json.Serialization;

namespace Aw.Context.Core.Engine;

/// <summary>
/// A question (spec §8.1). Exactly one shape: free <see cref="Text"/>, or a <see cref="Node"/>, <see cref="Item"/> or
/// <see cref="Symbol"/> anchor (the graph shapes arrive in later phases and answer SND-CONTEXT-003 until then).
/// </summary>
/// <param name="Text">Free text, or null.</param>
/// <param name="Node">A graph node key, name or alias, or null.</param>
/// <param name="Item">A Sindri item id or tracker key, or null.</param>
/// <param name="Symbol">A file path or symbol, or null.</param>
/// <param name="Depth">The cascade depth (a depth past 8 is read as 8), or null for the profile's.</param>
/// <param name="Budget">The token budget, or null for the default; capped at 'context.maxBudget'.</param>
/// <param name="Draft">True to read the draft map instead of the current one.</param>
/// <param name="Diff">A commit range (two shas): the files changed from their merge base to the second, mapped to nodes (the diff shape), or null. Left out of JSON when null.</param>
/// <param name="Repo">The repo a diff is read in; only valid with <paramref name="Diff"/>, and optional when the caller's scope holds one repo. Left out of JSON when null.</param>
public sealed record ContextQuery(
    string? Text,
    string? Node = null,
    string? Item = null,
    string? Symbol = null,
    int? Depth = null,
    int? Budget = null,
    bool Draft = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DiffRange? Diff = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ContextRepoName? Repo = null)
{
    /// <summary>Read the graph as of a retained map version ('v3', '3' or 'v3@a41b07d'); null for the current one. Left out of JSON when null; not valid with Draft.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MapVersion { get; init; }

    /// <summary>The cascade hops as a comma list ('state,flow,dependency,component'); null for the default three. Left out of JSON when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hops { get; init; }
}
