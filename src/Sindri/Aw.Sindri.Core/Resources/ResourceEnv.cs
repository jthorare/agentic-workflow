namespace Aw.Sindri.Core.Resources;

/// <summary>
/// The environment variable names a run reads its resources from (agent-first PR 14). The one place that spells them: the
/// SDK of PR 15 and the block context read these constants, never their own copies.
/// </summary>
public static class ResourceEnv
{
    /// <summary>Prefix of a database's variable: <c lang="text">SINDRI_DB_&lt;NAME&gt;</c> holds the path of its SQLite file.</summary>
    public const string DatabasePrefix = "SINDRI_DB_";

    /// <summary>Prefix of a disk's variable: <c lang="text">SINDRI_DISK_&lt;NAME&gt;</c> holds the path of its folder.</summary>
    public const string DiskPrefix = "SINDRI_DISK_";

    /// <summary>Prefix of a connection's variable: <c lang="text">SINDRI_CONNECTION_&lt;NAME&gt;</c> holds the connection's name.</summary>
    public const string ConnectionPrefix = "SINDRI_CONNECTION_";

    /// <summary>The variable for a database.</summary>
    /// <param name="name">The resource name (lowercase letters, digits and '-').</param>
    /// <returns>For example <c lang="text">SINDRI_DB_STATE</c>.</returns>
    public static string Database(string name) => DatabasePrefix + Suffix(name);

    /// <summary>The variable for a disk.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>For example <c lang="text">SINDRI_DISK_SCRATCH</c>.</returns>
    public static string Disk(string name) => DiskPrefix + Suffix(name);

    /// <summary>The variable for a connection.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>For example <c lang="text">SINDRI_CONNECTION_GITHUB</c>.</returns>
    public static string Connection(string name) => ConnectionPrefix + Suffix(name);

    private static string Suffix(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.ToUpperInvariant().Replace('-', '_');
    }
}
