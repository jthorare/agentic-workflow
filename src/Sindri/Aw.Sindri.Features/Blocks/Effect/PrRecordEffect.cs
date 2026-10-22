using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Blocks.Effect;

/// <summary>
/// 'effect.pr.record' (PB spec §4.7): appends a 'recheck' or 'docs' row to the merge record, 'babysit.jsonl' in the instance's run area, written from the fact
/// the child returned. A row carries its kind, the effect identity (what keeps a repeated dispatch from writing twice), the instance, the head 'fact:prState' holds,
/// the time and the result. It replies nothing.
/// </summary>
/// <param name="area">The instance's run area, where the merge record lives.</param>
/// <param name="contents">Reads the facts it takes.</param>
/// <param name="clock">When a row is written.</param>
internal sealed class PrRecordEffect(IRunArea area, IFactContents contents, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    private const string Record = "babysit.jsonl";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => PrEffects.Record;

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.Arguments.TryGetValue("kind", out var kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() is not ("recheck" or "docs"))
        {
            return PrEffectFacts.Missing(context, "kind 'recheck' or 'docs'");
        }

        var result = await PrEffectFacts.Content(context, "result", contents, cancellationToken).ConfigureAwait(false);
        var state = result.IsError ? result.Error : await PrEffectFacts.Held(context, "fact:prState", contents, cancellationToken).ConfigureAwait(false);
        var before = state.IsError ? state.Error : await area.Read(context.Instance, Record, cancellationToken).ConfigureAwait(false);
        if (before.IsError)
        {
            return before.Error;
        }

        if (Written(before.Value, context.Identity))
        {
            return new EffectDone(Reply: null, Existed: true);
        }

        var row = new JsonObject
        {
            ["kind"] = kind.GetString(),
            ["identity"] = context.Identity,
            ["instance"] = context.Instance,
            ["headSha"] = state.Value is { } held ? PrEffectFacts.Head(held) : null,
            ["at"] = clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["result"] = JsonNode.Parse(result.Value.GetRawText()),
        };
        var written = await area.Write(context.Instance, Record, row.ToJsonString() + "\n", append: true, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new EffectDone(Reply: null, Existed: false);
    }

    private static bool Written(string record, string identity) =>
        record.Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(line => line.Contains("\"identity\":" + JsonValue.Create(identity).ToJsonString(), StringComparison.Ordinal));
}
