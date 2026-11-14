using Aw.Kernel;
using Aw.Sindri.Host.Statuses;

namespace Aw.Sindri.Host;

/// <summary>Maps a returned Result to TypedResults (a value) or ProblemDetails (an error, by its kind).</summary>
internal static class HttpResults
{
    internal static IResult ToHttpResult<T>(this Result<T> result)
        => result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : Problem(result.Error);

    /// <summary>An error as a ProblemDetails answer: title the code, detail the message, the status its kind's; extension members (RFC 9457 §3.2) follow when it has any.</summary>
    internal static IResult Problem(Error error) => Problem(error, StatusOf(error.Kind));

    /// <summary>An error as a ProblemDetails answer with a status the caller chose (a middleware refusal).</summary>
    internal static IResult Problem(Error error, int status)
        => error.Extensions is { Count: > 0 }
            ? new ProblemWithExtensions(error, status)
            : TypedResults.Problem(title: error.Code, detail: error.Message, statusCode: status);

    internal static int StatusOf(ErrorKind kind) => ErrorStatus.Of(kind);
}
