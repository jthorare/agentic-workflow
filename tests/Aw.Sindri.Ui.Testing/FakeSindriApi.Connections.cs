using Aw.Kernel;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The connection calls of the fake: in memory, seedable, each call recorded. No golden is served (the connection goldens are checked by the parity test).</summary>
internal sealed partial class FakeSindriApi
{
    private readonly Dictionary<string, ConnectionView> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConnectionView> _pendingSignIns = new(StringComparer.Ordinal);
    private int _signIns;
    private readonly List<AddConnectionBody> _addedConnections = [];
    private readonly List<string> _connectionCalls = [];

    /// <summary>The consent URL an oauth add or sign-in answers with.</summary>
    public string ConsentUrl { get; set; } = "https://provider.example.test/authorize?state=fake";

    /// <summary>When set, ListConnections waits for it before it answers; the rows it answers with are the ones at call time, so a test can change them meanwhile.</summary>
    public TaskCompletionSource? ConnectionListGate { get; set; }

    /// <summary>When set, the next GetConnection answers this error (once), as a dropped call would.</summary>
    public Error? NextConnectionGetError { get; set; }

    /// <summary>When set, every connection write (add, test, sign-in, remove) answers this error.</summary>
    public Error? ConnectionWriteError { get; set; }

    /// <summary>The outcome of TestConnection per name; a name not here passes with no tools.</summary>
    public Dictionary<string, ConnectionTested> ConnectionTests { get; } = new(StringComparer.Ordinal);

    /// <summary>Every body AddConnection was given, in order (the key as sent, so a test can check it was sent once).</summary>
    public IReadOnlyList<AddConnectionBody> AddedConnections { get { lock (_gate) { return [.. _addedConnections]; } } }

    /// <summary>The latest body AddConnection was given; null before the first.</summary>
    public AddConnectionBody? LastAddConnection { get { lock (_gate) { return _addedConnections.Count == 0 ? null : _addedConnections[^1]; } } }

    /// <summary>Every connection call, as 'Verb name' ('list', 'get linear:work', 'add x', 'test x', 'signin x', 'remove x').</summary>
    public IReadOnlyList<string> ConnectionCallLog { get { lock (_gate) { return [.. _connectionCalls]; } } }

    /// <summary>Makes a connection exist.</summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The same connection.</returns>
    public ConnectionView SeedConnection(ConnectionView connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        lock (_gate)
        {
            _connections[connection.Name] = connection;
        }

        return connection;
    }

    /// <summary>A connection row for a test.</summary>
    /// <param name="name">The name.</param>
    /// <param name="status">The status ('connected', 'needs-sign-in').</param>
    /// <param name="kind">The kind.</param>
    /// <param name="reason">Why it is not connected, or null.</param>
    /// <returns>The row.</returns>
    public static ConnectionView ConnectionRow(string name, string status = "connected", string kind = "oauth", string? reason = null) =>
        new(name, kind, name.Split(':')[0], status, reason, new Dictionary<string, string>(StringComparer.Ordinal), LastRefreshedAt: null, HasSecret: true);

    /// <summary>
    /// The browser callback of a sign-in lands, as the real server does it: a pending add becomes a 'connected' row for the first time, and a stored row (whatever its status) turns
    /// 'connected' with a new 'signedInAt'. Nothing else changes a row, and nothing before this call does.
    /// </summary>
    /// <param name="name">The connection's name.</param>
    public void CompleteSignIn(string name)
    {
        lock (_gate)
        {
            var stamp = "signed-in-" + (++_signIns).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var basis = _pendingSignIns.Remove(name, out var pending) ? pending : _connections[name];
            var config = new Dictionary<string, string>(basis.Config, StringComparer.Ordinal) { ["signedInAt"] = stamp };
            _connections[name] = basis with { Status = "connected", StatusReason = null, Config = config, HasSecret = true };
        }
    }

    public async Task<Result<ConnectionList>> ListConnections(Uri api, CancellationToken cancellationToken)
    {
        Record("list");
        var gate = ConnectionListGate;
        var answer = NotReceived<ConnectionList>(api) ?? new ConnectionList([.. Snapshot()]);   // the rows as they are at call time, even when the answer is held back
        if (gate is not null)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return answer;
    }

