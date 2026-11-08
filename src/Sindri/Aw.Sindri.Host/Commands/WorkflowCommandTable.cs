using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group after the recipe engine: 'complete' stays for a launched session's report (rewritten with the
/// graph engine's agent block), 'takeover' and 'handback' refuse with SND-GRAPH-104 until that block lands.
/// </summary>
internal static class WorkflowCommandTable
{
    internal const string UsageText =
        "usage: sindri workflow complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json] | takeover <address> | handback <address>";

    /// <summary>The top-level short forms: 'sindri takeover' and 'sindri handback'.</summary>
    internal static Task<int> RunShort(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) => Run(args, services, output, desk, ct);

    /// <summary>Runs one 'workflow' verb.</summary>
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) => args switch
    {
        ["complete", .. var rest] => CompleteCommand.Run(rest, desk, services, output, ct),
        ["takeover" or "handback", ..] => output.Error(SindriErrors.GraphRunControlsPending.With($"'{args[0]}' returns with the graph engine's agent blocks")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({UsageText})")),
    };
}
