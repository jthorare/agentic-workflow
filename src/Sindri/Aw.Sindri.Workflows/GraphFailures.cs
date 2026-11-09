using Aw.Kernel;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Turns a failed store call into the activity failure Temporal expects: a missing run or revision never appears by retrying, anything else may.</summary>
internal static class GraphFailures
{
    /// <summary>The value of <paramref name="result"/>.</summary>
    /// <param name="result">The store's answer.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>The value.</returns>
    public static T Value<T>(Result<T> result)
    {
        if (!result.IsError)
        {
            return result.Value;
        }

        throw new ApplicationFailureException(result.Error.Message, errorType: result.Error.Code, nonRetryable: result.Error.Kind == ErrorKind.NotFound);
    }
}
