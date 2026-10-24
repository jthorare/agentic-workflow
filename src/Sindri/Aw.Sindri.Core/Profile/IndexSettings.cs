using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The 'index:' section, at the profile's top level and on a repo entry (docs/csharp/sindri-profile.md): the globs map extraction
/// never reads, added to the built-in secret globs and never replacing them (context spec §6.2), and the code index's keys
/// (code index spec §13). A key left out is null here; the code index applies the default. A repo's list keys join the profile's
/// and each scalar it names replaces the profile's.
/// </summary>
/// <param name="DenyPaths">Repo-relative globs ('private/**', '**/*.generated.ts'); a repo's own are added to the profile's.</param>
/// <param name="UtilityGlobs">Globs of internal utility modules; their exports are flagged 'utility'.</param>
/// <param name="MaxFileKB">Larger files are skipped as too large (default <see cref="DefaultMaxFileKB"/>).</param>
/// <param name="MaxTotalMB">Larger tracked input stops the build (default <see cref="DefaultMaxTotalMB"/>).</param>
/// <param name="Embeddings">The embeddings layer's keys.</param>
/// <param name="Graph">The graph layer's keys.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record IndexSettings(
    IReadOnlyList<string>? DenyPaths = null,
    IReadOnlyList<string>? UtilityGlobs = null,
    int? MaxFileKB = null,
    int? MaxTotalMB = null,
    EmbeddingSettings? Embeddings = null,
    GraphSettings? Graph = null)
{
    /// <summary>The default for 'maxFileKB'.</summary>
    public const int DefaultMaxFileKB = 4_096;

    /// <summary>The default for 'maxTotalMB'.</summary>
    public const int DefaultMaxTotalMB = 4_096;
}
