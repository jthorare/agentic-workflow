namespace Aw.Context.Core.Engine;

/// <summary>A repo as the profile writes it, before the engine checks its name (<see cref="SettingsReader.Check(System.Text.Json.JsonElement?, IReadOnlyList{ProfileRepoEntry})"/>).</summary>
/// <param name="Name">The repo's name as written.</param>
/// <param name="Path">The absolute path of its checkout.</param>
/// <param name="DenyPaths">The globs the profile's 'index.denyPaths' (its own and this repo's) add to the built-in secret globs, or null for none.</param>
/// <param name="Concepts">The pins of the profile's 'index.concepts' (its own and this repo's), or null for none.</param>
/// <param name="DemotePaths">The globs of the repo's demoted roles ('index.demote'): indexed, but never an implementer, anchor or scope surface; or null for none.</param>
public sealed record ProfileRepoEntry(string Name, string Path, IReadOnlyList<string>? DenyPaths = null, IReadOnlyList<ConceptPin>? Concepts = null, IReadOnlyList<string>? DemotePaths = null)
{
    /// <summary>The repo's roles with their globs and treatment ('index.roles', 'index.ignore', 'index.demote'), for the role counts; or null for none.</summary>
    public IReadOnlyList<RepoRole>? Roles { get; init; }
}
