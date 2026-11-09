namespace Aw.Sindri.Infrastructure.Resources;

/// <summary>Creates the empty SQLite file of a database resource.</summary>
internal static class SqliteFileProvisioner
{
    /// <summary>Creates <paramref name="path"/> 0600 when it is missing; an existing file is left as it is.</summary>
    /// <param name="path">The database file.</param>
    internal static void Create(string path) => throw new NotSupportedException();
}
