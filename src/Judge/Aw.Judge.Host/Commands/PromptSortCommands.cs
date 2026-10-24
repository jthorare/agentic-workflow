using System.Text.Json;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge prompt-sort …': the one route, decided by the second word. None goes to the hook path (<see cref="SortPromptCommand"/>,
/// unit 6.1), 'scaffold' to the scaffold switch (<see cref="ScaffoldCommand"/>, unit 6.1), a stubbed subcommand to
/// <see cref="PromptSortNotBuiltCommand"/>; anything else is 'judge: unknown prompt-sort subcommand: &lt;sub&gt;' on stderr, exit 1.
/// </summary>
internal static class PromptSortCommands
{
    internal const string Name = "prompt-sort";

    internal static async Task<int> Run(IReadOnlyList<string> args, IReadOnlyCollection<ProviderName>? allowed, IServiceProvider services, JsonSerializerOptions json)
    {
        if (args.Count == 0)
        {
            return await SortPromptCommand.Run(allowed, services, json).ConfigureAwait(false);
        }

        var rest = args.Skip(1).ToArray();
        if (string.Equals(args[0], ScaffoldCommand.Name, StringComparison.Ordinal))
        {
            return await ScaffoldCommand.Run(rest, services, json).ConfigureAwait(false);
        }

        return PromptSortNotBuiltCommand.Subcommands.Contains(args[0])
            ? PromptSortNotBuiltCommand.Run(args[0])
            : CommandOutput.Emit(1, "", $"judge: unknown prompt-sort subcommand: {args[0]}");
    }
}
