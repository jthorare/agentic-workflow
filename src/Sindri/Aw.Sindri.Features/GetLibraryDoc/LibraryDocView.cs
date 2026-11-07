using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.GetLibraryDoc;

/// <summary>A design doc as the library stores it.</summary>
/// <param name="Slug">The front matter's <c language="text">id</c>, which the JSON calls <c language="text">id</c>.</param>
/// <param name="Root">The root: <c language="text">aw</c>, a vault's name or <c language="text">repo:&lt;name&gt;</c>.</param>
/// <param name="Scope">The scope its id is unique in.</param>
/// <param name="Kind">The front matter's kind; null when it is unreadable.</param>
/// <param name="Status">The front matter's status.</param>
/// <param name="Project">The front matter's project.</param>
/// <param name="Repos">The front matter's repos.</param>
/// <param name="Issues">The front matter's issues.</param>
/// <param name="Title">The doc's title.</param>
/// <param name="Path">The doc's file, with the home folder written <c language="text">~</c>.</param>
/// <param name="Errors">What is wrong with the doc, in file order.</param>
public sealed record LibraryDocView(
    [property: JsonPropertyName("id")] string Slug,
    string Root,
    string Scope,
    string? Kind,
    string? Status,
    string? Project,
    IReadOnlyList<string> Repos,
    IReadOnlyList<string> Issues,
    string Title,
    string Path,
    IReadOnlyList<LibraryDocErrorView> Errors);
