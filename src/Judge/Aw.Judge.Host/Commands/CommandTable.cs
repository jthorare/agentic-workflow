using System.Collections.Frozen;
using System.Text.Json;
using Aw.Judge.Core.Providers;
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
    private static readonly FrozenSet<string> _routed = FrozenSet.Create(StringComparer.Ordinal, GetHealthCommand.Name, BriefCommands.Name, ExplainDecisionCommand.Name, UndoDecisionCommand.Name, ApproveDecisionCommand.Name, ConfigCommands.Name);

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
            var services = scope.ServiceProvider;
            return args[0] switch
            {
                _ when !_routed.Contains(args[0]) => await Question(args[0], rest, flag ?? services.GetRequiredService<ProvidersVariable>().Value, services, json).ConfigureAwait(false),
                GetHealthCommand.Name when rest.Length == 0 => await GetHealthCommand.Run(services, json).ConfigureAwait(false),
                GetHealthCommand.Name => CommandOutput.Usage("health"),
                BriefCommands.Name => await BriefCommands.Run(rest, services, json).ConfigureAwait(false),
                ConfigCommands.Name => await ConfigCommands.Run(rest, services, json).ConfigureAwait(false),
                _ when rest.Length != 1 => CommandOutput.Usage($"{args[0]} <decision-id>"),
                ExplainDecisionCommand.Name => await ExplainDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
                UndoDecisionCommand.Name => await UndoDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
                _ => await ApproveDecisionCommand.Run(rest[0], services, json).ConfigureAwait(false),
            };
        }
    }

    // A question reads the allowlist (the flag, else AW_JUDGE_PROVIDERS); a list it can't read is a usage error.
    private static async Task<int> Question(string question, string[] rest, string? list, IServiceProvider services, JsonSerializerOptions json)
    {
        IReadOnlyList<ProviderName>? allowed = null;
        if (list is not null)
        {
            var parsed = ProviderNames.ParseList(list);
            if (parsed.IsError)
            {
                return CommandOutput.Usage(Synopsis, parsed.Error.Message);
            }

            allowed = parsed.Value;
        }

        return await QuestionCommand.Run(question, rest, allowed, services, json).ConfigureAwait(false);
    }
}
