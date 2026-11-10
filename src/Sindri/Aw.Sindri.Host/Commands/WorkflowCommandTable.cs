using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group after the recipe engine: 'complete' stays for a launched session's report (rewritten with the
/// graph engine's agent block), 'takeover' and 'handback' of a 'run_agent' node (&lt;run&gt; &lt;node&gt;) go to the 'wf' verbs' client; of an instance they still refuse with SND-GRAPH-104.
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
        ["takeover" or "handback"] => output.Error(SindriErrors.InvalidInput.With($"usage: sindri {args[0]} <item-or-address> [--nonce <n>] [--json]")),
        ["takeover", var run, var node, .. var rest] => WfAgentNodeCommand.TakeOver([run, node, .. rest], desk, output, ct),   // the short form of 'wf takeover <run> <node>'
        ["handback", var run, var node, .. var rest] => WfAgentNodeCommand.HandBack([run, node, .. rest], desk, output, ct),
        ["takeover" or "handback", ..] => output.Error(SindriErrors.GraphRunControlsPending.With($"'{args[0]}' of an instance returns with the graph engine; a run_agent node uses 'sindri wf {args[0]} <run> <node> [--item <index>]'")),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({UsageText})")),
    };
}
