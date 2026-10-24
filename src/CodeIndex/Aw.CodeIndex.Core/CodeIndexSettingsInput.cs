namespace Aw.CodeIndex.Core;

/// <summary>The 'index:' keys as Sindri's profile hands them over, merged per repo (spec §13); a null key takes its default. <see cref="CodeIndexSettings.Create"/> re-checks them.</summary>
/// <param name="DenyPaths">Globs added to the built-in secret globs.</param>
/// <param name="UtilityGlobs">Globs of internal utility modules.</param>
/// <param name="MaxFileKB">Larger files are skipped as too-large.</param>
/// <param name="MaxTotalMB">Larger input stops the build.</param>
/// <param name="EmbeddingsEnabled">'embeddings.enabled'.</param>
/// <param name="EmbeddingsUrl">'embeddings.url'.</param>
/// <param name="EmbeddingsModel">'embeddings.model'; null means the lock's model.</param>
/// <param name="GraphEnabled">'graph.enabled'.</param>
/// <param name="GraphMaxMB">'graph.maxMB'.</param>
public sealed record CodeIndexSettingsInput(
    IReadOnlyList<string>? DenyPaths = null,
    IReadOnlyList<string>? UtilityGlobs = null,
    int? MaxFileKB = null,
    int? MaxTotalMB = null,
    bool? EmbeddingsEnabled = null,
    string? EmbeddingsUrl = null,
    string? EmbeddingsModel = null,
    bool? GraphEnabled = null,
    int? GraphMaxMB = null);
