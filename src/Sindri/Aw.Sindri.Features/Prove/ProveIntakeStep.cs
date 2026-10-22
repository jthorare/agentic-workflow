using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.intake' on the block runtime (line spec §7.3): loads the phase's items and their signals from the phase packet (acceptance.json and signals.json) and seeds the
/// facts the prove station's later states read. 'proof' is <c language="text">{head, items[{id, text, signals[], artifacts[]}]}</c> for every item that is not superseded, 'holdoutResult' and
/// 'beforeAfter' start as <c language="text">{commit, results[]}</c>, 'findings' as an empty list. A packet whose files cannot be read fails the Step with the reason.
/// </summary>
/// <param name="files">Reads the phase packet's files.</param>
/// <param name="checkouts">Where the repo is checked out (a relative packet directory is read from it).</param>
internal sealed class ProveIntakeStep(IBriefFiles files, IRepoCheckouts checkouts) : IBlockStep, IBlockStepId, IRepoStep
{
    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.intake");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var dir = StepFacts.Read(context, "fact:phasePacket") is JsonObject packet && packet["dir"] is JsonValue d && d.TryGetValue<string>(out var text) ? text : null;
        var head = StepFacts.Head(StepFacts.Read(context, "fact:built"));
        if (dir is null || head is null)
        {
            return BlockStepResult.Failed("step.prove.intake needs fact:phasePacket (with its dir) and fact:built (with its head)");
        }

        var (resolved, unresolved) = await Resolve(context, dir, cancellationToken).ConfigureAwait(false);
        if (resolved is null)
        {
            return BlockStepResult.Failed(unresolved!); // why: a null directory comes with its reason
        }

        dir = resolved;
        var (acceptance, acceptanceFailure) = await ReadJson(dir, "acceptance.json", cancellationToken).ConfigureAwait(false);
        var (signals, signalsFailure) = await ReadJson(dir, "signals.json", cancellationToken).ConfigureAwait(false);
        if (acceptanceFailure is not null || signalsFailure is not null)
        {
            return BlockStepResult.Failed($"step.prove.intake can't load the phase packet {dir}: {acceptanceFailure ?? signalsFailure}");
        }

        var items = Items(acceptance, signals);
        return StepFacts.Done(
            "ready",
            ("fact:findings", new JsonArray()),
            ("fact:proof", new JsonObject { ["head"] = head, ["items"] = items }),
            ("fact:holdoutResult", Empty(head)),
            ("fact:beforeAfter", Empty(head)));
    }

    // A relative directory is read under the repo's checkout, never under serve's working directory; an absolute one is read as it is.
    private async ValueTask<(string? Dir, string? Failure)> Resolve(StepContext context, string dir, CancellationToken cancellationToken)
    {
        if (Path.IsPathRooted(dir))
        {
            return (dir, null);
        }

        if (context.Repo is not { } repo)
        {
            return (null, $"step.prove.intake needs the instance's repo to read the relative phase packet {dir}");
        }

        var checkout = await checkouts.Of(repo, cancellationToken).ConfigureAwait(false);
        return checkout.IsError ? (null, $"step.prove.intake has no checkout of {repo.Value} to read the phase packet {dir} from: {checkout.Error.Message}") : (PhaseDir.Of(dir, checkout.Value), null);
    }

    private static JsonObject Empty(string head) => new() { ["commit"] = head, ["results"] = new JsonArray() };

    private static JsonArray Items(JsonNode? acceptance, JsonNode? signals)
    {
        var items = new JsonArray();
        foreach (var item in (acceptance?["items"] as JsonArray ?? []).OfType<JsonObject>().Where(static i => (string?)i["status"] != "superseded"))
        {
            var id = (string?)item["id"] ?? string.Empty;
            var own = new JsonArray();
            foreach (var signal in PhaseSignals.Of(signals, id))
            {
                own.Add((JsonNode)signal);
            }

            items.Add((JsonNode)new JsonObject { ["id"] = id, ["text"] = (string?)item["text"] ?? string.Empty, ["signals"] = own, ["artifacts"] = new JsonArray() });
        }

        return items;
    }

    private async ValueTask<(JsonNode? Node, string? Failure)> ReadJson(string dir, string name, CancellationToken cancellationToken)
    {
        var read = await files.Read(name, [dir], cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return (null, $"{name}: {read.Error.Message}");
        }

        try
        {
            return (JsonNode.Parse(read.Value.Text), null);
        }
        catch (System.Text.Json.JsonException e)
        {
            return (null, $"{name} is not JSON: {e.Message}");
        }
    }
}
