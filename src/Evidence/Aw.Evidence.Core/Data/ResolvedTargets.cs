namespace Aw.Evidence.Core.Data;

/// <summary>What the repo's targets config allows a script to reach.</summary>
/// <param name="BuildOutputs">Directories relative to the repo whose executables a script may run.</param>
/// <param name="Allow">Absolute paths of programs a script may run.</param>
/// <param name="PassEnv">Names of variables every CLI call may inherit.</param>
/// <param name="Databases">The databases db-run may read, by name.</param>
/// <param name="Desktop">What desktop-run needs; null when the config has no 'desktop' part.</param>
public sealed record ResolvedTargets(IReadOnlyList<string> BuildOutputs, IReadOnlyList<string> Allow, IReadOnlyList<string> PassEnv, IReadOnlyDictionary<string, DatabaseEntry>? Databases = null, ResolvedDesktop? Desktop = null)
{
    /// <summary>The databases db-run may read, by name (empty when none).</summary>
    public IReadOnlyDictionary<string, DatabaseEntry> DatabaseMap => Databases ?? new Dictionary<string, DatabaseEntry>(StringComparer.Ordinal);

    /// <summary>No config: nothing is allowed.</summary>
    public static ResolvedTargets Empty { get; } = new([], [], []);

    /// <summary>True when no program is allowed at all.</summary>
    public bool AllowsNoProgram => BuildOutputs.Count == 0 && Allow.Count == 0;
}
