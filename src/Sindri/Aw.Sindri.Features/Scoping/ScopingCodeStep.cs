using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// A scoping Step the block runtime runs (scoping spec §8.2, §9): the same code the desk helper runs (<see cref="DeskBlocks"/>), moved behind
/// <see cref="IBlockStep"/>, so a scoping instance passes the deterministic Steps without a desk task. The context's facts become the code's
/// facts (JSON text by key, without the 'fact:' prefix); the code's outputs become typed facts again. A subclass names its Step; one that needs
/// more than the facts (gather's Step inputs) overrides <see cref="Extra"/>. A Step that keeps files in the run area (check's rounds.jsonl) does so through <see cref="IRunArea"/>.
/// </summary>
/// <param name="step">The Step's id, which picks the code.</param>
internal abstract class ScopingCodeStep(BlockId step) : IBlockStep
{
    private const string FactPrefix = "fact:";

    /// <inheritdoc/>
    public async ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var extra = await Extra(context, cancellationToken).ConfigureAwait(false);
        if (extra.IsError)
        {
            return Failed(extra.Error);
        }

        var ran = DeskBlocks.Run(step.Value, new ScopeFacts(Texts(context, extra.Value)), context.LoopCaps.GetValueOrDefault(ScopeRules.ChallengeLoop, ScopeRules.DefaultChallengeRounds), context.LoopCaps.GetValueOrDefault(ScopeRules.GapLoop, ScopeRules.DefaultGapRounds));
        if (ran.IsError)
        {
            return Failed(ran.Error);
        }

        var kept = await Keep(context, ran.Value, cancellationToken).ConfigureAwait(false);
        if (kept.IsError)
        {
            return Failed(kept.Error);
        }

