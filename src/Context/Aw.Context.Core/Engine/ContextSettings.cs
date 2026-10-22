using Aw.Context.Core.Onyx;
using Aw.Context.Core.Trust;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The profile's 'context:' section, checked, with its defaults (spec §8.8). Later phases add their keys as init
/// properties with defaults, so a profile written for an earlier build keeps its meaning.
/// </summary>
public sealed record ContextSettings
{
    /// <summary>The default doc globs.</summary>
    public static IReadOnlyList<string> DefaultDocGlobs { get; } = ["docs/**/*.md", "**/PRD*.md", "**/TDD*.md"];

    /// <summary>The default 'context.code.schemaGlobs': the migration and schema files the code lens's data files may name.</summary>
    public static IReadOnlyList<string> DefaultSchemaGlobs { get; } =
        ["**/migrations/**", "**/migrate/**", "**/schema/**", "**/schemas/**", "**/*.sql", "**/*.prisma", "**/*.graphql", "**/*.gql", "**/*.proto", "**/*.avsc", "**/*.xsd", "**/*.schema.json"];

    /// <summary>The default 'context.code.testGlobs': the paths that mark a file as a test.</summary>
    public static IReadOnlyList<string> DefaultTestGlobs { get; } =
        ["**/test/**", "**/tests/**", "**/Test/**", "**/Tests/**", "**/__tests__/**", "**/spec/**", "**/specs/**", "**/*.test.*", "**/*.spec.*", "**/*_test.*", "**/*Test.*", "**/*Tests.*", "**/test_*.*"];

    /// <summary>The profile repos the engine maps, or null for all of them.</summary>
    public IReadOnlyList<ContextRepoName>? Repos { get; init; }

    /// <summary>The repo-relative globs whose files the repo-docs source indexes.</summary>
    public IReadOnlyList<string> DocGlobs { get; init; } = DefaultDocGlobs;

    /// <summary>The per-query deadline.</summary>
    public TimeSpan Deadline { get; init; } = TimeSpan.FromSeconds(4);

    /// <summary>The code lens's lexical search deadline: one git grep per implementing file and data thing, run in turn, so it gets longer than a query.</summary>
    public TimeSpan CodeDeadline { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>The largest token budget a request may ask for.</summary>
    public int MaxBudget { get; init; } = 32_000;

    /// <summary>Below this many fused items a query reformulates once (Phase 8).</summary>
    public int MinHits { get; init; } = 3;

    /// <summary>The cascade's depth cap ('context.cascadeDepth', spec §6.1): 0 to 4, default 2; a query's depth overrides it.</summary>
    public int CascadeDepth { get; init; } = 2;

    /// <summary>The component hop's fan-out cap ('context.cascadeComponentFanOut'): 1 to 50, default 5; a code unit implementing more features is skipped and reported.</summary>
    public int CascadeComponentFanOut { get; init; } = CascadeOptions.DefaultFanOut;

    /// <summary>Whether session callers may read context at all, on any transport (spec §3). Off until approved.</summary>
    public bool SessionsEnabled { get; init; }

    /// <summary>The checked 'context.sources.onyx' entry, or null when the profile names no onyx source.</summary>
    public OnyxSettings? Onyx { get; init; }

    /// <summary>The Linear team keys ('context.sources.linear.teams') an Onyx identifier alias may name; empty for none.</summary>
    public IReadOnlyList<string> LinearTeams { get; init; } = [];

    /// <summary>
    /// The profile's extra scrub patterns ('context.scrub.extraPatterns', D14), as written: <see cref="IScrubberFactory"/>
    /// checks them and every call scrubs with them, on ingest and on egress.
    /// </summary>
    public IReadOnlyList<ScrubPattern> ExtraPatterns { get; init; } = [];

    /// <summary>'context.labeling': the optional labeling pass of a map build (S-4). Off by default.</summary>
    public LabelingSettings Labeling { get; init; } = new();

    /// <summary>'context.eval.adjudicator' (S-7A): whether a backtest asks the model to label surfaces no PR touched, within 1.5M tokens per run. Off by default.</summary>
    public bool Adjudicator { get; init; }

    /// <summary>'context.code.schemaGlobs' (S-CL): repo-relative globs of the migration and schema files the code lens's data files may name; empty turns the schema scan off.</summary>
    public IReadOnlyList<string> SchemaGlobs { get; init; } = DefaultSchemaGlobs;

    /// <summary>'context.code.testGlobs' (S-CL): repo-relative globs that mark a file as a test for the code lens's lexical tests.</summary>
    public IReadOnlyList<string> TestGlobs { get; init; } = DefaultTestGlobs;

    /// <summary>'context.extract.recognizers' (S-SK): the profile's extra surface recognizers, added to the built-in forms. Empty by default.</summary>
    public IReadOnlyList<ProfileRecognizer> Recognizers { get; init; } = [];

    /// <summary>'context.extract.graphify' (S-4): the cluster pass's time, snapshot, file and graph limits; generous defaults, 0 for no limit.</summary>
    public ClusterLimits Graphify { get; init; } = ClusterLimits.Default;

    /// <summary>'context.publish': when a refreshed map becomes the current one (retrieval spec §8.7a).</summary>
    public PublishSettings Publish { get; init; } = new();

    /// <summary>'context.ranker': the feature stage and the rerank arm of the query (Phase 8). Both off by default.</summary>
    public RankerSettings Ranker { get; init; } = new();
}
