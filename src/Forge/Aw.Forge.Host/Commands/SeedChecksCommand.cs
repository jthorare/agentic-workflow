using System.Text.Json;
using Aw.Forge.Core;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'seed checks' command: sets the checks on the head.</summary>
internal static class SeedChecksCommand
{
    internal const string Name = "seed checks";

    internal static readonly CommandSpec Spec = new(
        "seed checks <link> --state none|pending|passing|failing [--failing <name,name>]", ["state"], ["failing"], [], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var seed = services.GetRequiredService<IForgeSeed>();
        Result<ChecksState> state = Enum.TryParse<ChecksState>(line["state"], ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : ForgeErrors.Invalid.With("state: none, pending, passing or failing");
        var failing = (line["failing"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = forge.Parse(line.Positionals[0]).Bind(pull => state.Bind(s =>
            seed.SeedChecks(new SeedChecksRequest(pull, s, failing, new Dictionary<string, string>(StringComparer.Ordinal)))))
            .Map(_ => new Applied(Ok: true));
        return Task.FromResult(CommandOutput.Write(result, json));
    }
}
