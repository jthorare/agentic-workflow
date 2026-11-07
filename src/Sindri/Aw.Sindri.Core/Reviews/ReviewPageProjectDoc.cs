using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Reviews;

/// <summary>A doc offered on the docMap panel.</summary>
/// <param name="Key">The doc id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Path">Its file.</param>
public sealed record ReviewPageProjectDoc(
    [property: JsonPropertyName("id")] string Key,
    string Title,
    string Path);
