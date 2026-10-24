using System.Collections.Frozen;
using System.Text.Json;
using Aw.Judge.Core.Providers;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// Command name → handler. The table first takes the '--providers a,b' flag out of argv (ProvidersFlag), then checks each
/// command's argument count (a wrong count is a usage error, 64); a question also reads the allowlist (the flag, else
/// AW_JUDGE_PROVIDERS), and a list it can't read is a usage error there;
/// each handler binds its arguments, calls one service and maps the result to an exit code (docs/csharp/judge.md).
/// </summary>
internal static class CommandTable
{
    internal const string Synopsis = "<command> [args] | judge <question> < input.json";

    // The first words the switch routes; any other is a question.
    private static readonly FrozenSet<string> _routed = FrozenSet.Create(StringComparer.Ordinal, GetHealthCommand.Name, BriefCommands.Name, ExplainDecisionCommand.Name, UndoDecisionCommand.Name, ApproveDecisionCommand.Name, ConfigCommands.Name,
        PromptSortCommands.Name, TraitCheckCommand.Name, LabelCommand.Name, AdjudicateCommand.Name, EvalCommand.Name);

    // The commands use the one AwJson options instance Compose registers, the same the adapters get.
    internal static async Task<int> Run(string[] argv, IServiceProvider provider)
    {
        var json = provider.GetRequiredService<JsonSerializerOptions>();
        var (args, flag) = ProvidersFlag.Take(argv);
        if (args is null)
        {
            return CommandOutput.Usage(Synopsis, "--providers needs a comma-separated list");
        }

        if (args.Length == 0)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var rest = args[1..];
            return _routed.Contains(args[0])
                ? await Route(args[0], rest, flag, scope.ServiceProvider, json).ConfigureAwait(false)
                : await Question(args[0], rest, flag ?? scope.ServiceProvider.GetRequiredService<ProvidersVariable>().Value, scope.ServiceProvider, json).ConfigureAwait(false);
        }
    }

    // The routed first words. A stubbed word (a verb that isn't in this build yet) needs its arm before the decision-id
    // commands', which count their arguments: behind them it would be a usage error. A word missing from _routed is a question.
    private static async Task<int> Route(string command, string[] rest, string? flag, IServiceProvider services, JsonSerializerOptions json) => command switch
    {
        GetHealthCommand.Name when rest.Length == 0 => await GetHealthCommand.Run(services, json).ConfigureAwait(false),
        GetHealthCommand.Name => CommandOutput.Usage("health"),
        BriefCommands.Name => await BriefCommands.Run(rest, services, json).ConfigureAwait(false),
        ConfigCommands.Name => await ConfigCommands.Run(rest, services, json).ConfigureAwait(false),
        LabelCommand.Name => LabelCommand.Run(),
        AdjudicateCommand.Name => AdjudicateCommand.Run(),
        EvalCommand.Name => EvalCommand.Run(rest),
        PromptSortCommands.Name or TraitCheckCommand.Name => await Sorter(command, rest, flag, services, json).ConfigureAwait(false),
        _ => await DecisionCommand(command, rest, services, json).ConfigureAwait(false),
    };

    private static async Task<int> DecisionCommand(string command, string[] rest, IServiceProvider services, JsonSerializerOptions json) => command switch
    {
        _ when rest.Length != 1 => CommandOutput.Usage($"{command} <decision-id>"),
        ExplainDecisionCommand.Name => await ExplainDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
        UndoDecisionCommand.Name => await UndoDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
        _ => await ApproveDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
    };

    // 'prompt-sort' and 'trait-check' read the allowlist like a question does (the flag, else AW_JUDGE_PROVIDERS): they
    // ask Jev, which the list can leave out.
    private static async Task<int> Sorter(string command, string[] rest, string? flag, IServiceProvider services, JsonSerializerOptions json)
    {
        var allowlist = Allowlist(flag ?? services.GetRequiredService<ProvidersVariable>().Value);
        if (allowlist.IsError)
        {
            return CommandOutput.Usage(Synopsis, allowlist.Error.Message);
        }

        return string.Equals(command, PromptSortCommands.Name, StringComparison.Ordinal)
            ? await PromptSortCommands.Run(rest, allowlist.Value, services, json).ConfigureAwait(false)
            : await TraitCheckCommand.Run(rest, allowlist.Value, services).ConfigureAwait(false);
    }

    // A question reads the allowlist (the flag, else AW_JUDGE_PROVIDERS); a list it can't read is a usage error.
    private static async Task<int> Question(string question, string[] rest, string? list, IServiceProvider services, JsonSerializerOptions json)
    {
        var allowlist = Allowlist(list);
        return allowlist.IsError
            ? CommandOutput.Usage(Synopsis, allowlist.Error.Message)
            : await QuestionCommand.Run(question, rest, allowlist.Value, services, json).ConfigureAwait(false);
    }

    private static Result<IReadOnlyList<ProviderName>?> Allowlist(string? list)
    {
        if (list is null)
        {
            return Results.Success<IReadOnlyList<ProviderName>?>(value: null);
        }

        var parsed = ProviderNames.ParseList(list);
        return parsed.IsError
            ? Results.Failure<IReadOnlyList<ProviderName>?>(parsed.Error)
            : Results.Success<IReadOnlyList<ProviderName>?>(parsed.Value);
    }
}
