using System.Net;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// <see cref="IT3Api"/> over T3 Code's MCP endpoint ('/mcp', Streamable HTTP), the same tools T3's own 't3 thread' CLI calls. One client is kept and
/// rebuilt when the token is renewed or T3 restarts on another port; a call that throws drops it, so the next call connects again.
/// </summary>
/// <param name="credentials">The endpoint and token.</param>
/// <param name="options">The call budget.</param>
internal sealed class T3McpApi(T3Credentials credentials, T3Options options) : IT3Api, IAsyncDisposable, IDisposable
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(T3JsonContext.Default);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Connection? _connection;

    /// <inheritdoc/>
    public async ValueTask<Result<T3Environment>> Environment(CancellationToken cancellationToken)
    {
        var read = await Call<T3Wire.NoArgs, T3Wire.Environment>("t3_environment_read", new T3Wire.NoArgs(), cancellationToken).ConfigureAwait(false);
        return read.IsError ? read.Error : read.Value.Value is { } environment ? new T3Environment(environment.EnvironmentId, environment.ServerVersion) : Failed("t3_environment_read", read.Value);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<IReadOnlyList<T3Project>>> Projects(CancellationToken cancellationToken)
    {
        var projects = new List<T3Wire.Project>();
        int? cursor = 0;
        while (cursor is { } at)
        {
            var page = await Call<T3Wire.ProjectListArgs, T3Wire.ProjectList>("t3_project_list", new T3Wire.ProjectListArgs(at, 100), cancellationToken).ConfigureAwait(false);
            if (page.IsError)
            {
                return page.Error;
            }

            if (page.Value.Value is not { } list)
            {
                return Failed("t3_project_list", page.Value);
            }

            projects.AddRange(list.Projects);
            cursor = list.NextCursor;
        }

        return projects.Select(static p => new T3Project(p.Id, p.WorkspaceRoot, p.RepositoryIdentity?.CanonicalKey)).ToList();
    }

    /// <inheritdoc/>
    public async ValueTask<Result<T3Model?>> ModelFor(string driverKind, CancellationToken cancellationToken)
    {
        var read = await Call<T3Wire.NoArgs, T3Wire.Capabilities>("orchestrator_capabilities", new T3Wire.NoArgs(), cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value.Value is { } capabilities ? T3Providers.ModelFor(capabilities, driverKind) : Failed("orchestrator_capabilities", read.Value);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<string>> Launch(T3LaunchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var workspace = request.WorktreePath is { } path ? new T3Wire.Workspace("existing_worktree", path) : null;
        var model = request.Model is { } chosen ? new T3Wire.ModelSelection(chosen.InstanceId, chosen.Model) : null;
        var launched = await Call<T3Wire.LaunchArgs, T3Wire.Launched>("t3_thread_launch", new T3Wire.LaunchArgs(request.ProjectId, request.Title, request.Message, workspace, model), cancellationToken).ConfigureAwait(false);
        return launched.IsError ? launched.Error : launched.Value.Value is { } thread ? thread.ThreadId : Failed("t3_thread_launch", launched.Value);
    }

    /// <inheritdoc/>
    public async ValueTask<Result<T3ThreadState?>> Read(string threadId, CancellationToken cancellationToken)
    {
        var read = await Call<T3Wire.ThreadArgs, T3Wire.ThreadRead>("t3_thread_read", new T3Wire.ThreadArgs(threadId), cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value switch
        {
            { Value: { } found } => new T3ThreadState(found.Thread.Status, found.Thread.Archived),
            { Failure.Code: "thread_not_found" } => (T3ThreadState?)null,
            _ => Failed("t3_thread_read", read.Value),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<Result<Unit>> Interrupt(string threadId, CancellationToken cancellationToken)
    {
        var stopped = await Call<T3Wire.ThreadArgs, JsonElement>("t3_thread_interrupt", new T3Wire.ThreadArgs(threadId), cancellationToken).ConfigureAwait(false);
        return stopped.IsError ? stopped.Error : stopped.Value.Failure is { Code: not "thread_not_found" } ? Failed("t3_thread_interrupt", stopped.Value) : Unit.Value;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_connection is { } connection)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _connection = null;
        _gate.Dispose();
    }

    /// <inheritdoc/>
    /// <remarks>A container disposed synchronously: the HTTP client goes now, and the MCP session's close is started and left to finish.</remarks>
    public void Dispose()
    {
        if (_connection is { } connection)
        {
            _ = connection.Client.DisposeAsync().AsTask();
            connection.Http.Dispose();
        }

        _connection = null;
        _gate.Dispose();
    }

    private static Error Failed<T>(string tool, T3Reply<T> reply) =>
        SindriErrors.LaunchT3Unavailable.With(reply.Failure is { } failure ? $"{tool}: {failure.Code}: {failure.Message}" : $"{tool} answered without a result");

    /// <summary>One tool call within the call budget: the structured result, T3's own failure, or SND-LAUNCH-025 when T3 couldn't be asked.</summary>
    private async ValueTask<Result<T3Reply<TResult>>> Call<TArgs, TResult>(string tool, TArgs arguments, CancellationToken cancellationToken)
    {
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bound.CancelAfter(options.CallBudget);
        var connection = await Connect(bound.Token).ConfigureAwait(false);
        if (connection.IsError)
        {
            return connection.Error;
        }

        CallToolResult result;
        try
        {
            result = await connection.Value.Client.CallToolAsync(new CallToolRequestParams { Name = tool, Arguments = T3Arguments.Of(arguments, _json) }, bound.Token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)   // boundary: any transport failure means T3 couldn't be asked
        {
            await Reset(e).ConfigureAwait(false);
            return SindriErrors.LaunchT3Unavailable.With($"{tool} failed: {e.Message} (fix: open T3 Code)");
        }

        return T3Replies.Read<TResult>(result, _json);
    }

    private async ValueTask<Result<Connection>> Connect(CancellationToken cancellationToken)
    {
        var endpoint = credentials.Endpoint();
        if (endpoint.IsError)
        {
            return endpoint.Error;
        }

        var token = await credentials.Token(cancellationToken).ConfigureAwait(false);
        if (token.IsError)
        {
            return token.Error;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_connection is { } held && held.Endpoint == endpoint.Value && string.Equals(held.Token, token.Value, StringComparison.Ordinal))
            {
                return held;
            }

            if (_connection is { } stale)
            {
                await stale.DisposeAsync().ConfigureAwait(false);
                _connection = null;
            }

            _connection = await Connection.Open(endpoint.Value, token.Value, cancellationToken).ConfigureAwait(false);
            return _connection;
        }
        catch (Exception e) when (e is not OperationCanceledException)   // boundary: initialize failed, T3 isn't answering
        {
            return SindriErrors.LaunchT3Unavailable.With($"T3 Code at {endpoint.Value} didn't answer: {e.Message} (fix: open T3 Code)");
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask Reset(Exception cause)
    {
        if (cause is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized })
        {
            credentials.Drop();   // revoked or expired early: mint a new one next time
        }

        await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (_connection is { } connection)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            _connection = null;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>An open MCP session with the endpoint and token it was opened with.</summary>
    private sealed record Connection(Uri Endpoint, string Token, McpClient Client, HttpClient Http) : IAsyncDisposable
    {
        public static async ValueTask<Connection> Open(Uri endpoint, string token, CancellationToken cancellationToken)
        {
            HttpClient? http = null;
            HttpClientTransport? transport = null;
            try
            {
                http = Loopback();
#pragma warning disable CA2000 // disposed in the finally below unless the client took it over; the analyzer only follows Dispose, and this type is disposed with DisposeAsync
                transport = new HttpClientTransport(
                    new HttpClientTransportOptions
                    {
                        Endpoint = endpoint,
                        TransportMode = HttpTransportMode.StreamableHttp,
                        AdditionalHeaders = new Dictionary<string, string>(StringComparer.Ordinal) { ["Authorization"] = "Bearer " + token },
                    },
                    http, loggerFactory: null, ownsHttpClient: false);
#pragma warning restore CA2000
                var options = new McpClientOptions { ClientInfo = new Implementation { Name = "sindri", Version = "1" } };
                var client = await McpClient.CreateAsync(transport, options, loggerFactory: null, cancellationToken).ConfigureAwait(false);
                transport = null;   // the client owns it now
                var connection = new Connection(endpoint, token, client, http);
                http = null;   // the connection owns it now
                return connection;
            }
            finally
            {
                if (transport is not null)
                {
                    await transport.DisposeAsync().ConfigureAwait(false);
                }

                http?.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync().ConfigureAwait(false);
            Http.Dispose();
        }

        /// <summary>No proxy and no redirect: the endpoint is T3 on this box and nowhere else.</summary>
#pragma warning disable CA2000 // the client owns the handler and disposes it
        private static HttpClient Loopback() => new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false });
#pragma warning restore CA2000
    }
}
