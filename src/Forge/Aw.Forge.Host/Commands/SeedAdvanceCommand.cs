using System.Text.Json;
using Aw.Forge.Core.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'seed advance' command: moves the head as an out-of-band push would.</summary>
internal static class SeedAdvanceCommand
{
    internal const string Name = "seed advance";

    internal static readonly CommandSpec Spec = new("seed advance <link> --sha <sha>", ["sha"], [], [], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var seed = services.GetRequiredService<IForgeSeed>();
        var result = forge.Parse(line.Positionals[0]).Bind(pull => seed.AdvanceHead(pull, line.Need("sha"))).Map(_ => new Applied(Ok: true));
        return Task.FromResult(CommandOutput.Write(result, json));
    }
}
