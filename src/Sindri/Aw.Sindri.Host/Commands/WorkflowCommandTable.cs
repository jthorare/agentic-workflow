using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The 'workflow' verb group (engine spec rev 8 §6.3): every verb of §6.3 has its row, each a client that calls one
/// slice. Every verb is a client of the control plane's API: the writes (start, answer, command, cancel, complete) so the
/// command chain classes the caller (API P1), the reads (status, list, events) through the instance routes without starting
/// the control plane (engine spec §6.1: only 'recipe validate' and 'recipe eval' run in process). Unit (b) builds 'start' (<see cref="WorkflowStartCommand"/>) and 'answer', 'command' and 'cancel'
/// (<see cref="ApplyCommandVerbs"/>); unit (c) 'complete' (<see cref="CompleteCommand"/>); unit (e) 'status'
/// (<see cref="StatusCommand"/>), 'list' (<see cref="ListCommand"/>) and 'events' (<see cref="EventsCommand"/>).
/// </summary>
internal static class WorkflowCommandTable
{
    internal const string UsageText =
        "usage: sindri workflow start <recipe> --item <id> [--attempt next] [--fact <key>=<file>]... [--json] | status <address> [--json] | "
        + "list [--needs-me | --item <id> | --state <s>] [--query <q>] [--json] | events <address> [--follow] [--json] | injection <address> [--section <name>] [--json] | "
        + "answer <address> [--text …|--file …] [--grant n | --grant-tokens n | --accept | --reject | --resume | --record <batch.json>] [--nonce <n>] [--json] | "
        + "rewind <address> --to <state> [--nonce <n>] [--json] | command <address> <verb> [--nonce <n>] [--json] | cancel <address> [--nonce <n>] [--json] | takeover <address> [--nonce <n>] [--json] | handback <address> [--nonce <n>] [--json] | complete <address> --run <token> --outcome <o> [--output <fact>=<file>]... [--nonce <n>] [--json]";

    /// <summary>
    /// The top-level short forms of 'workflow' verbs (engine spec §6.3): 'sindri takeover &lt;item-or-address&gt;' and
    /// 'sindri handback &lt;item-or-address&gt;' are 'sindri workflow takeover' and 'sindri workflow handback', and a bare item
    /// resolves to the root of its latest attempt.
    /// </summary>
    internal static Task<int> RunShort(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) => args switch
    {
        ["takeover", .. var rest] => new ApplyCommandVerbs(desk, services, output).Takeover(rest, ct, shortForm: true),
        ["handback", .. var rest] => new ApplyCommandVerbs(desk, services, output).Handback(rest, ct, shortForm: true),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', args)}' is not a sindri command")),
    };

    /// <summary>Runs one 'workflow' verb, each a client of the API through <paramref name="desk"/>.</summary>
    internal static Task<int> Run(string[] args, IServiceProvider services, Output output, RecipeDesk desk, CancellationToken ct) => args switch
    {
        ["start", .. var rest] => WorkflowStartCommand.Run(rest, desk, services, output, ct),
        ["status", .. var rest] => StatusCommand.Run(rest, desk, output, ct),
        ["list", .. var rest] => ListCommand.Run(rest, desk, services, output, ct),
        ["events", .. var rest] => EventsCommand.Run(rest, desk, services, output, ct),
        ["injection", .. var rest] => InjectionCommand.Run(rest, desk, output, ct),
        ["answer", .. var rest] => new ApplyCommandVerbs(desk, services, output).Answer(rest, ct),
        ["command", .. var rest] => new ApplyCommandVerbs(desk, services, output).Command(rest, ct),
        ["rewind", .. var rest] => new ApplyCommandVerbs(desk, services, output).Rewind(rest, ct),
        ["cancel", .. var rest] => new ApplyCommandVerbs(desk, services, output).Cancel(rest, ct),
        ["takeover", .. var rest] => new ApplyCommandVerbs(desk, services, output).Takeover(rest, ct),
        ["handback", .. var rest] => new ApplyCommandVerbs(desk, services, output).Handback(rest, ct),
        ["complete", .. var rest] => CompleteCommand.Run(rest, desk, services, output, ct),
        _ => output.Error(SindriErrors.UnknownVerb.With($"'{string.Join(' ', ["workflow", .. args])}' is not a sindri command ({UsageText})")),
    };
}