    public Task<Result<ConnectionOne>> GetConnection(Uri api, string name, CancellationToken cancellationToken)
    {
        Record("get " + name);
        if (NotReceived<ConnectionOne>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            if (NextConnectionGetError is { } dropped)
            {
                NextConnectionGetError = null;
                return Task.FromResult<Result<ConnectionOne>>(dropped);
            }

            if (!_connections.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<ConnectionOne>>(NoConnection(name));
            }

            return Task.FromResult<Result<ConnectionOne>>(new ConnectionOne(found));
        }
    }

    public Task<Result<ConnectionAdded>> AddConnection(Uri api, AddConnectionBody body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (NotReceived<ConnectionAdded>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            _addedConnections.Add(body);
            _connectionCalls.Add("add " + body.Name);
            if (ConnectionWriteError is { } error)
            {
                return Task.FromResult<Result<ConnectionAdded>>(error);
            }

            var (row, oauth) = NewRow(body);
            if (oauth)
            {
                _pendingSignIns[body.Name] = row;   // the server writes no row until the callback lands (OAuthSignInFlow.Insert)
            }
            else
            {
                _connections[body.Name] = row;
            }

            return Task.FromResult<Result<ConnectionAdded>>(new ConnectionAdded(row, oauth ? ConsentUrl : null));
        }
    }

    public Task<Result<ConnectionTested>> TestConnection(Uri api, string name, CancellationToken cancellationToken)
    {
        Record("test " + name);
        if (NotReceived<ConnectionTested>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            if (ConnectionWriteError is { } error)
            {
                return Task.FromResult<Result<ConnectionTested>>(error);
            }

            if (!_connections.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<ConnectionTested>>(NoConnection(name));
            }

            return Task.FromResult<Result<ConnectionTested>>(ConnectionTests.TryGetValue(name, out var own) ? own : new ConnectionTested(found, Ok: true, Reason: null, Tools: null));
        }
    }

    public Task<Result<ConnectionAdded>> SignInConnection(Uri api, string name, CancellationToken cancellationToken)
    {
        Record("signin " + name);
        if (NotReceived<ConnectionAdded>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            if (ConnectionWriteError is { } error)
            {
                return Task.FromResult<Result<ConnectionAdded>>(error);
            }

            if (!_connections.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<ConnectionAdded>>(NoConnection(name));
            }

            // the stored row stays as it is until the callback lands; only the text the caller sees says it is waiting (OAuthSignInFlow.Sign.Waiting)
            found = found with { StatusReason = "waiting for the browser sign-in" };
            return Task.FromResult<Result<ConnectionAdded>>(new ConnectionAdded(found, ConsentUrl));
        }
    }

    public Task<Result<ConnectionRemoved>> RemoveConnection(Uri api, string name, CancellationToken cancellationToken)
    {
        Record("remove " + name);
        if (NotReceived<ConnectionRemoved>(api) is { } unanswered)
        {
            return Task.FromResult(unanswered);
        }

        lock (_gate)
        {
            if (ConnectionWriteError is { } error)
            {
                return Task.FromResult<Result<ConnectionRemoved>>(error);
            }

            if (!_connections.Remove(name, out var gone))
            {
                return Task.FromResult<Result<ConnectionRemoved>>(NoConnection(name));
            }

            return Task.FromResult<Result<ConnectionRemoved>>(new ConnectionRemoved(name, gone.HasSecret));
        }
    }

    // The row an add makes, and whether it waits for a browser sign-in (oauth and mcp do).
    private static (ConnectionView Row, bool Oauth) NewRow(AddConnectionBody body)
    {
        var kind = body.Kind ?? "oauth";
        var oauth = string.Equals(kind, "oauth", StringComparison.Ordinal) || string.Equals(kind, "mcp", StringComparison.Ordinal);
        var row = new ConnectionView(
            body.Name,
            kind,
            body.Preset,
            oauth ? "needs-sign-in" : "connected",
            oauth ? "waiting for the browser sign-in" : null,
            new Dictionary<string, string>(StringComparer.Ordinal),
            LastRefreshedAt: null,
            HasSecret: body.Key is not null || body.KeyEnv is not null);
        return (row, oauth);
    }

    private static Error NoConnection(string name) => new("SND-CONN-001", ErrorKind.NotFound, $"No connection named {name}.");

    private List<ConnectionView> Snapshot()
    {
        lock (_gate)
        {
            return [.. _connections.Values.OrderBy(c => c.Name, StringComparer.Ordinal)];
        }
    }

    private void Record(string call)
    {
        lock (_gate)
        {
            _connectionCalls.Add(call);
        }
    }
}
