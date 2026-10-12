using Aw.Sindri.Core;

namespace Aw.Sindri.Host;

/// <summary>
/// Gives the API's own failures a ProblemDetails body with a catalogue code, so the CLI never reads one as 'not running':
/// an unhandled exception (a bug in a handler) is logged and answered 500 with SND-API-005, and a request the request
/// delegate refused before any handler ran (a body that isn't JSON of the request's shape, answered 400 with no body) is
/// answered with SND-CLI-003. It runs after the loopback guard, whose refusals stay bodiless.
/// </summary>
internal static partial class ApiProblems
{
    internal static WebApplication UseApiProblems(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (Exception e) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)   // boundary: a handler's bug becomes a 500 with a code, not a dropped connection
            {
                LogUnhandled(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ApiProblems)), e,
                    context.Request.Method, context.Request.Path.Value ?? string.Empty);
                await Write(context, SindriErrors.ApiInternal.Code, StatusCodes.Status500InternalServerError,
                    $"{context.Request.Method} {context.Request.Path} failed with {e.GetType().Name}; the control plane logged it").ConfigureAwait(false);
                return;
            }

            if (context.Response is { HasStarted: false, StatusCode: StatusCodes.Status400BadRequest, ContentType: null })
            {
                await Write(context, SindriErrors.InvalidInput.Code, StatusCodes.Status400BadRequest,
                    $"{context.Request.Method} {context.Request.Path}: the request's body or query values aren't valid for this route").ConfigureAwait(false);
            }
        });
        return app;
    }

    /// <summary>Replaces whatever the response holds with a ProblemDetails answer: <paramref name="code"/> as its title.</summary>
    internal static Task Write(HttpContext context, string code, int status, string detail)
    {
        context.Response.Clear();
        return TypedResults.Problem(title: code, detail: detail, statusCode: status).ExecuteAsync(context);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SND-API-005 {Method} {Path} failed with an unhandled exception")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
