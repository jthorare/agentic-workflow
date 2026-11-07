using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Sends the linear adapter's operations with the key and reads Linear's replies into typed records (D18). Every request is one
/// 'POST' of '{query, variables}' to the profile's GraphQL url and nowhere else, so there is no route to allow or refuse. The key comes from calling the
/// environment with the settings' pointer text (Sindri resolves pointers, spec §4), once per client, off the calling thread and under the call's deadline,
/// and goes only into the Authorization header (a personal API key is sent as it is, without 'Bearer'), never into an error or a log line. Failures map to the catalogue: no usable key 002,
/// no answer in time or a 5xx 010, a 401 or 403 or an authentication error 071, a GraphQL error naming a missing entity 073, any other status, error or shape 072.
/// </summary>
/// <param name="settings">The checked settings.</param>
/// <param name="parts">The client, environment, clock and log every linear source shares.</param>
internal sealed class LinearClient(LinearSettings settings, LinearSourceParts parts)
{
    private const string TokenKey = "context.sources.linear.token";
    private const int PageSize = 250;
    private readonly LinearReadKey _key = new(settings.Token, parts.Environment);
    private readonly LinearCalls _calls = new(settings, parts.Logger, $"Create a read-only personal API key, store it behind {TokenKey} and approve the profile again (spec §8.8).");

    /// <summary>The checked settings.</summary>
    public LinearSettings Settings { get; } = settings;

    /// <summary>Runs one operation.</summary>
    /// <param name="operation">The operation's name ('Projects', 'Issue', ...), as embedded.</param>
    /// <param name="variables">Its variables.</param>
    /// <param name="deadline">How long Linear gets to answer.</param>
    /// <param name="cancellationToken">The caller's cancellation, which is thrown, not turned into an error.</param>
    /// <typeparam name="T">The data record of the operation.</typeparam>
    /// <returns>The data, or an error from the catalogue.</returns>
    public async ValueTask<Result<T>> Query<T>(string operation, JsonObject variables, TimeSpan deadline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variables);
        cancellationToken.ThrowIfCancellationRequested();
        if (deadline <= TimeSpan.Zero)
        {
            return _calls.Failed(operation, _calls.Unavailable($"wasn't called: the query's deadline was spent before {operation}"), status: null);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        try
        {
            var key = await _key.Get(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(key) || key.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
            {
                return _calls.Failed(operation, ContextErrors.SourceNotConfigured.With(
                    $"{TokenKey}: the {Scheme(Settings.Token)}: secret pointer doesn't resolve to a usable Linear key (none, or one with spaces or control characters); "
                    + "'sindri doctor' shows why (its SND-SECRET code names the target and the fix). Point it at a read-only personal API key (spec §8.8)."), status: null);
            }

            var body = AwJson.Serialize(new LinearRequest(LinearOperations.Text(operation), variables), LinearWire.Options);
            using var request = new HttpRequestMessage(HttpMethod.Post, Settings.Url) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            _ = request.Headers.TryAddWithoutValidation("Authorization", key);
            using var response = await parts.Http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var reply = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);
            return _calls.Read<T>(operation, response.StatusCode, reply);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var what = _key.Settled
                ? $"didn't answer {operation}"
                : $"wasn't called: the {TokenKey} pointer didn't resolve (a locked keychain, or op waiting for a sign-in?)";
            return _calls.Failed(operation, _calls.Unavailable(string.Create(CultureInfo.InvariantCulture, $"{what} within {deadline.TotalSeconds:0.###} s")), status: null);
        }
        catch (HttpRequestException e)
        {
            return _calls.Failed(operation, _calls.Unavailable($"isn't reachable ({e.HttpRequestError}); start the fixture or check context.sources.linear.url"), status: null);
        }
    }

    /// <summary>
    /// Reads a list a page at a time until the wanted nodes are in hand, Linear says there are no more, with no page limit: the shared deadline bounds the read (owner q93, 2026-10-23).
    /// The filter is copied for each page; <paramref name="left"/> is asked before every call, so the pages share one deadline.
    /// </summary>
    /// <param name="list">What to read.</param>
    /// <param name="left">What is left of the deadline.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <typeparam name="TData">The operation's data record.</typeparam>
    /// <typeparam name="TNode">The node record.</typeparam>
    /// <returns>The nodes in the order Linear gave them, or the first error.</returns>
    public async ValueTask<Result<List<TNode>>> Pages<TData, TNode>(LinearList<TData, TNode> list, Func<TimeSpan> left, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(left);
        List<TNode> nodes = [];
        string? after = null;
        while (true)
        {
            var variables = LinearFilters.Variables((JsonObject?)list.Filter?.DeepClone(), Math.Clamp(list.Want - nodes.Count, 1, PageSize), after);
            var data = await Query<TData>(list.Operation, variables, left(), cancellationToken).ConfigureAwait(false);
            if (data.IsError)
            {
                return data.Error;
            }

            var connection = list.Pick(data.Value);
            nodes.AddRange(connection.Nodes);
            if (nodes.Count >= list.Want || connection.PageInfo is not { HasNextPage: true, EndCursor: { } cursor })
            {
                return nodes.Count > list.Want ? nodes[..list.Want] : nodes;
            }

            after = cursor;
        }
    }

    private static string Scheme(string pointer) => pointer[..pointer.IndexOf(':', StringComparison.Ordinal)];
}
