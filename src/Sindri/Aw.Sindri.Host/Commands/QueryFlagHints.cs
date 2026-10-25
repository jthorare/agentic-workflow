using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Sindri.Host.Commands;

/// <summary>Adds the CLI's own flag to the transport-neutral 006 a depth or budget gets from the engine (the engine's message names no flag, so HTTP and MCP callers aren't told to use one).</summary>
internal static class QueryFlagHints
{
    internal static Error Add(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error.Code != ContextErrors.QueryInvalid.Code)
        {
            return error;
        }

        return error.Message switch
        {
            var m when m.StartsWith(QueryRules.DepthMessage, StringComparison.Ordinal) => error with { Message = m + " (fix: --depth 2)" },
            var m when m == QueryRules.ShapeMessage => error with { Message = m + " (CLI: text, --node, --item or --symbol)" },
            var m when m == QueryRules.NodeMessage => error with { Message = m + " (CLI: --node)" },
            _ => error,
        };
    }
}
