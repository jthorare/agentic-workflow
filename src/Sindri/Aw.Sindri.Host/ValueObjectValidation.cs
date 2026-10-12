using Aw.Sindri.Core;
using Vogen;

namespace Aw.Sindri.Host;

/// <summary>
/// Maps Vogen's validation exception (an uninitialized id from an empty route or query value) to a 400 ProblemDetails
/// with SND-CLI-003 and fixed text (the method, the path, and that a value isn't valid for the route). The framework's
/// message never reaches the response: it is logged.
/// </summary>
internal static partial class ValueObjectValidation
{
    internal static WebApplication UseValueObjectValidation(this WebApplication app)
    {
        _ = app.Use(async (context, next) =>
        {
            try
            {
                await next(context).ConfigureAwait(false);
            }
            catch (ValueObjectValidationException e) when (!context.Response.HasStarted)
            {
                var path = context.Request.Path.Value ?? string.Empty;
                LogRefused(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(ValueObjectValidation)),
                    context.Request.Method, path, e.Message);
                await ApiProblems.Write(context, SindriErrors.InvalidInput.Code, StatusCodes.Status400BadRequest,
                    $"{context.Request.Method} {path}: a value in the request isn't valid for this route").ConfigureAwait(false);
            }
        });
        return app;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SND-CLI-003 {Method} {Path}: a value in the request isn't valid: {Reason}")]
    private static partial void LogRefused(ILogger logger, string method, string path, string reason);
}
