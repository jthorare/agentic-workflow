using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>'todo.obsidian' (todo spec §4.5): the note the todos sync with. <see cref="TodoObsidianRules"/> checks both fields.</summary>
/// <param name="Vault">The 'name' of one of 'context.sources.library.vaults'.</param>
/// <param name="Note">The note's path inside that vault, such as `Todo.md`: relative, ending .md, no `..`, not in a dot folder, not matched by the vault's deny globs.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TodoObsidianSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Vault = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Note = null);
