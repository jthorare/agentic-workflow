using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Ship;

/// <summary>
/// 'step.ship.pack' on the block runtime (line spec §7.5): assembles the hand-off packet (<see cref="HandoffPack"/>) from the phase packet, the proof pack and the review result into the instance's
/// run area ('handoff/'), packed on the phase worktree's head, which may be newer than the proof's or the review's: 'step.ship.freeze' calls that stale. 'packed' carries
/// <c language="text">handoffDraft = {dir, head, workspace}</c>. The title, labels and body pass the scrubber, and then the canary scan (PRV-HOLD-003) against the phase's hidden set: a hit fails the Step, naming the rule and not the
/// phrase, and nothing is written. The Step also fails (an unrouted failure parks the instance for the owner) when a fact, a packet directory or one of its files is missing or not what it should be, when the phase has no
/// hidden set to take the canary from, when the worktree has no head or when the run area cannot be written. A relative <c language="text">phasePacket.dir</c> is read under the repo's checkout.
/// </summary>
/// <param name="files">Reads the phase, proof and review packets and writes the hand-off draft.</param>
/// <param name="checkouts">Where the repo is checked out.</param>
/// <param name="holdouts">The hidden-scenario store, for the canary phrase.</param>
/// <param name="diff">Reads the phase worktree's head.</param>
/// <param name="scrubber">Redacts secrets from the title, labels and body.</param>
internal sealed class ShipPackStep(IPacketDirectory files, IRepoCheckouts checkouts, IHoldoutStore holdouts, IRunDiff diff, ITextScrubber scrubber) : IBlockStep, IBlockStepId, IRepoStep
{
    private const string DraftName = "handoff";

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.ship.pack");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (ShipPackInputs.Of(context) is not { } inputs)
        {
            return Failed("needs the instance's address and repo, fact:phasePacket, fact:proofPack and fact:review (each with its dir and hash) and fact:workspace (a path)");
        }

        var checkout = await checkouts.Of(inputs.Repo, cancellationToken).ConfigureAwait(false);
        if (checkout.IsError)
        {
            return Failed($"has no checkout of {inputs.Repo.Value}: {checkout.Error.Message}");
        }

        var (sources, failure) = await Load(inputs, Path.IsPathRooted(inputs.PacketDir) ? inputs.PacketDir : Path.Combine(checkout.Value, inputs.PacketDir), cancellationToken).ConfigureAwait(false);
        return sources is null ? Failed(failure!) : await Pack(sources, cancellationToken).ConfigureAwait(false); // why: no sources come with their reason
    }

    private static BlockStepResult Failed(string reason) => BlockStepResult.Failed("step.ship.pack " + reason);

    private async ValueTask<(HandoffSources? Sources, string? Failure)> Load(ShipPackInputs inputs, string phaseDir, CancellationToken cancellationToken)
    {
        var (phase, phaseFailure) = await Read(phaseDir, ["packet.json", "phase.json"], cancellationToken).ConfigureAwait(false);
        var (proof, proofFailure) = await Read(inputs.ProofDir, ["acceptance.json", "holdout.json", "not-proven.md"], cancellationToken).ConfigureAwait(false);
        var (review, reviewFailure) = await Read(inputs.ReviewDir, ["review.json"], cancellationToken).ConfigureAwait(false);
        if ((phaseFailure ?? proofFailure ?? reviewFailure) is { } unreadable)
        {
            return (null, unreadable);
        }

        var head = await diff.Head(inputs.Workspace, cancellationToken).ConfigureAwait(false);
        return head.IsError ? (null, $"can't read the head of {inputs.Workspace}: {head.Error.Message}") : Parse(inputs, head.Value, phase, proof, review);
    }

    private static (HandoffSources? Sources, string? Failure) Parse(ShipPackInputs inputs, string head, Dictionary<string, string> phase, Dictionary<string, string> proof, Dictionary<string, string> review)
    {
        try
        {
            var packet = JsonNode.Parse(phase["packet.json"]);
            JsonNode?[] parsed = [JsonNode.Parse(phase["phase.json"]), JsonNode.Parse(proof["acceptance.json"]), JsonNode.Parse(proof["holdout.json"]), JsonNode.Parse(review["review.json"])];
            if (StepFacts.Field(packet, "id") is not { } id || StepFacts.Field(packet, "repo") is not { } repo || StepFacts.Field(packet, "baseSha") is not { } baseSha || Array.Exists(parsed, static n => n is null))
            {
                return (null, "packet.json must name the packet's id, repo and baseSha, and the other packet files must be JSON objects");
            }

            return (new HandoffSources(inputs, head, id, repo, baseSha, parsed[0]!, proof["acceptance.json"], parsed[1]!, parsed[2]!, proof["not-proven.md"], parsed[3]!), null);   // why: Array.Exists above found none null
        }
        catch (JsonException e)
        {
            return (null, $"a packet file is not the JSON it should be: {e.Message}");
        }
    }

    private async ValueTask<(Dictionary<string, string> Files, string? Failure)> Read(string directory, string[] names, CancellationToken cancellationToken)
    {
        var read = await files.Read(directory, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return ([], $"can't read the packet in {directory}: {read.Error.Message}");
        }

        var missing = names.FirstOrDefault(n => !read.Value.ContainsKey(n));
        return missing is null ? (new Dictionary<string, string>(read.Value, StringComparer.Ordinal), null) : ([], $"can't use the packet in {directory}: it has no {missing}");
    }

    private async ValueTask<BlockStepResult> Pack(HandoffSources sources, CancellationToken cancellationToken)
    {
        var address = sources.Inputs.Address;
        var set = await holdouts.Set(address.Split('/')[0], sources.PacketId, cancellationToken).ConfigureAwait(false);
        if (set.IsError || set.Value.Scenarios.FirstOrDefault(static s => s.IsCanary) is not { Phrase: { } phrase })
        {
            return Failed($"can't scan for the canary: {(set.IsError ? set.Error.Message : "the phase's hidden set has no canary scenario")}");
        }

        var title = scrubber.Scrub(HandoffPack.Title(sources.Goal));
        var labels = HandoffPack.Labels(sources).Select(scrubber.Scrub).ToList();
        var body = scrubber.Scrub(HandoffPack.Body(sources));
        if (new[] { title, body }.Concat(labels).Any(t => t.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
        {
            return Failed("found the canary of the phase's hidden set in the pull request text (PRV-HOLD-003); nothing was written, and the text that carries it comes from the phase packet, the proof pack or the review result");
        }

        var draft = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["packet.json"] = HandoffPack.Packet(sources).ToJsonString(),
            ["index.json"] = HandoffPack.Index(sources).ToJsonString(),
            ["pr.json"] = HandoffPack.Pr(sources, title, labels).ToJsonString(),
            ["body.md"] = body,
            ["green.json"] = HandoffPack.Green(sources).ToJsonString(),
        };
        var directory = await files.Create(address, DraftName, draft, cancellationToken).ConfigureAwait(false);
        return directory.IsError
            ? Failed($"can't write the hand-off draft: {directory.Error.Message}")
            : StepFacts.Done("packed", ("fact:handoffDraft", new JsonObject { ["dir"] = directory.Value, ["head"] = sources.Head, ["workspace"] = sources.Inputs.Workspace }));
    }
}
