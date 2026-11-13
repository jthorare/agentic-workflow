using Aw.Kernel;

namespace Aw.Context.Core;

public static partial class ContextErrors
{
    private const string MissedDeadlineSuffix = " missed the query's deadline";

    /// <summary>SND-CONTEXT-010 for a read that the query's deadline cut off; <see cref="IsMissedDeadline"/> tells it from any other 010.</summary>
    /// <param name="source">The id of the source that was being read (a string, so this root namespace never depends on Sources: ARCH-CYCLE-001, #3361).</param>
    /// <returns>The error.</returns>
    public static Error MissedDeadline(string source) => SourceUnavailable.With(source + MissedDeadlineSuffix);

    /// <summary>Whether <paramref name="error"/> is a read the deadline cut off (<see cref="MissedDeadline"/>).</summary>
    /// <param name="error">The error to check.</param>
    /// <returns>True for a missed deadline.</returns>
    public static bool IsMissedDeadline(Error error) =>
        error.Code == SourceUnavailable.Code && error.Message.EndsWith(MissedDeadlineSuffix, StringComparison.Ordinal);
}
