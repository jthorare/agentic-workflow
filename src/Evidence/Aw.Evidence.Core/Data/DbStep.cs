namespace Aw.Evidence.Core.Data;

/// <summary>One executed query.</summary>
/// <param name="Index">The query's 1-based position.</param>
/// <param name="Action">Always 'query'.</param>
/// <param name="Target">The SQL on one line, scrubbed and capped.</param>
/// <param name="Status">How it ended.</param>
/// <param name="RowCount">The rows kept; null when it never produced any.</param>
/// <param name="Truncated">True when the row limit cut the result.</param>
/// <param name="Output">The rows as JSON, secret-named columns hidden, scrubbed and capped; null when none.</param>
public sealed record DbStep(int Index, string Action, string Target, DbStepStatus Status, int? RowCount, bool Truncated, string? Output);
