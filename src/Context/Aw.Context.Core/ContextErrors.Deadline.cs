using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Core;

public static partial class ContextErrors
{
    private const string MissedDeadlineSuffix = " missed the query's deadline";

    /// <summary>SND-CONTEXT-010 for a read that the query's deadline cut off; <see cref="IsMissedDeadline"/> tells it from any other 010.</summary>
    /// <param name="source">The source that was being read.</param>
    /// <returns>The error.</returns>
    public static Error MissedDeadline(SourceId source) => SourceUnavailable.With(source.Value + MissedDeadlineSuffix);

    /// <summary>Whether <paramref name="error"/> is a read the deadline cut off (<see cref="MissedDeadline"/>).</summary>
    /// <param name="error">The error to check.</param>
    /// <returns>True for a missed deadline.</returns>
    public static bool IsMissedDeadline(Error error) =>
        error.Code == SourceUnavailable.Code && error.Message.EndsWith(MissedDeadlineSuffix, StringComparison.Ordinal);
}
