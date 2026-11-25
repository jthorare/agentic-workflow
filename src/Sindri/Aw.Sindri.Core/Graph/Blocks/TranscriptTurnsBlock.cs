using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>
/// The 'transcript_turns' block (issue 3787): lists the newest human turns of the local agent transcripts and hands them on as batches of refs, with no text.
/// 400 refs are about 12 KB, so the list fits the 64 KB node output cap; a 'transcript_batch' inside the loop reads each batch's text.
/// </summary>
/// <param name="transcripts">Where the transcripts are read.</param>
/// <param name="clock">The clock 'since' counts back from.</param>
public sealed class TranscriptTurnsBlock(IGraphTranscriptPort transcripts, TimeProvider clock) : IGraphBlock
{
    /// <summary>The most turns a node may ask for: 20 batches of 20, what one steering-miner run labels.</summary>
    public const int MaxTurnsCeiling = 400;

    private const string DefaultSince = "7d";
    private const int DefaultBatchSize = TranscriptBatchBlock.MaxTurns;
    private static readonly string[] _providers = ["claude-code", "codex", "cursor"];

    private static readonly BlockOutputs _outputs = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["batches"] = "array",
        ["sessions"] = "number",
        ["turns"] = "number",
        ["truncated"] = "boolean",
    });

    /// <inheritdoc />
    public string Type => "transcript_turns";

    /// <inheritdoc />
    public string Title => "Transcript turns";

    /// <inheritdoc />
    public BlockGroup Group => BlockGroup.Data;

    /// <inheritdoc />
    public bool IsTrigger => false;

    /// <inheritdoc />
    public BlockSchema Config { get; } = new([
        new("since", BlockFieldType.Text, Required: false, Caption: "How far back"),
        new("providers", BlockFieldType.TextList, Required: false, Caption: "Agents"),
        new("repo", BlockFieldType.Text, Required: false, Caption: "Repository"),
        new("batchSize", BlockFieldType.Number, Required: false, Caption: "Turns per batch"),
        new("maxTurns", BlockFieldType.Number, Required: false, Caption: "Most turns"),
    ]);

    /// <inheritdoc />
    public string Example => """{"since":"7d","providers":["claude-code"],"batchSize":20,"maxTurns":400}""";

    /// <inheritdoc />
    public BlockOutputs OutputsFor(JsonObject config) => _outputs;

    /// <inheritdoc />
    public IReadOnlyList<(string Field, string Problem)> CheckConfig(JsonObject config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Settings.Read(config) is { IsError: true } bad ? [(bad.Error.Message.Split('\'')[1], bad.Error.Message)] : [];   // why: the message starts with the field's name in quotes
    }

    /// <inheritdoc />
    public async ValueTask<Result<BlockResult>> Execute(BlockContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var settings = Settings.Read(context.Config);
        if (settings.IsError)
        {
            return SindriErrors.GraphBlockFailed.With(settings.Error.Message);
        }

        var (since, providers, repo, batchSize, maxTurns) = settings.Value;
        var listed = await transcripts.HumanTurns(new TranscriptTurnQuery(clock.GetUtcNow() - since, providers, repo), cancellationToken).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        // why: the newest turns win, so a long week is cut at its oldest end
        var taken = listed.Value.OrderByDescending(static t => t.At).ThenBy(static t => t.Ref, StringComparer.Ordinal).Take(maxTurns).ToList();
        var batches = new JsonArray();
        foreach (var chunk in taken.Chunk(batchSize))
        {
            var batch = new JsonArray();
            foreach (var turn in chunk)
            {
                batch.Add((JsonNode)turn.Ref);
            }

            batches.Add((JsonNode)batch);
        }

        return new BlockResult(new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["batches"] = batches,
            ["sessions"] = taken.Select(static t => t.Session).Distinct(StringComparer.Ordinal).Count(),
            ["turns"] = taken.Count,
            ["truncated"] = listed.Value.Count > taken.Count,
        });
    }

    private sealed record Settings(TimeSpan Since, IReadOnlyList<string> Providers, string? Repo, int BatchSize, int MaxTurns)
    {
        // why: one reader for the validator and the run, so a config that validates runs the same way; a field that is one reference is checked only at run time
        public static Result<Settings> Read(JsonObject config)
        {
            var since = ReadSince(config);
            if (since.IsError)
            {
                return since.Error;
            }

            var providers = ReadProviders(config);
            if (providers.IsError)
            {
                return providers.Error;
            }

            var batchSize = ConfigValues.Int(config["batchSize"]) ?? DefaultBatchSize;
            if (batchSize is < 1 or > TranscriptBatchBlock.MaxTurns)
            {
                return Invalid("batchSize", $"must be from 1 to {TranscriptBatchBlock.MaxTurns.ToString(CultureInfo.InvariantCulture)}, the most turns a 'transcript_batch' reads");
            }

            var maxTurns = ConfigValues.Int(config["maxTurns"]) ?? MaxTurnsCeiling;
            return maxTurns is < 1 or > MaxTurnsCeiling
                ? Invalid("maxTurns", $"must be from 1 to {MaxTurnsCeiling.ToString(CultureInfo.InvariantCulture)}")
                : new Settings(since.Value, providers.Value, ConfigValues.Text(config["repo"]), batchSize, maxTurns);
        }

        private static Result<TimeSpan> ReadSince(JsonObject config)
        {
            var text = ConfigValues.Text(config["since"]) ?? DefaultSince;
            var since = ConfigValues.IsReference(text) ? Results.Success(TimeSpan.FromDays(7)) : BlockDuration.Parse(text);
            return since.IsError ? Invalid("since", $"must be a length of time such as 7d or 12h, not '{text}'").Error : since;
        }

        private static Result<IReadOnlyList<string>> ReadProviders(JsonObject config)
        {
            List<string> providers = config["providers"] is JsonArray list ? [.. list.Select(static p => ConfigValues.Text(p) ?? string.Empty)] : [];
            return providers.Find(p => !_providers.Contains(p, StringComparer.Ordinal)) is { } unknown
                ? Invalid("providers", $"names '{unknown}'; the agents are {string.Join(", ", _providers)}").Error
                : Results.Success<IReadOnlyList<string>>(providers);
        }

        private static Result<Settings> Invalid(string field, string problem) => SindriErrors.GraphBlockFailed.With($"'{field}' {problem}");
    }
}
