using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Packets.Core.Checks;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Core.Kinds.Line;

/// <summary>
/// The line kinds' schema checks (LN-PKT1, <see cref="LineRules"/>), one engine for all six: 001 to 006 are shared,
/// then each kind's own, then PKT-BASE-001 (every kind) and, for a plan graph, the build kind's task rules over its
/// tasks.json (LN10). Pure: the directory's files and the base commit's files come in. What the files mean stays in
/// Sindri's DesignPacketChecks, PlanChecks and ProofChecks.
/// </summary>
internal static class LinePacketChecks
{
    private const string ManifestFile = PacketReader.ManifestFile;

    /// <summary>Reads packet.json as the kind's manifest.</summary>
    /// <param name="kind">A line kind.</param>
    /// <param name="files">The packet as read.</param>
    /// <param name="json">The package's JSON options.</param>
    /// <returns>The manifest view, or none when packet.json is missing or doesn't parse (the checks say why).</returns>
    internal static ManifestRead ReadManifest(PacketKind kind, LinePacketFiles files, JsonSerializerOptions json) =>
        Manifest(kind, files, json);

    internal static IReadOnlyList<CheckResult> Run(PacketKind kind, LinePacketFiles files, LineInputs inputs, JsonSerializerOptions json)
    {
        var (read, tree, sizes, sealedSet) = inputs;
        var rules = LineRules.For(kind) ?? throw new ArgumentException($"'{kind.Name}' is not a line kind.", nameof(kind));
        var checks = new CheckList();
        var context = new LineContext(kind, rules, files, read, json, checks) { SealedSet = sealedSet };
        context.Common();
        var plan = context.KindRules();
        if (plan is not null && tree is not null && context.Manifest is { } manifest)
        {
            // the task plan is a build packet's, minus the decision log and the out-of-scope list a plan graph doesn't have
            var packet = new Packet(Shell(manifest), new DecisionLog(1, []), new OutOfScope(1, []), plan);
            context.Append(PacketChecks.RunPlan(packet, tree, sizes));
        }
        else
        {
            context.Base(tree);
            if (kind.Name == "plan")
            {
                const string Why = "tasks.json can't be read, so PKT's task rules didn't run";
                checks.Unchecked(PacketRules.UniqueIds, Why);
                checks.Unchecked(PacketRules.TaskInWorkstream, Why);
                checks.Unchecked(PacketRules.KnownDependencies, Why);
                checks.Unchecked(PacketRules.Acyclic, Why);
                checks.Unchecked(PacketRules.ScopeExists, Why);
                checks.Unchecked(PacketRules.ScopeOverlap, Why);
                checks.Unchecked(PacketRules.AcceptanceText, Why);
                checks.Unchecked(PacketRules.Evidence, Why);
                checks.Unchecked(PacketRules.Decisions, Why);
                checks.Unchecked(PacketRules.HoldoutExists, Why);
                checks.Unchecked(PacketRules.HoldoutStored, Why);
                checks.Unchecked(PacketRules.SizeBudget, Why);
            }
        }

        return checks.Results;
    }

    private static PacketManifest Shell(LineManifest manifest) => new()
    {
        SchemaVersion = manifest.SchemaVersion,
        Id = manifest.Id,
        Repo = manifest.Repo,
        BaseSha = manifest.BaseSha,
        Status = manifest.Status,
        Hash = manifest.Hash,
        Links = new PacketLinks { Decisions = string.Empty, OutOfScope = string.Empty, Tasks = "tasks.json", Design = [], Plans = [] },
    };

    internal static ManifestRead Manifest(PacketKind kind, LinePacketFiles files, JsonSerializerOptions json)
    {
        if (!files.Present.TryGetValue(ManifestFile, out var bytes) || PacketFileShape.Problem(new PacketFileSpec(ManifestFile, PacketFileFormat.Json), bytes) is not null)
        {
            return new ManifestRead(Manifest: null, Problem: null);
        }

        var parsed = kind.Name switch
        {
            "design" => AwJson.Deserialize<LinePacketManifest<DesignLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "plan" => AwJson.Deserialize<LinePacketManifest<PlanLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "phase" => AwJson.Deserialize<LinePacketManifest<PhaseLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "proof" => AwJson.Deserialize<LinePacketManifest<ProofLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            "review" => AwJson.Deserialize<LinePacketManifest<ReviewLinks>>(bytes.Span, json).Map(static m => m.ToView()),
            _ => AwJson.Deserialize<LinePacketManifest<HandoffLinks>>(bytes.Span, json).Map(static m => m.ToView()),
        };
        return parsed.IsError ? new ManifestRead(Manifest: null, Problem: LineProblems.Plain(ManifestFile, parsed.Error.Message)) : new ManifestRead(Manifest: parsed.Value, Problem: null);
    }
}
