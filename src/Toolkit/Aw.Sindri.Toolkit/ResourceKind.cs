namespace Aw.Sindri.Toolkit;

/// <summary>What kind of resource a toolkit needs.</summary>
public enum ResourceKind
{
    /// <summary>A SQLite file, in SINDRI_DB_&lt;NAME&gt;.</summary>
    Database,

    /// <summary>A folder with a size limit, in SINDRI_DISK_&lt;NAME&gt;.</summary>
    Disk,

    /// <summary>A named connection, in SINDRI_CONNECTION_&lt;NAME&gt;.</summary>
    Connection,
}
