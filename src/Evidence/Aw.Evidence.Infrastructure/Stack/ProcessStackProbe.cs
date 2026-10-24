using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Stack;
using Aw.Evidence.Infrastructure.Processes;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Stack;

/// <summary>
/// Probes the local stack: a page's &lt;title&gt; over HTTP (5 s), the database through
/// 'docker exec &lt;container&gt; pg_isready -U &lt;user&gt;' (no container configured: nothing to probe, so ready), and a
/// TCP connect to a port.
/// </summary>
internal sealed partial class ProcessStackProbe(string docker, DatabaseSettings settings) : IStackProbe, IDisposable
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http = new() { Timeout = _limit };

    /// <inheritdoc/>
    public async ValueTask<Result<string>> PageTitleAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            var title = Title.Match(html);
            return title.Success
                ? WebUtility.HtmlDecode(title.Groups["text"].Value).Trim()
                : EvidenceErrors.ProbeFailed.With($"{url} has no <title>");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return EvidenceErrors.ProbeFailed.With($"{url} did not answer: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> DatabaseReadyAsync(CancellationToken cancellationToken)
    {
        if (settings.Container is not { } container)
        {
            return Results.Ok();
        }

        var ready = await ProcessRunner.RunAsync(
            [docker, "exec", container, "pg_isready", "-U", settings.User], standardInput: null, TimeSpan.FromSeconds(15), EvidenceErrors.ProbeFailed, cancellationToken)
            .ConfigureAwait(false);
        return ready.Bind(outcome => outcome.ExitCode == 0
            ? Results.Ok()
            : EvidenceErrors.ProbeFailed.With(string.Create(CultureInfo.InvariantCulture, $"the database is not ready (pg_isready exit {outcome.ExitCode}): {outcome.StandardOutput.Trim()}{outcome.StandardError.Trim()}")));
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> PortOpenAsync(string host, int port, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(_limit);
        try
        {
            await client.ConnectAsync(host, port, limit.Token).ConfigureAwait(false);
            return Results.Ok();
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return EvidenceErrors.ProbeFailed.With(string.Create(CultureInfo.InvariantCulture, $"nothing accepts connections on {host}:{port}: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> DatabaseExistsAsync(string database, CancellationToken cancellationToken)
    {
        if (settings.Container is not { } container)
        {
            return Results.Ok();   // no container configured: nothing to probe, as for DatabaseReadyAsync
        }

        if (!DatabaseName.IsMatch(database))
        {
            return EvidenceErrors.ProbeFailed.With($"{database} is not a database name of letters, digits and _");   // it goes into a SQL literal
        }

        var query = await ProcessRunner.RunAsync(
            [docker, "exec", container, "psql", "-U", settings.User, "-d", "postgres", "-tAc", $"SELECT 1 FROM pg_database WHERE datname = '{database}'"],
            standardInput: null, TimeSpan.FromSeconds(15), EvidenceErrors.ProbeFailed, cancellationToken).ConfigureAwait(false);
        return query.Bind(outcome => outcome.ExitCode == 0 && outcome.StandardOutput.Trim() == "1"
            ? Results.Ok()
            : EvidenceErrors.ProbeFailed.With($"database {database} does not exist in {container}"));
    }

    // lsof names the listener's process (-t) and then that process's current directory (-d cwd, -Fn: one 'n<path>' line).
    /// <inheritdoc/>
    public async ValueTask<Result<string?>> ListenerDirectoryAsync(int port, CancellationToken cancellationToken)
    {
        var listener = await ProcessRunner.RunAsync(
            ["lsof", "-nP", $"-iTCP:{port.ToString(CultureInfo.InvariantCulture)}", "-sTCP:LISTEN", "-t"], standardInput: null, _limit, EvidenceErrors.ProbeFailed, cancellationToken).ConfigureAwait(false);
        if (listener.IsError)
        {
            return listener.Error;
        }

        var pid = listener.Value.StandardOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (pid is null || !int.TryParse(pid, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return (string?)null;   // nobody listens (lsof exits 1 with no output)
        }

        var directory = await ProcessRunner.RunAsync(
            ["lsof", "-a", "-p", pid, "-d", "cwd", "-Fn"], standardInput: null, _limit, EvidenceErrors.ProbeFailed, cancellationToken).ConfigureAwait(false);
        return directory.Map(static outcome => outcome.StandardOutput.Split('\n', StringSplitOptions.TrimEntries)
            .Where(static line => line.StartsWith('n'))
            .Select(static line => line[1..])
            .FirstOrDefault());
    }

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();

    [GeneratedRegex("^[A-Za-z0-9_]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DatabaseName { get; }

    [GeneratedRegex("<title[^>]*>(?<text>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Title { get; }
}
