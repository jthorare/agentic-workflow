using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Toolkit;

/// <summary>What a tool handler gets besides its request: the resources the toolkit declared, read from the environment Sindri set.</summary>
public sealed class ToolkitContext
{
    private readonly IReadOnlyList<ResourceNeed> _needs;
    private readonly Func<string, string?> _environment;

    /// <summary>Makes a context over a declared set of resources.</summary>
    /// <param name="needs">The declared resources.</param>
    /// <param name="environment">Reads one environment variable; null when it is not set.</param>
    public ToolkitContext(IReadOnlyList<ResourceNeed> needs, Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(environment);
        _needs = needs;
        _environment = environment;
    }

    /// <summary>Opens the SQLite file of a declared database; the file must already exist.</summary>
    /// <param name="name">The database name.</param>
    /// <returns>An open read-write connection; the caller disposes it.</returns>
    public SqliteConnection Database(string name)
    {
        var path = Value(ResourceKind.Database, name);
        if (!File.Exists(path))
        {
            throw new ToolkitException(ToolkitErrors.ResourceUnavailable, $"Database '{name}' is not at {path}; Sindri creates it when it provisions the toolkit.");
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch (SqliteException ex)
        {
            connection.Dispose();
            throw new ToolkitException(ToolkitErrors.ResourceUnavailable, $"Database '{name}' at {path} did not open: {ex.Message}");
        }
    }

    /// <summary>The folder of a declared disk.</summary>
    /// <param name="name">The disk name.</param>
    /// <returns>The folder path.</returns>
    public string DiskPath(string name)
    {
        var path = Value(ResourceKind.Disk, name);
        return Directory.Exists(path)
            ? path
            : throw new ToolkitException(ToolkitErrors.ResourceUnavailable, $"Disk '{name}' is not at {path}; Sindri creates it when it provisions the toolkit.");
    }

    /// <summary>The value Sindri set for a declared connection.</summary>
    /// <param name="name">The connection name.</param>
    /// <returns>The value.</returns>
    public string Connection(string name) => Value(ResourceKind.Connection, name);

    private string Value(ResourceKind kind, string name)
    {
        var need = _needs.FirstOrDefault(n => n.Kind == kind && string.Equals(n.Name, name, StringComparison.Ordinal))
            ?? throw new ToolkitException(ToolkitErrors.InvalidDeclaration, $"The toolkit did not declare {kind} '{name}'; add it to Needs(...).");
        return _environment(need.Variable) is { Length: > 0 } value
            ? value
            : throw new ToolkitException(ToolkitErrors.ResourceMissing, $"{need.Variable} is not set; Sindri sets it when it provisions {kind} '{name}'.");
    }
}
