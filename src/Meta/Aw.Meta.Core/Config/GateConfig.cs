using System.Text.Json.Serialization;

namespace Aw.Meta.Core.Config;

/// <summary>What the gate runs for a pull request, chosen by the paths it changes.</summary>
/// <param name="Always">Scopes that run for every pull request.</param>
/// <param name="DocsOnly">Globs; when every changed path matches one, only the always scopes run.</param>
/// <param name="Scopes">Every scope by name; the order is the order they run in.</param>
/// <param name="ParallelSteps">How many heavy steps of one gate run at once; absent: the team's heavy cap (<see cref="HeavyConfig.Cap"/>). 1 runs the gate serially.</param>
/// <param name="Mode">"local" (the default; a file without the key reads so): gate-run runs the scopes' steps in a gate tree on this box. "ci": gate-run only reads the pull request's required checks, review decision and merge state from GitHub. Any other value is AW-META-003 at load.</param>
public sealed record GateConfig(IReadOnlyList<string> Always, IReadOnlyList<string> DocsOnly, IReadOnlyDictionary<string, ScopeConfig> Scopes, int? ParallelSteps = null, string Mode = GateConfig.LocalMode)
{
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
    public static GateConfig Empty { get; } = new([], [], new Dictionary<string, ScopeConfig>(StringComparer.Ordinal));
}
