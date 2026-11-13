using Aw.Kernel;

namespace Aw.Sindri.Host.Statuses;

/// <summary>
/// The one map from an error's kind to its HTTP status. It is a leaf namespace (it depends on nothing under Host), so the Host root's HttpResults and the method-group
/// endpoints that answer before an upgrade (the terminal socket, the event stream) all reach it without a namespace cycle (ARCH-CYCLE-001).
/// </summary>
internal static class ErrorStatus
{
    internal static int Of(ErrorKind kind) => kind switch
    {
        ErrorKind.Invalid => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.RateLimited or ErrorKind.BudgetExceeded => StatusCodes.Status429TooManyRequests,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Timeout => StatusCodes.Status504GatewayTimeout,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError,
    };
}
