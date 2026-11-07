using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>One test plan row.</summary>
/// <param name="Key">The row id.</param>
/// <param name="Text">Its text.</param>
/// <param name="Verifies">The items it verifies.</param>
/// <param name="Ticked">Whether the owner ticked it.</param>
public sealed record ReviewPageTestRow(
    [property: JsonPropertyName("id")] string Key,
    string Text,
    IReadOnlyList<string> Verifies,
    bool Ticked);
