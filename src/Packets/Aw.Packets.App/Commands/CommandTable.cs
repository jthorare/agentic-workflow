using System.Text.Json;
using Aw.Packets.Core.Kinds;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Packets.App.Commands;

/// <summary>Command name → handler. Each handler binds its arguments, calls one service and maps the result to an exit code.</summary>
internal static class CommandTable
{
    internal static readonly string Usage =
        $"validate|freeze|hash|verify <packet-dir> [--kind {string.Join('|', PacketKinds.All.Select(static k => k.Name))}] [--json] "
        + "(build and the line kinds design, plan, phase, proof, review, handoff: validate and freeze take [--repo <dir>]; validate takes [--sizes <file>] (build and plan) and [--seal <file>] (phase), freeze takes [--by owner|machine]; verify takes --hash <sha256:hex> [--candidate]; scoping: freeze takes --repo-slug <owner/name> --instance <slug> [--by owner|machine])";

    internal static async Task<int> Run(string[] args, IServiceProvider provider)
    {
        if (args.Length == 0)
        {
            return CommandOutput.Usage(Usage);
        }

        if (KindArgument.Extract(args[1..]) is not { } kindArgs)
        {
            return CommandOutput.Usage($"--kind takes one of: {string.Join(", ", PacketKinds.All.Select(static k => k.Name))}");
        }

        var json = provider.GetRequiredService<JsonSerializerOptions>();   // the one Kernel JSON profile Composition registered
        var scope = provider.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var (kind, rest) = kindArgs;
            var services = scope.ServiceProvider;
            var scoping = kind == PacketKinds.Scoping;
            var line = kind.IsLine;
            return args[0] switch
            {
                ValidatePacketCommand.Name when line => await ValidateLinePacketCommand.Run(kind, rest, services, json).ConfigureAwait(false),
                ValidatePacketCommand.Name when scoping => await ValidateScopingPacketCommand.Run(rest, services, json).ConfigureAwait(false),
                ValidatePacketCommand.Name => await ValidatePacketCommand.Run(rest, services, json).ConfigureAwait(false),
                FreezePacketCommand.Name when line => await FreezeLinePacketCommand.Run(kind, rest, services, json).ConfigureAwait(false),
                FreezePacketCommand.Name when scoping => await FreezeScopingPacketCommand.Run(rest, services, json).ConfigureAwait(false),
                FreezePacketCommand.Name => await FreezePacketCommand.Run(rest, services, json).ConfigureAwait(false),
                HashPacketCommand.Name => await HashPacketCommand.Run(kind, rest, services, json).ConfigureAwait(false),
                VerifyPacketCommand.Name => await VerifyPacketCommand.Run(kind, rest, services, json).ConfigureAwait(false),
                _ => CommandOutput.Usage($"unknown command '{args[0]}'"),
            };
        }
    }
}
