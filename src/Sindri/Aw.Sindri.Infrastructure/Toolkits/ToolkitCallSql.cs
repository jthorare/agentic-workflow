namespace Aw.Sindri.Infrastructure.Toolkits;

/// <summary>The toolkit call SQL (ledger.db, migration 0018_toolkits).</summary>
internal static class ToolkitCallSql
{
    internal const string Columns = "toolkit, tool, caller, input, output, truncated, error, duration_ms, at";

    internal const string Insert =
        "INSERT INTO toolkit_call (toolkit, tool, caller, input, output, truncated, error, duration_ms, at) " +
        "VALUES ($toolkit, $tool, $caller, $input, $output, $truncated, $error, $durationMs, $at)";

    internal const string Select =
        "SELECT " + Columns + " FROM toolkit_call WHERE ($toolkit IS NULL OR toolkit = $toolkit) ORDER BY at DESC, id DESC LIMIT $limit";

    internal const string DeleteBefore = "DELETE FROM toolkit_call WHERE at < $before";
}
