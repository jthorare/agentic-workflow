using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>What the gate runs for a pull request, chosen by the paths it changes.</summary>
public sealed record GateConfig
{
    /// <summary>Builds a gate. A key the team file leaves out reads as empty, so a CI team can write just a gate block with "mode": "ci".</summary>
    /// <param name="always">Scopes that run for every pull request; absent: none.</param>
    /// <param name="docsOnly">Globs; when every changed path matches one, only the always scopes run; absent: none.</param>
    /// <param name="scopes">Every scope by name; the order is the order they run in; absent: none.</param>
    /// <param name="parallelSteps">How many heavy steps of one gate run at once; absent: the team's heavy cap (<see cref="HeavyConfig.Cap"/>). 1 runs the gate serially.</param>
    /// <param name="mode">"local" (the default; a file without the key reads so): gate-run runs the scopes' steps in a gate tree on this box. "ci": gate-run only reads the pull request's required checks, review decision and merge state from GitHub. Any other value is AW-META-003 at load.</param>
    [JsonConstructor]
    public GateConfig(IReadOnlyList<string>? always = null, IReadOnlyList<string>? docsOnly = null, IReadOnlyDictionary<string, ScopeConfig>? scopes = null, int? parallelSteps = null, string mode = LocalMode)
    {
        Always = always ?? [];
        DocsOnly = docsOnly ?? [];
        Scopes = scopes ?? new Dictionary<string, ScopeConfig>(StringComparer.Ordinal);
        ParallelSteps = parallelSteps;
        Mode = mode;
    }

    /// <summary>Scopes that run for every pull request.</summary>
    public IReadOnlyList<string> Always { get; init; }

    /// <summary>Globs; when every changed path matches one, only the always scopes run.</summary>
    public IReadOnlyList<string> DocsOnly { get; init; }

    /// <summary>Every scope by name; the order is the order they run in.</summary>
    public IReadOnlyDictionary<string, ScopeConfig> Scopes { get; init; }

    /// <summary>How many heavy steps of one gate run at once; null: the team's heavy cap.</summary>
    public int? ParallelSteps { get; init; }

    /// <summary>"local" or "ci"; see the constructor.</summary>
    public string Mode { get; init; }

    /// <summary>The gate runs its steps on this box (the default).</summary>
    public const string LocalMode = "local";

    /// <summary>The gate reads the pull request's CI result from GitHub and runs no step.</summary>
    public const string CiMode = "ci";

    /// <summary>The one scope name a CI gate record carries.</summary>
    public const string CiScope = "ci";

    /// <summary>Whether the gate reads CI instead of running steps.</summary>
    [JsonIgnore]
    public bool IsCi => string.Equals(Mode, CiMode, StringComparison.Ordinal);

    /// <summary>A gate with no scopes.</summary>
    public static GateConfig Empty { get; } = new();
}
