using Aw.Context.Core.Records;
using Aw.Context.Core.Sources;

namespace Aw.Context.Core.Library;

/// <summary>A design doc as the library stores it (PR review page spec 2026-11-04 §5.3): its record and its parsed header.</summary>
/// <param name="Key">The doc record's key.</param>
/// <param name="Root">The root: <c language="text">aw</c>, a vault's name or <c language="text">repo:&lt;name&gt;</c>.</param>
/// <param name="Scope">The scope its ids are unique in: <c language="text">aw/&lt;repo-slug&gt;</c> in the aw root, otherwise the root.</param>
/// <param name="Slug">The front matter's <c language="text">id</c>; null when the front matter is unreadable.</param>
/// <param name="Kind">The front matter's kind.</param>
/// <param name="Status">The front matter's status.</param>
/// <param name="Project">The front matter's project.</param>
/// <param name="Repos">The front matter's <c language="text">repos:</c> entries.</param>
/// <param name="Issues">The front matter's <c language="text">issues:</c> entries.</param>
/// <param name="ParserVersion">The parser version that made the rows.</param>
/// <param name="Errors">The errors stored with the doc, then <see cref="DesignDocCodes.SharedDocId"/> when another doc keeps its id (decided when read).</param>
/// <param name="Title">The record's title.</param>
/// <param name="Url">The record's link, when it has one.</param>
/// <param name="Audience">The record's audience; null is visible to no one.</param>
/// <param name="UpdatedAt">When the doc's file last changed, as the record stores it; null when unknown.</param>
public sealed record LibraryDoc(
    RecordKey Key,
    string Root,
    string Scope,
    string? Slug,
    string? Kind,
    string? Status,
    string? Project,
    IReadOnlyList<string> Repos,
    IReadOnlyList<string> Issues,
    int ParserVersion,
    IReadOnlyList<DesignDocError> Errors,
    string Title,
    string? Url,
    AudienceKey? Audience,
    DateTimeOffset? UpdatedAt = null);
