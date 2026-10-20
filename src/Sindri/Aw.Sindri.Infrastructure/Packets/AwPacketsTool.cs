using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Packets;

/// <summary>
/// <see cref="IPackets"/> over the aw-packets program (AW_PACKETS_BIN, else 'aw-packets' on the PATH), run through <see cref="IToolRunner"/> with --json: the one place Sindri
/// invokes it. Exit 0 and 1 carry a JSON answer (1 is a failed check); anything else, a missing program or a timeout is an error.
/// </summary>
/// <param name="runner">Runs aw-packets.</param>
/// <param name="environment">Reads AW_PACKETS_BIN.</param>
internal sealed class AwPacketsTool(IToolRunner runner, Func<string, string?> environment) : IPackets
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    public ValueTask<Result<PacketValidation>> Validate(PacketRef packet, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Answer("validate", packet, by: null, cancellationToken).Map(static json => new PacketValidation(Flag(json, "passed"), Failures(json))));

    public ValueTask<Result<string>> Hash(PacketRef packet, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Run("hash", packet, by: null, cancellationToken).Bind(static run => run.Output.Trim() is var hash && hash.StartsWith("sha256:", StringComparison.Ordinal)
            ? Results.Success(hash)
            : ScopingErrors.PacketToolFailed.With("aw-packets hash printed no sha256 hash")));

    public ValueTask<Result<PacketFreeze>> Freeze(PacketRef packet, PacketFreezer by, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Answer("freeze", packet, by, cancellationToken).Map(static json => new PacketFreeze(
            Flag(json, "frozen"), Text(json, "hash"), Freezer(Text(json, "by")) ?? PacketFreezer.Machine, Flag(json, "alreadyFrozen"), Freezer(Text(json, "alreadyFrozenBy")), Failures(json))));

    // Exit 0 and 1 carry a JSON answer (1 is a failed check).
    private Result<JsonObject> Answer(string verb, PacketRef packet, PacketFreezer? by, CancellationToken cancellationToken) =>
        Run(verb, packet, by, cancellationToken).Bind(run => Parse(run.Output) is { } json ? Results.Success(json) : NoAnswer(verb, run));

    // No JSON on stdout: exit 1 is the tool's own refusal of a malformed packet (AW-PACKETS-003, -004), reported on stderr; the packet is at fault, not the install.
    private Error NoAnswer(string verb, ToolRun run) => run.ExitCode == 1
        ? ScopingErrors.PacketToolFailed.With($"aw-packets {verb} refused the packet: {ToolError(run.Error)}")
        : Failed(verb, Program(), $"it printed no JSON: {Tail(run.Output)}");

    // The tool's Error record on stderr (--json) as "CODE message", else the raw tail.
    private static string ToolError(string stderr) => Parse(stderr) is { } json && Text(json, "code") is { } code
        ? $"{code} {Text(json, "message")}".Trim()
        : Tail(stderr);

    // The finished run: exit 0, and exit 1 for a verb whose answer is the check result.
    private Result<ToolRun> Run(string verb, PacketRef packet, PacketFreezer? by, CancellationToken cancellationToken)
    {
        var program = Program();
        var run = runner.Run(program, Arguments(verb, packet, by), _timeout, options: null, cancellationToken);
        if (run.Outcome != ToolOutcome.Exited)
        {
            return Failed(verb, program, run.Outcome.ToString());
        }

        var accepted = run.ExitCode == 0 || (run.ExitCode == 1 && verb != "hash");
        return accepted ? run : Failed(verb, program, $"exit {run.ExitCode.ToString(CultureInfo.InvariantCulture)}: {Tail(run.Error.Length > 0 ? run.Error : run.Output)}");
    }

    private string Program() => environment("AW_PACKETS_BIN") is { Length: > 0 } given ? given : "aw-packets";

    private static List<string> Arguments(string verb, PacketRef packet, PacketFreezer? by)
    {
        var arguments = new List<string> { verb };
        if (packet.Kind != PacketKind.Build)
        {
            arguments.AddRange(["--kind", packet.Kind.ToString().ToLowerInvariant()]);
        }

        arguments.Add(packet.Directory);
        AddOption(arguments, "--repo", verb == "hash" || packet.Kind == PacketKind.Scoping ? null : packet.Repo);   // a scoping packet has no repo: aw-packets rejects --repo for it
        AddOption(arguments, "--repo-slug", verb == "freeze" ? packet.RepoSlug : null);
        AddOption(arguments, "--instance", verb == "freeze" ? packet.Instance : null);
        AddOption(arguments, "--by", by is { } who ? who.ToString().ToLowerInvariant() : null);
        if (verb != "hash")
        {
            arguments.Add("--json");
        }

        return arguments;
    }

    private static void AddOption(List<string> arguments, string name, string? value)
    {
        if (value is not null)
        {
            arguments.AddRange([name, value]);
        }
    }

    private static JsonObject? Parse(string output)
    {
        try
        {
            return JsonNode.Parse(output) as JsonObject;
        }
        catch (JsonException)
        {
            return null;   // boundary: the tool printed something else
        }
    }

    private static bool Flag(JsonObject json, string name) => json[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static string? Text(JsonObject json, string name) => json[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static PacketFreezer? Freezer(string? name) => name switch
    {
        "owner" => PacketFreezer.Owner,
        "machine" => PacketFreezer.Machine,
        _ => null,
    };

    private static List<PacketFailure> Failures(JsonObject json) => json["checks"] is JsonArray checks
        ? [.. checks.OfType<JsonObject>().Where(static c => Text(c, "state") is { } state && (string.Equals(state, "fail", StringComparison.Ordinal) || string.Equals(state, "omitted", StringComparison.Ordinal))).Select(static c => new PacketFailure(Text(c, "rule") ?? "?", Text(c, "subject") ?? "packet", Text(c, "message") ?? string.Empty, Text(c, "fix")))]
        : [];

    private static string Tail(string text) => text.Trim() is { Length: > 300 } trimmed ? trimmed[^300..] : text.Trim();

    private static Error Failed(string verb, string program, string why) =>
        ScopingErrors.PacketToolFailed.With($"aw-packets {verb} failed ({program}: {why}) (fix: build aw-packets and put it on the PATH, or set AW_PACKETS_BIN)");
}
