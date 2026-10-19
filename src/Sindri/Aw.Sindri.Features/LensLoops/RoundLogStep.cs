using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// A lens loop's log Step on the block runtime (line spec §4.1, §5.2, DTC4): appends the lens report to the round log; 'next' while a lens is still queued;
/// at the round's end the subclass reads the round's findings into the loop's outcome. Caps and no-progress are the recipe's loops (spec §3.7): the Step reports
/// the round and nothing else, as it does at the desk. The desk's run-area file, lens-rounds.jsonl, is the design packet's own file, assembled from
/// 'fact:lensRounds' by 'step.design.evidence'; the log is the fact. The runner has already failed the Step when a fact its descriptor reads is missing.
/// </summary>
/// <param name="scale">The loop's severity scale.</param>
internal abstract class RoundLogStep(LensScale scale) : IBlockStep
{
    private static readonly string[] _read = ["lensReport", "lensRounds", "lensQueue"];

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var texts = _read.Where(k => context.Facts.ContainsKey(FactKey.From("fact:" + k)))
            .ToDictionary(static k => k, k => Text(context.Facts[FactKey.From("fact:" + k)]), StringComparer.Ordinal);
        var appended = RoundLog.Append(scale, texts);
        if (appended.IsError)
        {
            return ValueTask.FromResult(BlockStepResult.Failed(appended.Error.Message));
        }

        var log = appended.Value;
        return ValueTask.FromResult(log.Ended
            ? Ended(log)
            : Done("next", ("lensRounds", log.Rounds), ("lensQueue", log.Queue)));
    }

    /// <summary>The outcome and facts at the round's end.</summary>
    /// <param name="log">The round's log, its last report appended.</param>
    /// <returns>The Step's result.</returns>
    protected abstract BlockStepResult Ended(RoundLog log);

    /// <summary>A completed result: the outcome and the facts, as parsed elements.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="facts">Fact keys (without the prefix) and their values.</param>
    /// <returns>The result.</returns>
    protected static BlockStepResult Done(string outcome, params (string Key, JsonNode Value)[] facts) => BlockStepResult.Completed(
        OutcomeName.From(outcome),
        facts.ToDictionary(static f => FactKey.From("fact:" + f.Key), static f => Element(f.Value)));

    private static JsonElement Element(JsonNode node)
    {
        using var parsed = JsonDocument.Parse(LoopJson.Write(node));
        return parsed.RootElement.Clone();
    }

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
}