        var more = await MoreOutputs(context, ran.Value, cancellationToken).ConfigureAwait(false);
        return more.IsError ? Failed(more.Error) : Completed(ran.Value, more.Value);
    }

    /// <summary>Facts the Step records beyond the code's outputs, such as the hash of a packet it just kept: none by default.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="response">The code's response.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>JSON text by fact key without the 'fact:' prefix, or the error that fails the Step.</returns>
    protected virtual ValueTask<Result<IReadOnlyDictionary<string, string>>> MoreOutputs(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Facts([]));

    /// <summary>Keeps what the code produced beyond the facts, such as the round line: nothing by default.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="response">The code's response.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>True when kept; the error that fails the Step (a line is never dropped).</returns>
    protected virtual ValueTask<Result<bool>> Keep(StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<bool>>(true);

    /// <summary>Writes the response's files into the instance's run area, in order.</summary>
    /// <param name="area">The run area.</param>
    /// <param name="context">The Step's context, which names the instance.</param>
    /// <param name="response">The code's response.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>True when all were written; the first error otherwise.</returns>
    protected static async ValueTask<Result<bool>> WriteFiles(IRunArea area, StepContext context, ScopingDeskResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(response);
        if (context.Address is not { } address)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With("the Step has no instance address, so there is no run area to write to");
        }

        foreach (var file in response.Files)
        {
            var written = await area.Write(address, file.Path, file.Content, file.Append, cancellationToken).ConfigureAwait(false);
            if (written.IsError)
            {
                return written.Error;
            }
        }

        return true;
    }

    /// <summary>The rounds the instance's run area holds, as the 'rounds' fact the code takes (a JSON array, one entry per line), none when no round was kept yet.</summary>
    /// <param name="area">The run area.</param>
    /// <param name="context">The Step's context, which names the instance.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>The code's extra facts, or the error that fails the Step.</returns>
    protected static async ValueTask<Result<IReadOnlyDictionary<string, string>>> Rounds(IRunArea area, StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(area);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Address is not { } address)
        {
            return WorkflowErrors.BlockRunAreaUnavailable.With("the Step has no instance address, so there is no run area to read the rounds from");
        }

        var read = await area.Read(address, RoundsFile, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var lines = read.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Facts(lines.Length == 0 ? [] : new Dictionary<string, string>(StringComparer.Ordinal) { ["rounds"] = "[" + string.Join(',', lines) + "]" });
    }

    /// <summary>The run-area file the rounds are kept in.</summary>
    protected const string RoundsFile = "rounds.jsonl";

    /// <summary>The code's facts beyond the context's own: what the Step's inputs gave it. None by default.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="cancellationToken">Cancelled when the run ends before the Step does.</param>
    /// <returns>JSON text by code fact key, or the error that fails the Step.</returns>
    protected virtual ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Facts([]));

    /// <summary>The JSON tree of one of the Step's facts.</summary>
    /// <param name="context">The Step's context.</param>
    /// <param name="name">The fact's name without the 'fact:' prefix.</param>
    /// <returns>The tree, or null when the fact is absent or not JSON.</returns>
    protected static JsonNode? Fact(StepContext context, string name)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            return context.Facts.TryGetValue(FactKey.From(FactPrefix + name), out var value) ? JsonNode.Parse(Text(value)) : null;
        }
        catch (JsonException)
        {
            return null;   // boundary: a fact is text a model or a person wrote
        }
    }

    /// <summary>The 'contextQuery' fact the desk code takes: the text shape's suggested anchors.</summary>
    /// <param name="anchors">The anchors.</param>
    /// <returns>The fact's JSON text.</returns>
    protected static string ContextQuery(IEnumerable<string> anchors) => new JsonObject { ["suggestedAnchors"] = ScopeJson.ToArray(anchors) }.ToJsonString();

    /// <summary>The code's extra facts as a result.</summary>
    /// <param name="texts">JSON text by code fact key.</param>
    /// <returns>The result.</returns>
    protected static Result<IReadOnlyDictionary<string, string>> Facts(Dictionary<string, string> texts) => texts;

    /// <summary>The JSON text a fact or an input's typed result holds: the string the runner stored, or the element's own text.</summary>
    /// <param name="value">The element.</param>
    /// <returns>The text.</returns>
    protected static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();

    // An error of kind Unavailable (the lenses, the packet tool) is infrastructure: the engine retries the Step, then parks it as unavailable, never as a question for a person.
    private BlockStepResult Failed(Error error) => error.Kind == ErrorKind.Unavailable ? BlockStepResult.Unavailable(step.Value, error.Message, error.Code) : BlockStepResult.Failed(error.Message);

    private static BlockStepResult Completed(ScopingDeskResponse response, IReadOnlyDictionary<string, string> more)
    {
        var outputs = response.Outputs.Concat(more).ToDictionary(static o => FactKey.From(FactPrefix + o.Key), static o => Element(o.Value));
        return BlockStepResult.Completed(OutcomeName.From(response.Outcome), outputs);
    }

    // A fact is stored as its text, so a JSON string fact goes back as a string element holding its quoted text and everything else as its own value.
    private static JsonElement Element(string text)
    {
        using var parsed = JsonDocument.Parse(text);
        if (parsed.RootElement.ValueKind != JsonValueKind.String)
        {
            return parsed.RootElement.Clone();
        }

        using var quoted = JsonDocument.Parse(ScopeJson.Write(JsonValue.Create(text) ?? throw new InvalidOperationException("a string is always a value"))); // why: Create returns null only for a null string, and text is not null
        return quoted.RootElement.Clone();
    }

    private static Dictionary<string, string> Texts(StepContext context, IReadOnlyDictionary<string, string> extra)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in context.Facts.Where(static f => f.Key.Value.StartsWith(FactPrefix, StringComparison.Ordinal)))
        {
            texts[key.Value[FactPrefix.Length..]] = Text(value);
        }

        foreach (var (key, text) in extra)
        {
            texts[key] = text;
        }

        return texts;
    }
}
