using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>The doc the PR claims, as the library holds it.</summary>
/// <param name="Key">The doc id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Path">Its file.</param>
/// <param name="Items">Its items in document order, flat; <see cref="ReviewPageItemFact.Parent"/> makes the tree.</param>
public sealed record ReviewPageDocFact([property: JsonPropertyName("id")] string Key, string Title, string Path, IReadOnlyList<ReviewPageItemFact> Items);
