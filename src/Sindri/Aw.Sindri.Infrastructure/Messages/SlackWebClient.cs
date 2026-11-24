using System.Net.Http.Headers;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Infrastructure.Messages;

/// <summary>
/// The Slack Web API adapter of Messages (messages spec §5.3, §8.1, §11). One <see cref="HttpClient"/> over the handler it is given (production: no
/// redirects; tests: a stub), so the fake and the real server use the same class. The token goes only to https slack.com hosts, or to the loopback base
/// <see cref="SlackBaseUrlRule"/> accepts for a fixture token. Every call draws from the one <see cref="SlackRateBudget"/>: a held method answers
/// SND-MSG-005 without a request, and a 429 holds only its own method. The token is never logged and never in an error.
/// </summary>
internal sealed partial class SlackWebClient(HttpMessageHandler handler, SlackWebSettings settings, SlackRateBudget budget, TimeProvider clock) : ISlackClient, IDisposable
{
    private const string AuthTestMethod = "auth.test";

    private readonly HttpClient _http = new(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(30) };
    private readonly SlackWebSettings _settings = settings;
    private readonly SlackRateBudget _budget = budget;
    private readonly TimeProvider _clock = clock;

    public async ValueTask<Result<SlackAuth>> AuthTest(string token, CancellationToken cancellationToken)
    {
        var reply = await Call(token, AuthTestMethod, [], cancellationToken).ConfigureAwait(false);
        return reply.IsError ? reply.Error : ReadAuth(reply.Value);
    }

    public void Dispose() => _http.Dispose();

    /// <summary>One Web API call: POST of a form to <paramref name="method"/>, the reply's 'ok' checked and mapped (SlackWebClient.Replies.cs).</summary>
    /// <param name="token">The token, sent as a Bearer header.</param>
    /// <param name="method">The Slack method ('conversations.history').</param>
    /// <param name="form">The method's arguments.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The reply's root object, or the mapped error.</returns>
    internal async ValueTask<Result<JsonElement>> Call(string token, string method, IReadOnlyList<KeyValuePair<string, string>> form, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentException.ThrowIfNullOrEmpty(method);
        var wait = _budget.Wait(method, _clock.GetUtcNow());
        if (wait > TimeSpan.Zero)
        {
            return RateLimited(method, wait);
        }

        if (Url(token, method) is not { } url)
        {
            return SindriErrors.ConnectionProviderChanged.With("the Slack call would leave slack.com; nothing was sent");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("agentic-workflow-sindri");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return await Interpret(method, response, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            return SindriErrors.MessagesSlackUnreachable.With($"Slack could not be reached ({e.HttpRequestError}); nothing was read.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SindriErrors.MessagesSlackUnreachable.With("Slack did not answer in time; nothing was read.");
        }
    }

    // The API base for this token (the fixture's loopback base only under SlackBaseUrlRule), and the method's url; null when Slack's own base would
    // somehow leave slack.com, so the token is never sent there.
    private Uri? Url(string token, string method)
    {
        var api = SlackBaseUrlRule.Resolve(_settings.BaseUrlOverride, _settings.Marker(), token);
        var url = new Uri(api, method);
        return ReferenceEquals(api, SlackBaseUrlRule.Slack) && !SlackHostRule.IsSlack(url) ? null : url;
    }

    private static Result<SlackAuth> ReadAuth(JsonElement root) =>
        Text(root, "team_id") is { } workspace && Text(root, "user_id") is { } user
            ? new SlackAuth(workspace, user, Text(root, "enterprise_id"))
            : SindriErrors.ConnectionProviderChanged.With("Slack's auth.test answer has no team_id or user_id");

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;
}
