using System.Text.Json;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Features.Blocks.Evidence;

/// <summary>
/// 'evidence.check' (DT spec §5.5, §8.7): re-runs the frozen reproduction 'fact:check' on the candidate ('fact:candidate': its id and worktree) through bugfix-state, which
/// records the run for the candidate, and passes when the check passes. It collects at 'head', the candidate's tree; 'base' has no tree in the facts, so it does not pass.
/// Evidence it can't collect (a missing or unfrozen check, no candidate, no instance address, a refusal or an unavailable bugfix-state) does not pass and says why, so
/// the transition refuses rather than being waved through.
/// </summary>
/// <param name="state">bugfix-state.</param>
/// <param name="area">The instance's run area, where its bug fix state lives.</param>
internal sealed class EvidenceCheckProvider(IBugFixState state, IRunArea area) : IEvidenceProvider, IDescribedBlock
{
    private const string Name = "evidence.check";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => EvidenceProviders.Check;

    /// <inheritdoc/>
    public async ValueTask<EvidenceResult> Collect(EvidenceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (context.At != EvidencePoint.Head)
        {
            return Fail($"{Name} checks the candidate at head; it has no tree for the base");
        }

        if (FrozenCheckOf(Read(context, "fact:check")) is not { } check)
        {
            return Fail($"{Name} needs fact:check to be the frozen check: a 'test' check with a file and a command, or a 'ui' check with a file");
        }

        if (Candidate(Read(context, "fact:candidate")) is not ({ } id, { } worktree))
        {
            return Fail($"{Name} needs fact:candidate to carry the candidate's id and worktree");
        }

        if (string.IsNullOrEmpty(context.Address))
        {
            return Fail($"{Name} has no instance address to place the bug fix state under");
        }

        var placed = area.PathFor(context.Address, "bugfix");
        if (placed.IsError)
        {
            return Fail($"{Name} can't place the bug fix state: {placed.Error.Message}");
        }

        var ran = await state.Recheck(placed.Value, id, worktree, check, cancellationToken).ConfigureAwait(false);
        if (ran.Status != BugFixAnswerStatus.Ok || ran.Value is not { } run)
        {
            return Fail(ran.Refusal is { } refusal ? $"{Name}: bugfix-state refused: {refusal.Code} {refusal.Message}" : $"{Name}: {ran.Unavailable}");
        }

        return run.Passed ? new EvidenceResult(Passed: true, []) : Fail($"the frozen check fails on candidate {run.Candidate} (exit {run.ExitCode}); run record {run.Evidence}");
    }

    private static EvidenceResult Fail(string reason) => new(Passed: false, [reason]);

    private static JsonElement? Read(EvidenceContext context, string key) => context.Facts.TryGetValue(FactKey.From(key), out var fact) ? fact : null;

    private static string? Text(JsonElement? fact, string name) =>
        fact is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static (string Id, string? Worktree)? Candidate(JsonElement? fact) => Text(fact, "id") is { } id ? (id, Text(fact, "worktree")) : null;

    private static FrozenCheck? FrozenCheckOf(JsonElement? fact)
    {
        var kind = Text(fact, "kind");
        var file = Text(fact, "file");
        if (file is null || kind is not ("test" or "ui") || Command(fact) is not { } command || (kind == "test" && command.Count == 0))
        {
            return null;
        }

        return new FrozenCheck(kind, file, command, Text(fact, "directory") ?? ".");
    }

    // The command's words; empty when there is none, null when an item is not text.
    private static List<string>? Command(JsonElement? fact)
    {
        if (fact is not { ValueKind: JsonValueKind.Object } obj || !obj.TryGetProperty("command", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var command = new List<string>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } text)
            {
                return null;
            }

            command.Add(text);
        }

        return command;
    }
}
