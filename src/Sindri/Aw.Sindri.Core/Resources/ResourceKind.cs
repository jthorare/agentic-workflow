namespace Aw.Sindri.Core.Resources;

/// <summary>The kinds of resource that live on disk.</summary>
public enum ResourceKind
{
    /// <summary>A SQLite file.</summary>
    Database = 1,

    /// <summary>A folder with a size limit.</summary>
    Disk = 2,
}
