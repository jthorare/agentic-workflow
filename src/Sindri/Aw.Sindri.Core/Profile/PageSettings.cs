using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>One entry of 'index.pages' (docs/csharp/sindri-profile.md): a page the owner names, with its actions and the concepts they relate to.</summary>
/// <param name="Id">Its id, unique among the pages; left out, the name gives one.</param>
/// <param name="Name">What the map shows (required).</param>
/// <param name="Repo">The repo it is in; left out, the surface's repo or the profile's only repo. Under a repo's own 'index.pages' it must be absent or that repo.</param>
/// <param name="Surface">The map surface it names ('surface:route/GET:/schedule/week'); left out, it is an owner page.</param>
/// <param name="Actions">Its actions.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PageSettings(
    PagePinId? Id = null,
    string? Name = null,
    string? Repo = null,
    string? Surface = null,
    IReadOnlyList<PageActionSettings>? Actions = null);
