using System.Globalization;
using System.Text.Json;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Lenses;
using Aw.Context.Core.Map;
using Aw.Context.Core.ScopeGraph;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// 'input.context.graph' (issue 1005): the prefiltered graph the scoping agent Steps work from, read in process from the engine's scoping graph as the service, cut to the instance's repo, and remembered under the Step's workflow instance so the engine scopes that run's context reads to it.
/// The section 'graph' is a short readable line; the typed result is the <see cref="ScopeGraphView"/>. A graph the engine can't give (it builds none yet, or is down) is an honest section
/// saying so and no typed result: the gather then writes the graph as a projection of the lenses with a recorded limit. Unlike the lenses input it does not wait and try again.
/// </summary>
/// <param name="graph">The context engine (its facade for this read).</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="facts">Reads the content behind a fact-reference argument.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextGraphInput(IContextEngine graph, ContextInputScopes scopes, ContextInputFacts facts, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    private static readonly string[] _briefParts = ["subject", "text"];

    /// <summary>The block id.</summary>
    public const string Id = "input.context.graph";

    /// <summary>The section the text is under.</summary>
    public const string Section = "graph";

    /// <summary>The most nodes asked for when the recipe names none.</summary>
    internal const int DefaultMaxNodes = 400;

    /// <summary>The most bytes asked for when the recipe names none.</summary>
    internal const int DefaultMaxBytes = 300_000;

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [
            new ParameterSpec("anchors", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("depth", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("hops", ParameterType.List, Required: false, IsCapabilityField: false),
            new ParameterSpec("max-nodes", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("max-bytes", ParameterType.Number, Required: false, IsCapabilityField: false),
            new ParameterSpec("brief", ParameterType.Text, Required: false, IsCapabilityField: false),
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
            return ContextInputFacts.Fails(resolved.Error) ? resolved.Error : Unavailable(resolved.Error.Code + ": " + resolved.Error.Message, notes);
        }

        context = resolved.Value;
        var anchors = ContextInputArguments.Anchors(context, notes);
        if (anchors.Count == 0)
        {
            return Unavailable("this Step named no anchors", notes);
        }

        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message, notes);
        }

        var query = new ScopeGraphQuery(anchors, MapRead.Current, new LensRange(Number(context, "depth", notes), Hops(context)), Text(context, "brief"),
            Number(context, "max-nodes", notes) ?? DefaultMaxNodes, Number(context, "max-bytes", notes) ?? DefaultMaxBytes);
        var view = await graph.ScopeGraph(query, scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Unavailable(view.Error.Code + ": " + view.Error.Message, notes);
        }

        await Remember(context.Instance, view.Value, notes, cancellationToken).ConfigureAwait(false);
        var text = string.Create(CultureInfo.InvariantCulture, $"The scoping graph has {view.Value.Nodes.Count} nodes and {view.Value.Edges.Count} edges at map {view.Value.MapVersion}; {view.Value.Limits.Count} limits.\n");
        return ContextInputSections.Filled(Section, text, notes, view.Value, json);
    }

    // The instance's graph, kept so the engine scopes that run's context reads to it; no instance, nothing remembered. A failure is a note, the graph is still injected.
    private async ValueTask Remember(string? instance, ScopeGraphView view, List<string> notes, CancellationToken cancellationToken)
    {
        if (instance is null)
        {
            return;
        }

        var remembered = await graph.RememberScopeGraph(instance, view, cancellationToken).ConfigureAwait(false);
        if (remembered.IsError)
        {
            notes.Add($"the graph was not remembered for this run's context reads, so they are not scoped to it ({remembered.Error.Code}: {remembered.Error.Message})");
        }
    }

    private static InjectionSections Unavailable(string why, List<string> notes) => ContextInputSections.Unavailable(Section, "scoping graph", why, notes);

    private static int? Number(StepInputContext context, string name, List<string> notes)
    {
        if (!context.Arguments.TryGetValue(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n))
        {
            return n;
        }

        notes.Add($"{name} is not a whole number, so the default was used");
        return null;
    }

    // The brief as prose to rank by: text given as is, or a brief fact's subject and text ({ subject, text, refs }).
    private static string? Text(StepInputContext context, string name)
    {
        if (!context.Arguments.TryGetValue(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var parts = _briefParts.Select(p => value.TryGetProperty(p, out var part) && part.ValueKind == JsonValueKind.String ? part.GetString() : null).OfType<string>().Where(static s => s.Length > 0);
        return string.Join("\n\n", parts) is { Length: > 0 } prose ? prose : null;
    }

    private static string? Hops(StepInputContext context) =>
        context.Arguments.TryGetValue("hops", out var hops) && hops.ValueKind == JsonValueKind.Array
            ? string.Join(',', hops.EnumerateArray().Where(static h => h.ValueKind == JsonValueKind.String).Select(static h => h.GetString()))
            : null;
}
