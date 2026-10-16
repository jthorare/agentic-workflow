using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL 15 container from the image already on the box (never pulled), published on a free loopback port and removed
/// when the tests are done. It holds a notes table, a sequence, a SECURITY DEFINER function that inserts, and four roles: a read-only
/// one and three that must be refused. The secrets are random per run and never leave the process.
/// </summary>
public sealed class PostgresContainer : IAsyncLifetime
{
    internal const string Image = "postgres:15.2-alpine";
    internal const string Database = "fixture";

    private string _id = string.Empty;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal string AdminSecret { get; } = "a" + Guid.NewGuid().ToString("N");

    internal string ReaderSecret { get; } = "r" + Guid.NewGuid().ToString("N");

    internal int Port { get; private set; }

    internal string ServerAddress { get; private set; } = string.Empty;

    internal string AdminConnectionString => Connect("postgres", AdminSecret);

    internal string Connect(string user, string secret) =>
        new NpgsqlConnectionStringBuilder { Host = "127.0.0.1", Port = Port, Database = Database, Username = user, Password = secret, Pooling = false, SslMode = SslMode.Disable }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var published = $"127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}:5432";
        _id = (await Docker("run", "-d", "--rm", "-p", published, "-e", $"POSTGRES_PASSWORD={AdminSecret}", "-e", $"POSTGRES_DB={Database}", Image, "-c", "log_statement=all")).Trim();
        await WaitUntilReadyAsync();
        await ExecuteAsync(Setup());
        ServerAddress = (string)(await ScalarAsync("SELECT host(inet_server_addr())"))!;   // why: a TCP connection always has a server address
    }

    public async ValueTask DisposeAsync()
    {
        if (_id.Length > 0)
        {
            _ = await Docker("rm", "-f", _id);
        }
    }

    internal async Task ExecuteAsync(string sql)
    {
        var connection = new NpgsqlConnection(AdminConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(Ct);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                _ = await command.ExecuteNonQueryAsync(Ct);
            }
        }
    }

    internal async Task<object?> ScalarAsync(string sql)
    {
        var connection = new NpgsqlConnection(AdminConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(Ct);
            var command = new NpgsqlCommand(sql, connection);
            await using (command.ConfigureAwait(false))
            {
                return await command.ExecuteScalarAsync(Ct);
            }
        }
    }

    /// <summary>The server's log so far (log_statement=all: every statement any connection ran).</summary>
    internal Task<string> LogAsync() => Docker("logs", _id);

    /// <summary>Whether a file exists inside the container.</summary>
    internal async Task<bool> FileExistsAsync(string path)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", _id, "test", "-e", path })
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;   // why: docker is on the box (the heavy lane's precondition)
        await process.WaitForExitAsync(Ct);
        return process.ExitCode == 0;
    }

    private async Task WaitUntilReadyAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (true)
        {
            try
            {
                var connection = new NpgsqlConnection(AdminConnectionString);
                await using (connection.ConfigureAwait(false))
                {
                    await connection.OpenAsync(Ct);
                    return;
                }
            }
            catch (Exception exception) when (exception is NpgsqlException or SocketException or IOException)
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("the throwaway postgres did not accept connections in 90 s", exception);
                }

                await Task.Delay(500, Ct);
            }
        }
    }

    private static async Task<string> Docker(params string[] args)
    {
        var start = new ProcessStartInfo("docker") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args)
        {
            start.ArgumentList.Add(arg);
        }

        using var process = Process.Start(start)!;   // why: docker is on the box (the heavy lane's precondition)
        var output = process.StandardOutput.ReadToEndAsync(Ct);
        var error = process.StandardError.ReadToEndAsync(Ct);
        await process.WaitForExitAsync(Ct);
        return process.ExitCode == 0 || args[0] == "logs" ? await output + await error : throw new InvalidOperationException($"docker {args[0]} failed: {await error}");
    }

    private string Setup() => string.Join(
        '\n',
        "CREATE TABLE notes(id serial PRIMARY KEY, text text NOT NULL);",
        "CREATE FUNCTION bump() RETURNS int LANGUAGE sql SECURITY DEFINER AS $$ INSERT INTO notes(text) VALUES ('bumped') RETURNING id $$;",
        $"CREATE ROLE evidence_ro LOGIN NOSUPERUSER PASSWORD '{ReaderSecret}';",
        "GRANT SELECT ON notes TO evidence_ro;",
        "GRANT USAGE, SELECT ON SEQUENCE notes_id_seq TO evidence_ro;",
        $"CREATE ROLE evidence_rw LOGIN NOSUPERUSER PASSWORD '{ReaderSecret}';",
        "GRANT SELECT, INSERT ON notes TO evidence_rw;",
        $"CREATE ROLE evidence_files LOGIN NOSUPERUSER PASSWORD '{ReaderSecret}';",
        "GRANT SELECT ON notes TO evidence_files;",
        "GRANT pg_write_server_files TO evidence_files;",
        "CREATE TABLE secrets(id int, api_key text);",
        "INSERT INTO secrets VALUES (1, 'ak-PLANTED-pgcol-123456');",
        "GRANT SELECT ON secrets TO evidence_ro;",
        "CREATE TABLE hidden(id int);",
        "CREATE TABLE blobs(label text, secret_key bytea, api_tokens text[]);",
        "INSERT INTO blobs VALUES ('one', '\\xdeadbeef1234', ARRAY['tok-one-123456', 'tok-two-123456']);",
        "GRANT SELECT ON blobs TO evidence_ro;",
        "CREATE ROLE writers NOLOGIN;",
        "GRANT INSERT, DELETE ON notes TO writers;",
        $"CREATE ROLE evidence_member LOGIN NOSUPERUSER PASSWORD '{ReaderSecret}' IN ROLE writers;",
        "GRANT SELECT ON notes TO evidence_member;");
}
