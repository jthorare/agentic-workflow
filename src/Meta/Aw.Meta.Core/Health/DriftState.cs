namespace Aw.Meta.Core.Health;

/// <summary>How an installed tool compares with what origin/main would install (issue #4249).</summary>
public enum DriftState
{
    /// <summary>It matches origin/main, or it was built from origin/main's last change to its inputs or a later commit.</summary>
    Current = 1,

    /// <summary>origin/main changed it, or its inputs, after what is installed; or it was built from uncommitted changes.</summary>
    Stale = 2,

    /// <summary>The comparison could not be made (no build record, no origin/main, git failed). Never counted as healthy.</summary>
    Unknown = 3,
}
