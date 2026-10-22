using Aw.CodeIndex.Core.Model;
using Aw.Kernel;

namespace Aw.CodeIndex.Core;

/// <summary>
/// The index settings of one repo, after defaults and validation (spec §13). Sindri's profile validates the same keys with
/// its own key paths; <see cref="Create"/> re-checks them and raises SND-CODEIDX-001 (005 for a refused embedding target).
/// </summary>
/// <param name="DenyPaths">Globs added to the built-in secret globs, never replacing them.</param>
/// <param name="UtilityGlobs">Globs of internal utility modules; their exports are flagged 'utility'.</param>
/// <param name="MaxFileKB">Larger files are skipped as too-large.</param>
/// <param name="MaxTotalMB">Larger input stops the build (SND-CODEIDX-003).</param>
/// <param name="Embeddings">The embeddings layer.</param>
/// <param name="Graph">The graph layer.</param>
public sealed record CodeIndexSettings(
    IReadOnlyList<string> DenyPaths,
    IReadOnlyList<string> UtilityGlobs,
    int MaxFileKB,
    int MaxTotalMB,
    EmbeddingsSettings Embeddings,
    GraphSettings Graph)
{
    /// <summary>The default embedding server: Ollama on the loopback address.</summary>
    public const string DefaultEmbeddingsUrl = "http://127.0.0.1:11434";

    /// <summary>The defaults of every key (spec §13).</summary>
    public static CodeIndexSettings Default { get; } = new(
        DenyPaths: [],
        UtilityGlobs: [],
        MaxFileKB: 512,
        MaxTotalMB: 200,
        Embeddings: new EmbeddingsSettings(Enabled: true, DefaultEmbeddingsUrl, Model: null),
        Graph: new GraphSettings(Enabled: true, MaxMegabytes: 256, MaxNodes: 500_000));

    /// <summary>What the snapshot may keep under these settings.</summary>
    /// <returns>The deny globs and the size caps.</returns>
    public SnapshotLimits SnapshotLimits() => new(DenyPaths, MaxFileKB * 1024L, MaxTotalMB * 1024L * 1024L, CodeIndexRules.MaxFiles);

    /// <summary>Applies the defaults to a profile's keys and checks every value.</summary>
    /// <param name="input">The keys, or null for all defaults.</param>
    /// <returns>The settings; SND-CODEIDX-001 with the key path first for a bad key or value, 005 for a non-loopback embedding URL or a cloud model.</returns>
    public static Result<CodeIndexSettings> Create(CodeIndexSettingsInput? input)
    {
        input ??= new CodeIndexSettingsInput();
        var error = CheckNumbersAndGlobs(input) ?? CheckEmbeddings(input);
        if (error is not null)
        {
            return error;
        }

        return Merge(input);
    }

    private static Error? CheckNumbersAndGlobs(CodeIndexSettingsInput input)
    {
        var problem =
            Positive("index.maxFileKB", input.MaxFileKB)
            ?? Positive("index.maxTotalMB", input.MaxTotalMB)
            ?? Positive("index.graph.maxMB", input.GraphMaxMB)
            ?? Positive("index.graph.maxNodes", input.GraphMaxNodes)
            ?? Globs("index.denyPaths", input.DenyPaths)
            ?? Globs("index.utilityGlobs", input.UtilityGlobs);
        return problem is null ? null : CodeIndexErrors.SettingsInvalid.With(problem);
    }

    private static Error? CheckEmbeddings(CodeIndexSettingsInput input)
    {
        if (!EmbeddingTarget.IsLoopbackUrl(input.EmbeddingsUrl ?? DefaultEmbeddingsUrl))
        {
            return CodeIndexErrors.EmbeddingTargetRefused.With("index.embeddings.url: must be http://127.0.0.1 or http://[::1], with no credentials");
        }

        return input.EmbeddingsModel is { } model && (model.Length == 0 || model.Contains("cloud", StringComparison.OrdinalIgnoreCase))
            ? CodeIndexErrors.EmbeddingTargetRefused.With("index.embeddings.model: must name a local model, never a cloud model")
            : null;
    }

    private static CodeIndexSettings Merge(CodeIndexSettingsInput input)
        => new(
            input.DenyPaths ?? Default.DenyPaths,
            input.UtilityGlobs ?? Default.UtilityGlobs,
            input.MaxFileKB ?? Default.MaxFileKB,
            input.MaxTotalMB ?? Default.MaxTotalMB,
            new EmbeddingsSettings(input.EmbeddingsEnabled ?? Default.Embeddings.Enabled, input.EmbeddingsUrl ?? DefaultEmbeddingsUrl, input.EmbeddingsModel),
            new GraphSettings(input.GraphEnabled ?? Default.Graph.Enabled, input.GraphMaxMB ?? Default.Graph.MaxMegabytes, input.GraphMaxNodes ?? Default.Graph.MaxNodes));

    private static string? Positive(string key, int? value)
        => value is <= 0 ? $"{key}: must be a positive whole number" : null;

    private static string? Globs(string key, IReadOnlyList<string>? globs)
        => globs?.Any(string.IsNullOrWhiteSpace) == true ? $"{key}: a glob is empty" : null;
}
