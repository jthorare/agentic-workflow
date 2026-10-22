using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Prove;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

/// <summary>
/// 'step.prove.collect' on the block runtime (BS-13): the hand test, verify and evidence children name the files they produced as <c language="text">{artifacts: [path | {path, kind?, item?, signal?, passing?}]}</c>
/// in 'manualTest', 'evidencePack' and 'evidenceSummary' (a combined summary names them in its 'ui', 'api' and 'db' parts). Each file is hashed here, through <see cref="IArtifactFiles"/>, and
/// appended to 'proof' as <c language="text">{kind, path, sha256, commit, at, passing, signal?}</c> with the built head as its commit and the clock as its time; a hash or commit the agent typed is never read.
/// A file with an item backs that item, one with a signal backs the items that hold the signal, one with neither backs every item. A file that is missing, outside the worktree or unreadable adds nothing,
/// so the evidence Step's PRV-ACC-001 names the item it leaves unbacked; artifacts already in 'proof' (an older head's included) stay.
/// </summary>
/// <param name="files">Hashes the named files.</param>
/// <param name="clock">Stamps each artifact.</param>
internal sealed class ProveCollectStep(IArtifactFiles files, TimeProvider clock) : IBlockStep, IBlockStepId
{
    private static readonly (string Fact, string Part, string Kind)[] _sources =
    [
        ("fact:manualTest", string.Empty, "app-run"),
        ("fact:evidencePack", string.Empty, "verify"),
        ("fact:evidenceSummary", string.Empty, "evidence"),
        ("fact:evidenceSummary", "ui", "ui-evidence"),
        ("fact:evidenceSummary", "api", "api-evidence"),
        ("fact:evidenceSummary", "db", "db-evidence"),
    ];

    private static readonly string[] _identity = ["path", "commit", "sha256", "kind"];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.prove.collect");

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var head = StepFacts.Head(StepFacts.Read(context, "fact:built"));
        var workspace = StepFacts.Text(context, "fact:workspace");
        if (StepFacts.Read(context, "fact:proof") is not JsonObject proof || proof["items"] is not JsonArray items || head is null || workspace is null)
        {
            return BlockStepResult.Failed("step.prove.collect needs fact:proof (with its items), fact:built (with its head) and fact:workspace");
        }

        var at = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        foreach (var (fact, part, defaultKind) in _sources)
        {
            foreach (var entry in Entries(StepFacts.Read(context, fact), part))
            {
                await Add(items, entry, defaultKind, (workspace, head, at), cancellationToken).ConfigureAwait(false);
            }
        }

        return StepFacts.Done("collected", ("fact:proof", proof));
    }

    // The entries of a fact's 'artifacts' list (or of one part of it): a bare path or an object with one.
    private static IEnumerable<JsonNode> Entries(JsonNode? fact, string part)
    {
        var holder = part.Length == 0 ? fact : (fact as JsonObject)?[part];
        return holder is JsonObject { } obj && obj["artifacts"] is JsonArray list ? list.OfType<JsonNode>() : [];
    }

    private async ValueTask Add(JsonArray items, JsonNode entry, string defaultKind, (string Workspace, string Head, string At) run, CancellationToken cancellationToken)
    {
        var path = entry is JsonValue v && v.TryGetValue<string>(out var bare) ? bare : StepFacts.Field(entry, "path");
        var item = StepFacts.Field(entry, "item");
        var signal = StepFacts.Field(entry, "signal");
        var targets = string.IsNullOrWhiteSpace(path) ? [] : items.OfType<JsonObject>().Where(i => Backs(i, item, signal)).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var hash = await files.Sha256(run.Workspace, path!, cancellationToken).ConfigureAwait(false);   // why: a blank path has no targets
        if (hash.IsError || hash.Value is not { } sha256)
        {
            return;
        }

        var kind = StepFacts.Field(entry, "kind") ?? defaultKind;
        var artifact = new JsonObject { ["kind"] = kind, ["path"] = path, ["sha256"] = sha256, ["commit"] = run.Head, ["at"] = run.At, ["passing"] = Passing(entry) };
        if (signal is not null)
        {
            artifact["signal"] = signal;
        }

        foreach (var target in targets)
        {
            Append(target, artifact);
        }
    }

    // True unless the child's own entry says false: the recipe reaches 'collect' only after all three children resolved.
    private static bool Passing(JsonNode entry) => entry is not JsonObject { } o || o["passing"] is not JsonValue p || !p.TryGetValue<bool>(out var flag) || flag;

    // The same file on the same head, with the same kind and bytes, is added once however often the Step runs.
    private static void Append(JsonObject target, JsonObject artifact)
    {
        if (target["artifacts"] is not JsonArray artifacts)
        {
            artifacts = [];
            target["artifacts"] = artifacts;
        }

        if (!artifacts.OfType<JsonObject>().Any(a => Same(a, artifact)))
        {
            artifacts.Add(artifact.DeepClone());
        }
    }

    private static bool Same(JsonObject a, JsonObject b) => _identity.All(k => string.Equals((string?)a[k], (string?)b[k], StringComparison.Ordinal));

    // An item backs when named, or when it holds the named signal; a file naming neither backs all.
    private static bool Backs(JsonObject proofItem, string? item, string? signal)
    {
        if (item is not null)
        {
            return string.Equals((string?)proofItem["id"], item, StringComparison.Ordinal);
        }

        return signal is null || (proofItem["signals"] as JsonArray ?? []).OfType<JsonObject>().Any(s => string.Equals((string?)s["id"], signal, StringComparison.Ordinal));
    }
}
