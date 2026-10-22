using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Features.Line;

/// <summary>
/// 'effect.line.start-phase' (line spec LN-WF1, §8.2): starts <c language="text">workflow.phase</c> for one phase of the line as a linked instance at
/// '{line}/phase.{id}.{attempt}'. The effect's <c language="text">phase</c> argument is the line's <c language="text">fact:nextPhase</c>, <c language="text">{ phase, attempt, packet, base, stackParent }</c>:
/// the phase's frozen packet entry, the branch it builds on ('base': the default branch, or the head branch of the one phase it is stacked on)
/// and that phase's id. The linked instance gets <c language="text">fact:phasePacket</c> (the packet entry with its <c language="text">base</c> and <c language="text">stackParent</c>) and the line's
/// <c language="text">fact:lineContext</c>. It is idempotent per (line, phase, attempt): the address is the identity, so a retry or a re-emission finds the instance and
/// reports <see cref="EffectDone.Existed"/>. It replies <c language="text">line.phaseChanged</c> with <c language="text">started</c> and <c language="text">fact:phaseEvent</c> <c language="text">{ line, phase, attempt, status, instance, at }</c>.
/// </summary>
/// <param name="starts">Starts the linked instance.</param>
/// <param name="contents">Reads the <c language="text">phase</c> argument's fact.</param>
/// <param name="store">Stores the phase's packet fact and the reply's event fact.</param>
/// <param name="clock">When the phase started.</param>
internal sealed class StartPhaseEffect(IPhaseStarts starts, IFactContents contents, IFactStore store, TimeProvider clock) : IEffectKind, IDescribedBlock
{
    private static readonly FactKey _lineContext = FactKey.From("fact:lineContext");
    private static readonly FactKey _phasePacket = FactKey.From("fact:phasePacket");
    private static readonly FactKey _phaseEvent = FactKey.From("fact:phaseEvent");
    private static readonly EventKind _phaseChanged = EventKind.From("line.phaseChanged");
    private static readonly OutcomeName _started = OutcomeName.From("started");

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(RecipeChildren.StartPhase), BlockKind.Effect, Version: 1, Inputs: [_lineContext], Outputs: [new OutputSpec(_phaseEvent, [_started])], Outcomes: [_started],
        Capability.Effects,
        Parameters:
        [
            new ParameterSpec("recipe", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("phase", ParameterType.Text, Required: true, IsCapabilityField: false),
        ],
        Rung: null,
        DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<Result<EffectDone>> Dispatch(EffectContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var next = await NextPhase(context, cancellationToken).ConfigureAwait(false);
        if (next.IsError)
        {
            return next.Error;
        }

        var recipe = Text(context, "recipe");
        if (recipe is null || !RecipeId.TryFrom(recipe, out var recipeId))
        {
            return WorkflowErrors.NoRuntime.With("effect.line.start-phase needs a literal 'recipe' naming the recipe to start (fix: with: { recipe: workflow.phase })");
        }

        var inputs = await Inputs(context, next.Value, cancellationToken).ConfigureAwait(false);
        if (inputs.IsError)
        {
            return inputs.Error;
        }

        var started = await starts.Start(new PhaseStart(InstanceAddress.From(context.Instance), recipeId, next.Value.Phase, next.Value.Attempt, ItemKey.From(context.Item), inputs.Value), cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return started.Error;
        }

        var phaseEvent = new JsonObject
        {
            ["line"] = context.Instance,
            ["phase"] = next.Value.Phase,
            ["attempt"] = next.Value.Attempt,
            ["status"] = _started.Value,
            ["instance"] = started.Value.Address.Value,
            ["at"] = clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture),
        };
        var stored = await Put(phaseEvent, cancellationToken).ConfigureAwait(false);
        return stored.IsError
            ? stored.Error
            : new EffectDone(_started, started.Value.Existed, new Dictionary<FactKey, ReplyFact> { [_phaseEvent] = new ReplyFact(stored.Value.Ref, stored.Value.Sha256, HeadSha: null) }, _phaseChanged);
    }

    /// <summary>The line's <c language="text">fact:nextPhase</c>, read back from the stored fact the <c language="text">phase</c> argument names.</summary>
    private async ValueTask<Result<Next>> NextPhase(EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.Arguments.TryGetValue("phase", out var element) || Stored(element) is not { } fact)
        {
            return WorkflowErrors.NoRuntime.With("effect.line.start-phase needs 'phase', a stored fact (fix: with: { phase: { fact: fact:nextPhase } })");
        }

        var content = await contents.Read(fact, cancellationToken).ConfigureAwait(false);
        if (content.IsError)
        {
            return content.Error;
        }

        return Parse(content.Value);
    }

    private static Result<Next> Parse(ReadOnlyMemory<byte> content)
    {
        var node = Read(content);
        var id = LineJson.Str(node, "phase");
        var attempt = LineJson.Int(node, "attempt");
        if (node is not JsonObject obj || id is null || !IdShape(id) || attempt is not >= 1 || obj["packet"] is not JsonObject packet)
        {
            return WorkflowErrors.NoRuntime.With("fact:nextPhase must be { phase, attempt, packet, base, stackParent } with a lowercase phase id and an attempt from 1");
        }

        return new Next(id, attempt.Value, (JsonObject)packet.DeepClone(), obj["base"]?.DeepClone(), obj["stackParent"]?.DeepClone());
    }

    private static JsonNode? Read(ReadOnlyMemory<byte> content)
    {
        try
        {
            return JsonNode.Parse(content.Span);
        }
        catch (JsonException)
        {
            return null;   // boundary: the fact is not JSON; Parse names it
        }
    }

    private static bool IdShape(string id) => id.Length is > 0 and <= 64 && id.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-') && id[0] != '-';

    /// <summary>The linked instance's inputs: the phase's packet fact, and the line's own <c language="text">fact:lineContext</c> when it has one.</summary>
    private async ValueTask<Result<Dictionary<FactKey, FactValue>>> Inputs(EffectContext context, Next next, CancellationToken cancellationToken)
    {
        var packet = (JsonObject)next.Packet.DeepClone();
        packet["base"] = next.Base?.DeepClone();
        packet["stackParent"] = next.StackParent?.DeepClone();
        var stored = await Put(packet, cancellationToken).ConfigureAwait(false);
        if (stored.IsError)
        {
            return stored.Error;
        }

        var inputs = new Dictionary<FactKey, FactValue> { [_phasePacket] = stored.Value };
        if (context.Facts.TryGetValue(_lineContext, out var line) && Stored(line) is { } lineContext)
        {
            inputs[_lineContext] = lineContext;
        }

        return inputs;
    }

    private async ValueTask<Result<FactValue>> Put(JsonNode node, CancellationToken cancellationToken) =>
        await store.Put(Encoding.UTF8.GetBytes(node.ToJsonString()), cancellationToken).ConfigureAwait(false);

    private static string? Text(EffectContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static FactValue? Stored(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("ref", out var reference) && reference.ValueKind == JsonValueKind.String
        && element.TryGetProperty("sha256", out var sha) && sha.ValueKind == JsonValueKind.String
        && reference.GetString() is { } refText && sha.GetString() is { } shaText
            ? new FactValue(refText, shaText, HeadSha: null)
            : null;

    private sealed record Next(string Phase, int Attempt, JsonObject Packet, JsonNode? Base, JsonNode? StackParent);
}
