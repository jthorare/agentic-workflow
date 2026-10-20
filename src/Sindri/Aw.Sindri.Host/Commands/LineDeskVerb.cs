using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Features.Design;
using Aw.Sindri.Features.Plan;
using Aw.Sindri.Host.Commands.Line;
using Aw.Sindri.Host.Commands.Scoping;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'sindri line desk &lt;block&gt; --facts &lt;dir&gt; --out &lt;dir&gt;': a line block Step's code run at the desk, as 'scoping desk' runs a scoping one (line spec Appendix D). It writes the
/// Step's fact files, its run-area files and report.json under the out directory, and for an evidence Step the packet's hash from aw-packets.
/// </summary>
internal static class LineDeskVerb
{
    internal static async Task<int> Run(IReadOnlyList<string> args, ScopingCommandTable.Host host, string usage, CancellationToken ct)
    {
        var (services, output, _, _) = host;
        var parsed = ScopingArguments.Parse(args, "--facts", "--out", "--packet-id", "--repo-slug", "--base-sha", "--scope-hash", "--design-hash", "--parallel", "--max-stack", "--default-branch", "--scope", "--design", "--repo", "--style", "--lenses");
        if (parsed is not { Positional: [var block] } || parsed.Value("--facts") is not { } directory || parsed.Value("--out") is not { } outDirectory)
        {
            return await LineCommandTable.Fail(output, SindriErrors.InvalidInput.With(usage)).ConfigureAwait(false);
        }

        var report = await Execute(block, (directory, outDirectory, parsed), services, ct).ConfigureAwait(false);
        if (report.IsError)
        {
            return await LineCommandTable.Fail(output, report.Error).ConfigureAwait(false);
        }

        var code = report.Value.Findings.Count > 0 || string.Equals(report.Value.Outcome, "incomplete", StringComparison.Ordinal) ? 1 : 0;
        return parsed.Json ? await output.Json(report.Value, code).ConfigureAwait(false) : await output.Text($"{report.Value.Block}: {report.Value.Outcome}\n", code).ConfigureAwait(false);
    }

    private static async Task<Result<LineDeskReport>> Execute(string block, (string Facts, string Out, ScopingArguments Flags) input, IServiceProvider services, CancellationToken ct)
    {
        var (directory, outDirectory, flags) = input;
        if (ScopingFactDirectory.InGitWorktree(outDirectory))
        {
            return ScopingFactDirectory.WorktreeRefusal("--out", outDirectory);
        }

        var facts = await LineFactDirectory.Read(directory, flags, ct).ConfigureAwait(false);
        if (facts.IsError)
        {
            return facts.Error;
        }

        var context = Context(facts.Value, flags);
        if (context.IsError)
        {
            return context.Error;
        }

        var reply = await Dispatch(block, services, facts.Value, context.Value, ct).ConfigureAwait(false);
        return reply.IsError ? reply.Error : await Write(reply.Value, outDirectory, (services.GetRequiredService<IPackets>(), services.GetRequiredService<JsonSerializerOptions>()), ct).ConfigureAwait(false);
    }

    private static async Task<Result<LineDeskResponse>> Dispatch(string block, IServiceProvider services, Dictionary<string, string> facts, LineDeskContext context, CancellationToken ct) => block switch
    {
        _ when block.StartsWith("design.", StringComparison.Ordinal) || block.StartsWith("step.design.", StringComparison.Ordinal) =>
            await services.GetRequiredService<IService<DesignDeskRequest, LineDeskResponse>>().Handle(new DesignDeskRequest(block, facts, context), ct).ConfigureAwait(false),
        _ when block.StartsWith("plan.", StringComparison.Ordinal) || block.StartsWith("step.plan.", StringComparison.Ordinal) =>
            await services.GetRequiredService<IService<PlanDeskRequest, LineDeskResponse>>().Handle(new PlanDeskRequest(block, facts, context), ct).ConfigureAwait(false),
        _ => SindriErrors.InvalidInput.With($"'{block}' is not a line block Step (fix: design.check, design.evidence, plan.check or plan.evidence)"),
    };

