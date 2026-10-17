using System.Text.Json;
using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Forge.Host.Commands;

/// <summary>The 'seed thread' command: puts a conversation item where the babysitter will find it (file forge only).</summary>
internal static class SeedThreadCommand
{
    internal const string Name = "seed thread";

    internal static readonly CommandSpec Spec = new(
        "seed thread <link> --author <login> --body <text> [--kind thread|review|comment] [--association <text>] [--path <file>] [--bot]",
        ["author", "body"], ["kind", "association", "path"], ["bot"], Positionals: 1);

    internal static Task<int> Run(CommandLine line, IServiceProvider services, JsonSerializerOptions json)
    {
        var forge = Binding.Forge(services);
        var seed = services.GetRequiredService<IForgeSeed>();
        var result = forge.Parse(line.Positionals[0]).Bind(pull => Binding.Kind(line["kind"]).Bind(kind =>
            seed.SeedThread(new SeedThreadRequest(pull, kind, line.Need("author"), line["association"] ?? "NONE", line.Flags.Contains("bot"), line["path"], line.Need("body")))))
            .Map(key => new SeedResult(key));
        return Task.FromResult(CommandOutput.Write(result, json));
    }
}
