using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>One action of an 'index.pages' entry (docs/csharp/sindri-profile.md): what a person does on the page, and the concepts it relates to.</summary>
/// <param name="Id">Its id, unique within the page; left out, the name gives one.</param>
/// <param name="Name">What the map shows (required).</param>
/// <param name="Action">The map action it names ('action:SchedulePage.submit-schedule'); left out, it is an owner action.</param>
/// <param name="Concepts">Ids of the pins in 'index.concepts', or 'concept:&lt;id&gt;' keys of derived concepts, it relates to.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PageActionSettings(
    PageActionPinId? Id = null,
    string? Name = null,
    string? Action = null,
    IReadOnlyList<string>? Concepts = null);
