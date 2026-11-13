using System.Globalization;
using Aw.Kernel.Data;
using Aw.Sindri.Core.Launches;
using Microsoft.Data.Sqlite;

namespace Aw.Sindri.Infrastructure.Launch.Usage;

/// <summary>
/// Keeps a usage reader's per-transcript read state on disk (#3420), so a restarted serve resumes each transcript where the last run stopped
/// instead of reading today's files from byte 0. One SQLite file per reader, 'directory/reader.db'. It is a cache: a file with another
/// version, or one that can't be read, is deleted and the reader starts empty; it never stops a read.
/// </summary>
/// <param name="directory">The directory the state files live in.</param>
internal sealed class UsageReadCache(string directory)
{
    private const int Version = 1;
    private const string Stamp = "PRAGMA user_version = 1";
    private const string Schema =
        "CREATE TABLE IF NOT EXISTS files (path TEXT NOT NULL PRIMARY KEY, offset INTEGER NOT NULL);" +
        "CREATE TABLE IF NOT EXISTS messages (path TEXT NOT NULL, id TEXT NOT NULL, at TEXT, model TEXT NOT NULL, input INTEGER NOT NULL, cache_read INTEGER NOT NULL, cache_write INTEGER NOT NULL, output INTEGER NOT NULL, PRIMARY KEY (path, id));";

    /// <summary>The directory the state files live in.</summary>
    public string Directory { get; } = directory;

    /// <summary>How many times <see cref="Save"/> wrote to the file.</summary>
    public int Writes { get; private set; }

    /// <summary>The saved state of <paramref name="reader"/>, per transcript path.</summary>
    /// <param name="reader">The reader's name.</param>
    /// <returns>The saved states; empty when there are none, or when the file was unusable (it is deleted then).</returns>
    public IReadOnlyDictionary<string, FileState> Load(string reader)
    {
        ArgumentException.ThrowIfNullOrEmpty(reader);
        var path = PathOf(reader);
        var states = new Dictionary<string, FileState>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return states;
        }

        try
        {
            using var connection = Open(path, create: false);
            ReadFiles(connection, states);
            ReadMessages(connection, states);
            return states;
        }
        catch (Exception e) when (e is SqliteException or InvalidDataException or FormatException or OverflowException)
        {
            Delete(path);
            return new Dictionary<string, FileState>(StringComparer.Ordinal);
        }
    }

    /// <summary>Writes what changed: the transcripts marked dirty, and the removal of the ones no longer in <paramref name="files"/>. Nothing is written when nothing changed.</summary>
    /// <param name="reader">The reader's name.</param>
    /// <param name="files">The state per transcript path.</param>
    /// <exception cref="InvalidDataException">The file was unusable; it has been deleted and the caller should write everything again.</exception>
    public void Save(string reader, IReadOnlyDictionary<string, FileState> files)
    {
        ArgumentException.ThrowIfNullOrEmpty(reader);
        ArgumentNullException.ThrowIfNull(files);
        var path = PathOf(reader);
        try
        {
            using var connection = Open(path, create: true);
            var gone = SavedPaths(connection).Where(p => !files.ContainsKey(p)).ToList();
            var dirty = files.Where(static f => f.Value.Dirty).ToList();
            if (gone.Count == 0 && dirty.Count == 0)
            {
                return;
            }

            using var transaction = connection.BeginTransaction();
            foreach (var removed in gone)
            {
                Forget(connection, transaction, removed);
            }

            foreach (var (file, state) in dirty)
            {
                Forget(connection, transaction, file);
                Write(connection, transaction, file, state);
            }

            transaction.Commit();
            Writes++;
        }
        catch (SqliteException e)
        {
            Delete(path);
            throw new InvalidDataException($"The usage read cache {path} could not be written", e);
        }
    }

    private string PathOf(string reader) => Path.Combine(Directory, reader + ".db");

    private static SqliteConnection Open(string path, bool create)
    {
        if (create)
        {
            _ = System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        }

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        try
        {
            connection.Open();
            if (create)
            {
                _ = Run(connection, "PRAGMA journal_mode = WAL");
                _ = Run(connection, "PRAGMA synchronous = NORMAL");
            }

            var version = Convert.ToInt32(Run(connection, "PRAGMA user_version"), CultureInfo.InvariantCulture);
            if (version == 0 && create)
            {
                _ = Run(connection, Schema + Stamp);
            }
            else if (version != Version)
            {
                throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture, $"The usage read cache {path} has version {version}, this build reads {Version}"));
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    // only string constants of this class reach it
#pragma warning disable CA2100
    private static object? Run(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
#pragma warning restore CA2100

    private static void ReadFiles(SqliteConnection connection, Dictionary<string, FileState> states)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, offset FROM files";
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            states[rows.GetString(0)] = new FileState { Offset = rows.GetInt64(1) };
        }
    }

    private static void ReadMessages(SqliteConnection connection, Dictionary<string, FileState> states)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path, id, at, model, input, cache_read, cache_write, output FROM messages";
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            if (states.TryGetValue(rows.GetString(0), out var state))
            {
                DateTimeOffset? at = rows.IsDBNull(2) ? null : DateTimeOffset.Parse(rows.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                state.Messages[rows.GetString(1)] = (at, new ModelTokens(rows.GetString(3), rows.GetInt64(4), rows.GetInt64(5), rows.GetInt64(6), rows.GetInt64(7)));
            }
        }
    }

    private static List<string> SavedPaths(SqliteConnection connection)
    {
        var paths = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT path FROM files";
        using var rows = command.ExecuteReader();
        while (rows.Read())
        {
            paths.Add(rows.GetString(0));
        }

        return paths;
    }

#pragma warning disable CA2100 // two string constants
    private static void Forget(SqliteConnection connection, SqliteTransaction transaction, string file)
    {
        foreach (var table in new[] { "DELETE FROM messages WHERE path = $path", "DELETE FROM files WHERE path = $path" })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = table;
            _ = command.Bind("$path", file);
            _ = command.ExecuteNonQuery();
        }
    }
#pragma warning restore CA2100

    private static void Write(SqliteConnection connection, SqliteTransaction transaction, string file, FileState state)
    {
        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = "INSERT INTO files (path, offset) VALUES ($path, $offset)";
        _ = upsert.Bind("$path", file).Bind("$offset", state.Offset);
        _ = upsert.ExecuteNonQuery();
        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO messages (path, id, at, model, input, cache_read, cache_write, output) VALUES ($path, $id, $at, $model, $input, $read, $write, $output)";
        foreach (var (id, (at, tokens)) in state.Messages)
        {
            insert.Parameters.Clear();
            _ = insert.Bind("$path", file).Bind("$id", id).Bind("$at", at?.ToString("O", CultureInfo.InvariantCulture)).Bind("$model", tokens.Model)
                .Bind("$input", tokens.Input).Bind("$read", tokens.CacheRead).Bind("$write", tokens.CacheWrite).Bind("$output", tokens.Output);
            _ = insert.ExecuteNonQuery();
        }
    }

    private static void Delete(string path)
    {
        foreach (var file in new[] { path, path + "-wal", path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // a cache file that can't be removed is left; the next open fails the same way and the reader goes on without it
            }
        }
    }
}
