using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>One pack kind's choice (line spec §5, Appendix D): the pack used by default, and the pack a repo uses instead.</summary>
/// <param name="Default">The pack id used for every repo without its own entry.</param>
/// <param name="Repos">The pack id for a repo, by repo name.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PackChoice(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Default = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? Repos = null);
