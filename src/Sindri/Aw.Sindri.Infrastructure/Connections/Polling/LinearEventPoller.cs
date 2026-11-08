using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.Polling;

namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>
/// IEventPoller for Linear over a plain HttpClient: one GraphQL 'issues' query with each issue's history. The delivery key is the history
/// entry id plus what changed, so it is the same on every poll. The token goes only to api.linear.app and a redirect is refused
/// (SND-CONN-012). Provider text in the events is untrusted.
/// </summary>
/// <param name="http">The HTTP client; it must not follow redirects on its own.</param>
/// <param name="secrets">The connection secrets.</param>
internal sealed class LinearEventPoller(HttpClient http, IConnectionSecrets secrets) : IEventPoller
{
    private const string Host = "api.linear.app";

    private const string Fields = "identifier title url createdAt comments(first: 10) { nodes { id createdAt body user { name } } } history(first: 25) { nodes { id createdAt addedLabels { name } fromState { name } toState { name } } }";

    private readonly PollerClient _client = new(http, secrets, Host);

    public string Preset => "linear";

    public async ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!PollCatalog.For(Preset).Contains(kind))
        {
            return SindriErrors.ConnectionProviderChanged.With($"{kind} is not a Linear event");
        }

        var after = PollJson.Time(cursor);
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://{Host}/graphql") { Content = new StringContent(Query(after), Encoding.UTF8, "application/json") };
        var answer = await _client.Send(connection, request, cursor is not null, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return answer.Error;
        }

        return answer.Value.Body is { } body
            ? Read(body, kind, cursor, after)
            : new PollResult([], cursor, RateLimited: true, answer.Value.RetryAfter);
    }

    private static string Query(DateTimeOffset? after)
    {
        var filter = after is { } floor ? $"filter: {{ updatedAt: {{ gt: \"{floor.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)}\" }} }}, " : string.Empty;
        var query = "{ issues(" + filter + "first: 50, orderBy: updatedAt) { nodes { " + Fields + " } } }";
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static Result<PollResult> Read(string body, EventKind kind, string? cursor, DateTimeOffset? after)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (PollJson.Member(document.RootElement, "errors") is not null
                || PollJson.Member(PollJson.Path(document.RootElement, "data", "issues") ?? default, "nodes") is not { ValueKind: JsonValueKind.Array } issues)
            {
                return SindriErrors.ConnectionProviderChanged.With("the issues answer is not the shape Sindri expects");
            }

            var events = new List<PolledEvent>();
            var newest = (Text: cursor, At: after);
            foreach (var issue in issues.EnumerateArray())
            {
                events.AddRange(LinearEventMapper.Map(issue, kind, after));
                newest = Newest(issue, newest);
            }

            return new PollResult([.. events.OrderBy(static e => e.OccurredAt)], newest.Text, RateLimited: false, RetryAfter: null);
        }
        catch (JsonException)
        {
            return SindriErrors.ConnectionProviderChanged.With("the issues answer is not JSON");
        }
    }

    private static (string? Text, DateTimeOffset? At) Newest(JsonElement issue, (string? Text, DateTimeOffset? At) current)
    {
        var best = Later(current, PollJson.Text(PollJson.Member(issue, "createdAt")));
        foreach (var list in new[] { "history", "comments" })
        {
            var nodes = PollJson.Member(PollJson.Member(issue, list) ?? default, "nodes");
            foreach (var node in nodes is { ValueKind: JsonValueKind.Array } items ? items.EnumerateArray() : default)
            {
                best = Later(best, PollJson.Text(PollJson.Member(node, "createdAt")));
            }
        }

        return best;
    }

    private static (string? Text, DateTimeOffset? At) Later((string? Text, DateTimeOffset? At) best, string? text)
        => PollJson.Time(text) is { } at && (best.At is null || at > best.At) ? (text, at) : best;
}
