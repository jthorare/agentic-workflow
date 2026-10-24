using Aw.Judge.Core.Providers;
using Aw.Judge.Features.CheckTraits;
using Aw.Kernel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Judge.Host.Commands;

/// <summary>
/// 'judge trait-check --edit --file &lt;p&gt;… | --stop [--base ref] | --scan [--path p…] [--trait n…]' (trait-check.sh calls
/// the first two). Exit 0 nothing to report or skipped, 2 findings, 3 Jev unavailable, 64 usage. Unit 6.2 builds the
/// argument binding, the output and the exit mapping.
/// </summary>
internal static class TraitCheckCommand
{
    internal const string Name = "trait-check";
    internal const string Synopsis = "trait-check --edit --file <path>… | --stop [--base <ref>] | --scan [--path <path>…] [--trait <name>…]";

    internal static async Task<int> Run(IReadOnlyList<string> args, IReadOnlyCollection<ProviderName>? allowed, IServiceProvider services)
    {
        var mode = args.Select(Mode).FirstOrDefault(m => m is not null);
        if (mode is null)
        {
            return CommandOutput.Usage(Synopsis);
        }

        var service = services.GetRequiredService<IService<CheckTraitsRequest, CheckTraitsResponse>>();
        var request = new CheckTraitsRequest(mode.Value, Values(args, "--file"), Value(args, "--base"), Values(args, "--path"), Values(args, "--trait"), Providers: allowed?.ToArray());
        var result = await service.Handle(request, CancellationToken.None).ConfigureAwait(false);
        return result.IsError ? CommandOutput.Emit(1, "", result.Error.Message) : CommandOutput.Emit(0, "");
    }

    private static TraitCheckMode? Mode(string arg) => arg switch
    {
        "--edit" => TraitCheckMode.Edit,
        "--stop" => TraitCheckMode.Stop,
        "--scan" => TraitCheckMode.Scan,
        _ => null,
    };

    private static string[] Values(IReadOnlyList<string> args, string flag) =>
        [.. args.Select((arg, at) => (arg, at)).Where(x => string.Equals(x.arg, flag, StringComparison.Ordinal) && x.at + 1 < args.Count).Select(x => args[x.at + 1])];

    private static string? Value(IReadOnlyList<string> args, string flag) => Values(args, flag).FirstOrDefault();
}
