using Aw.Kernel;
using Aw.Sindri.Ui.Api.Connections;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The connection calls of the fake: in memory, seedable, each call recorded. No golden is served (the connection goldens are checked by the parity test).</summary>
internal sealed partial class FakeSindriApi
{
    private readonly Dictionary<string, ConnectionView> _connections = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pollsLeft = new(StringComparer.Ordinal);
    private readonly List<AddConnectionBody> _addedConnections = [];
    private readonly List<string> _connectionCalls = [];

    /// <summary>The consent URL an oauth add or sign-in answers with.</summary>
    public string ConsentUrl { get; set; } = "https://provider.example.test/authorize?state=fake";

    /// <summary>How many GetConnection reads an oauth connection stays 'needs-sign-in' for after an add or sign-in; then it reads 'connected'.</summary>
    public int ConnectsAfterPolls { get; set; } = 2;

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

    public Task<Result<ConnectionList>> ListConnections(Uri api, CancellationToken cancellationToken)
    {
        Record("list");
        return Task.FromResult(NotReceived<ConnectionList>(api) ?? new ConnectionList([.. Snapshot()]));
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
            if (!_connections.TryGetValue(name, out var found))
            {
                return Task.FromResult<Result<ConnectionOne>>(NoConnection(name));
            }

            if (_pollsLeft.TryGetValue(name, out var left))
            {
                if (left <= 1)
                {
                    _ = _pollsLeft.Remove(name);
                    found = _connections[name] = found with { Status = "connected", StatusReason = null };
                }
                else
                {
                    _pollsLeft[name] = left - 1;
                }
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

            var oauth = (body.Kind ?? "oauth") == "oauth";
            var row = new ConnectionView(
                body.Name,
                body.Kind ?? "oauth",
                body.Preset,
                oauth ? "needs-sign-in" : "connected",
                oauth ? "waiting for the browser sign-in" : null,
                new Dictionary<string, string>(StringComparer.Ordinal),
                LastRefreshedAt: null,
                HasSecret: body.Key is not null || body.KeyEnv is not null);
            _connections[body.Name] = row;
            if (oauth)
            {
                _pollsLeft[body.Name] = Math.Max(1, ConnectsAfterPolls);
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

            found = _connections[name] = found with { Status = "needs-sign-in", StatusReason = "waiting for the browser sign-in" };
            _pollsLeft[name] = Math.Max(1, ConnectsAfterPolls);
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

            _ = _pollsLeft.Remove(name);
            return Task.FromResult<Result<ConnectionRemoved>>(new ConnectionRemoved(name, gone.HasSecret));
        }
    }

    private static Error NoConnection(string name) => new("SND-CONN-404", ErrorKind.NotFound, $"No connection named {name}.");

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
