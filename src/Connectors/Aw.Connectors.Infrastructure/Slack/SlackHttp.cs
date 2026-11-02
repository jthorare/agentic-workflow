namespace Aw.Connectors.Infrastructure.Slack;

/// <summary>Builds the handler the Slack client runs on.</summary>
internal static class SlackHttp
{
    /// <summary>A handler that follows no redirect and uses no proxy (owned index spec §10).</summary>
    /// <returns>The handler.</returns>
    public static HttpMessageHandler CreateHandler() => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false };
}
