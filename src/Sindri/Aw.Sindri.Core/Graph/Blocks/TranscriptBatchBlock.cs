using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'transcript_batch' block (issue 3787): reads the text of one batch of human turns, usually '{{item}}' of a for_each over the batches 'transcript_turns'
/// hands on. Text keeps its first 1,500 characters and the assistant text before it its last 400, each under a JSON size (<see cref="TranscriptCut"/>), so a
/// batch fits the 64 KB node output cap. The text is a stranger's: a prompt that reads it gets it fenced as untrusted, as every node output is.
/// </summary>
/// <param name="transcripts">Where the transcripts are read.</param>
public sealed partial class TranscriptBatchBlock(IGraphTranscriptPort transcripts) : IGraphBlock
{
    /// <summary>The most turns one batch reads; more are dropped and counted.</summary>
    public const int MaxTurns = 20;

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["turns"] = "array",
        ["missing"] = "number",
        ["dropped"] = "number",
    });

    /// <inheritdoc />
    public string Type => "transcript_batch";

    /// <inheritdoc />
    public string Title => "Transcript batch";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Data;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("refs", BlockFieldType.Text, Caption: "Turn refs"),
        new("before", BlockFieldType.Boolean, Required: false, Caption: "Include the assistant text before each turn"),
    ]);

    /// <inheritdoc />
    public string Example => """{"refs":"{{item}}","before":true}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Config["refs"] is not JsonArray list)
        {
            return SindriErrors.GraphBlockFailed.With("'refs' must be a list of turn refs; point it at a batch such as {{item}} inside a for_each over {{turns.batches}}");
        }

        var refs = list.Select(ConfigValues.Text).OfType<string>().Where(static r => RefShape.IsMatch(r)).Distinct(StringComparer.Ordinal).Take(MaxTurns).ToList();
        var read = await transcripts.Read(refs, ConfigValues.Bool(context.Config["before"]) ?? true, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var turns = new JsonArray();
        foreach (var turn in read.Value.Where(t => refs.Contains(t.Ref, StringComparer.Ordinal)).Take(MaxTurns))
        {
            var row = new JsonObject { ["ref"] = turn.Ref, ["text"] = TranscriptCut.Head(turn.Text, TranscriptCut.TextChars, TranscriptCut.TextBytes) };
            if (!string.IsNullOrWhiteSpace(turn.Before))
            {
                row["before"] = TranscriptCut.Tail(turn.Before, TranscriptCut.BeforeChars, TranscriptCut.BeforeBytes);
            }

            turns.Add((JsonNode)row);
        }

        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["turns"] = turns,
            ["missing"] = refs.Count - turns.Count,
            ["dropped"] = list.Count - refs.Count,
        });
    }

    [GeneratedRegex("^transcript:[0-9a-f]{12}#[1-9][0-9]{0,8}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RefShape { get; }
}