    // The identity from the flags, else the candidate's own packet.json; the hashes of the packets it follows from the flags, else the facts the earlier stations wrote.
    private static Result<LineDeskContext> Context(Dictionary<string, string> facts, ScopingArguments flags)
    {
        var known = new LineFacts(facts);
        if (!Count(flags.Value("--parallel"), 2, out var parallel) || !Count(flags.Value("--max-stack"), 4, out var maxStack))
        {
            return SindriErrors.InvalidInput.With("--parallel and --max-stack take a whole number of 1 or more");
        }

        return new LineDeskContext(
            Identity(known, flags),
            flags.Value("--scope-hash") ?? LineJson.Str(known.Json("scopePacket.json"), "hash"),
            flags.Value("--design-hash") ?? LineJson.Str(known.Json("designPacket.json"), "hash"),
            parallel,
            maxStack,
            flags.Value("--default-branch") ?? "main");
    }

    private static PacketIdentity? Identity(LineFacts known, ScopingArguments flags)
    {
        var packet = known.Json("packet.json");
        var id = flags.Value("--packet-id") ?? LineJson.Str(packet, "id");
        var repo = flags.Value("--repo-slug") ?? LineJson.Str(packet, "repo");
        var sha = flags.Value("--base-sha") ?? LineJson.Str(packet, "baseSha");
        return id is not null && repo is not null && sha is not null ? new PacketIdentity(id, repo, sha) : null;
    }

    private static bool Count(string? text, int fallback, out int value)
    {
        value = fallback;
        return text is null || (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 1);
    }

    /// <summary>Writes the Step's fact files, its run-area files and report.json; an evidence Step's packet gets its hash from aw-packets.</summary>
    private static async Task<Result<LineDeskReport>> Write(LineDeskResponse response, string outDirectory, (IPackets Packets, JsonSerializerOptions Json) context, CancellationToken ct)
    {
        var outputs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, text) in response.Outputs)
        {
            await ScopingFactDirectory.Write(Path.Combine(outDirectory, key + ".json"), text + "\n", append: false, ct).ConfigureAwait(false);
            outputs["fact:" + key] = key + ".json";
        }

        foreach (var file in response.Files)
        {
            await ScopingFactDirectory.Write(Path.Combine(outDirectory, file.Path), file.Content, append: false, ct).ConfigureAwait(false);
        }

        if (response.Files.Count > 0)
        {
            var packet = await Hashed(response, outDirectory, context.Packets, outputs, ct).ConfigureAwait(false);
            if (packet.IsError)
            {
                return packet.Error;
            }
        }

        var report = new LineDeskReport(response.Block, response.Outcome, outputs, [.. response.Files.Select(static f => f.Path)], response.Findings, response.Warnings);
        await ScopingFactDirectory.Write(Path.Combine(outDirectory, "report.json"), Encoding.UTF8.GetString(AwJson.Serialize(report, context.Json)) + "\n", append: false, ct).ConfigureAwait(false);
        return report;
    }

    private static async Task<Result<string>> Hashed(LineDeskResponse response, string outDirectory, IPackets packets, Dictionary<string, string> outputs, CancellationToken ct)
    {
        var directory = response.Files[0].Path[..response.Files[0].Path.IndexOf('/', StringComparison.Ordinal)];
        var kind = string.Equals(directory, "design-packet", StringComparison.Ordinal) ? PacketKind.Design : PacketKind.Plan;
        var hash = await packets.Hash(new PacketRef(kind, Path.Combine(outDirectory, directory)), ct).ConfigureAwait(false);
        if (hash.IsError)
        {
            return SindriErrors.InvalidInput.With(hash.Error.Message);
        }

        var fact = kind == PacketKind.Design ? "designPacket" : "planPacket";
        await ScopingFactDirectory.Write(Path.Combine(outDirectory, fact + ".json"), new JsonObject { ["hash"] = hash.Value }.ToJsonString() + "\n", append: false, ct).ConfigureAwait(false);
        outputs["fact:" + fact] = fact + ".json";
        return hash.Value;
    }
}
