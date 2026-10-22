using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// 'input.context.retrieve' (context spec Phase 10): one question ('q', a 'node' or a 'symbol', with optional 'depth' and 'budget') put to the context engine's query as the service, cut to the
/// instance's repo, on the current map. The section 'context' is the readable answer with its [Cn] markers and a sources list (the run receipt's citations); the typed result is the <see cref="Context.Core.Results.ContextResult"/>
/// as the route answers it, made stable first (no query id, no clock). The record text in it was fenced as untrusted by the engine and is handed over as it is. A missing question, an item with no repo, an unapproved profile or any
/// refusal from the engine is one honest section and no typed result: the Step runs without context, never fails for it.
/// </summary>
/// <param name="engine">The context engine.</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="facts">Reads the content behind a fact-reference argument.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextRetrieveInput(IContextEngine engine, ContextInputScopes scopes, ContextInputFacts facts, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.context.retrieve";

    /// <summary>The section the text is under.</summary>
    public const string Section = "context";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [
            new ParameterSpec("q", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("node", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("symbol", ParameterType.Text, Required: false, IsCapabilityField: false),
            new ParameterSpec("depth", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("budget", ParameterType.Number, Required: false, IsCapabilityField: false),
        ],
        Rung: null, DeskCapable: false);

    /// <inheritdoc/>
    public async ValueTask<Result<InjectionSections>> Fill(StepInputContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        var notes = new List<string>();
        var resolved = await facts.Resolve(context, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            // content that no longer matches its SHA-256 fails the fill (the Step must not run on other data); anything else is an honest section
            return ContextInputFacts.Fails(resolved.Error) ? resolved.Error : Unavailable(resolved.Error.Code + ": " + resolved.Error.Message, notes);
        }

        context = resolved.Value;
        var (text, node, symbol) = (Text(context, "q"), Text(context, "node"), Text(context, "symbol"));
        if (text is null && node is null && symbol is null)
        {
            return Unavailable("this Step asked no question (q, node or symbol)", notes);
        }

        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message, notes);
        }

        var query = new ContextQuery(text, node, Item: null, symbol, Whole(context, "depth", notes), Whole(context, "budget", notes));
        var answer = await engine.Query(query, scope.Value, cancellationToken).ConfigureAwait(false);
        if (answer.IsError)
        {
            return Unavailable(answer.Error.Code + ": " + answer.Error.Message, notes);
        }

        var stable = ContextInputSections.Stable(answer.Value);
        return ContextInputSections.Filled(Section, ContextRetrieveText.Render(stable), notes, stable, json);
    }

    private static InjectionSections Unavailable(string why, List<string> notes) => ContextInputSections.Unavailable(Section, "context", why, notes);

    private static string? Text(StepInputContext context, string name) =>
        context.Arguments.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text ? text : null;

    private static int? Whole(StepInputContext context, string name, List<string> notes)
    {
        if (!context.Arguments.TryGetValue(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        notes.Add(name + " is not a whole number, so the default was used");
        return null;
    }
}
