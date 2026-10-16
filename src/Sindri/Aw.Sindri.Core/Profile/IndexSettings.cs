using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The 'index:' section, at the profile's top level and on a repo entry (docs/csharp/sindri-profile.md): the globs map extraction
/// never reads, added to the built-in secret globs and never replacing them (context spec §6.2).
/// </summary>
/// <param name="DenyPaths">Repo-relative globs ('private/**', '**/*.generated.ts'); a repo's own are added to the profile's.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IndexSettings(IReadOnlyList<string>? DenyPaths = null);
