namespace Aw.Sindri.Host;

/// <summary>Refuses a non-loopback Host header (400, DNS rebinding) and a non-loopback browser Origin (403), spec §4d.</summary>
internal static class LoopbackGuard
{
    internal static WebApplication UseLoopbackGuard(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            if (!IsLoopback(context.Request.Host.Host))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var origin = context.Request.Headers.Origin.ToString();
            if (origin.Length > 0 && !(Uri.TryCreate(origin, UriKind.Absolute, out var uri) && IsLoopback(uri.Host)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        return app;
    }

    private static bool IsLoopback(string host)
        => string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(host, "127.0.0.1", StringComparison.Ordinal)
            || string.Equals(host, "[::1]", StringComparison.Ordinal)
            || string.Equals(host, "::1", StringComparison.Ordinal);
}
