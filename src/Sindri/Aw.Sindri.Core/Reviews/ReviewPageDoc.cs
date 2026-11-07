using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The claimed doc and its item tree.</summary>
/// <param name="Key">The doc id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Path">Its file.</param>
/// <param name="Items">Its items.</param>
public sealed record ReviewPageDoc(
    [property: JsonPropertyName("id")] string Key,
    string Title,
    string Path,
    IReadOnlyList<ReviewPageDocItem> Items);
