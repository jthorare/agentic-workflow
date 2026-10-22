namespace Aw.CodeIndex.Core;

/// <summary>The fixed limits of the code index (spec §5.1, §7.1, §16); not profile keys, like CodeRules in Context. Passing one fails that file or the build with a count, never a hang.</summary>
public static class CodeIndexRules
{
    /// <summary>Most hits a symbol search returns.</summary>
    public const int MaxSymbolHits = 200;

    /// <summary>Most paths one FileSymbols call takes.</summary>
    public const int MaxFileSymbolPaths = 200;

    /// <summary>Most edges a callers or callees walk returns per level.</summary>
    public const int MaxEdgesPerLevel = 100;

    /// <summary>Most neighbors one embedding search returns.</summary>
    public const int MaxNeighbors = 100;

    /// <summary>Most candidates an ambiguous call keeps as edges; 'candidates' keeps the true count.</summary>
    public const int MaxAmbiguousEdges = 8;

    /// <summary>Most files a snapshot keeps.</summary>
    public const int MaxFiles = 100_000;

    /// <summary>Most symbols one file may hold before it is left unparsed.</summary>
    public const int MaxSymbolsPerFile = 5_000;

    /// <summary>Most call sites one file may hold before it is left unparsed.</summary>
    public const int MaxCallSitesPerFile = 20_000;

    /// <summary>The largest index file, in bytes (2 GB).</summary>
    public const long MaxIndexBytes = 2L * 1024 * 1024 * 1024;

    /// <summary>The longest a build may run.</summary>
    public static readonly TimeSpan MaxBuildTime = TimeSpan.FromHours(2);

    /// <summary>The longest a build waits for a heavy-job slot before it ends deferred.</summary>
    public static readonly TimeSpan HeavySlotWait = TimeSpan.FromMinutes(10);

    /// <summary>Files written per transaction.</summary>
    public const int FileBatchSize = 200;

    /// <summary>A stored signature is cut at this many characters.</summary>
    public const int SignatureMaxCharacters = 400;

    /// <summary>An embedded text (name, signature and body) is cut at this many characters.</summary>
    public const int EmbedTextMaxCharacters = 2_000;

    /// <summary>Texts per embedding request.</summary>
    public const int EmbedBatchSize = 32;

    /// <summary>Most symbols embedded per build; a big first build finishes over several.</summary>
    public const int MaxEmbeddedPerBuild = 4_096;

    /// <summary>A symbol with fewer tokens has no MinHash and no clone rows.</summary>
    public const int MinCloneTokens = 30;

    /// <summary>Hashes in a MinHash.</summary>
    public const int MinHashLength = 64;

    /// <summary>LSH bands (of four rows each).</summary>
    public const int MinHashBands = 16;

    /// <summary>The estimated Jaccard similarity at or above which two symbols are near clones.</summary>
    public const double NearCloneThreshold = 0.8;

    /// <summary>Most dependency rows one manifest gives; the rest are dropped.</summary>
    public const int MaxDepsPerManifest = 5_000;

    /// <summary>Most blobs the parse quarantine keeps per repo; the oldest go first (spec §9 CI12).</summary>
    public const int MaxQuarantined = 100;

    /// <summary>The index schema version; a different one is rebuilt, never migrated.</summary>
    public const int SchemaVersion = 1;
}
