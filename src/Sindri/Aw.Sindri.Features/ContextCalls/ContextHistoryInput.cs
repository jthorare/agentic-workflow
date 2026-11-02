using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Facade;
using Aw.Context.Core.History;
using Aw.Context.Core.Map;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// 'input.context.history' (scoping spec C5, W6): the records, decisions and landed changes behind the anchors inside a window, read in process from the
/// engine's history lens as the service, cut to the instance's repo. The section 'history' is the readable text; the typed result is the
/// <see cref="HistoryView"/> as the route answers it, (no live sync marker: a fill is a function of the stored state). A refusal, or an instance with no mapped repo,
/// is an honest section and no typed result. The window is 'since'/'before' (RFC 3339 or a date) as an object, or a bare string meaning 'since'.
/// </summary>
/// <param name="history">The context engine (its facade for this lens).</param>
/// <param name="scopes">The service scope for the instance's repo.</param>
/// <param name="facts">Reads the content behind a fact-reference argument.</param>
/// <param name="json">The host's JSON options.</param>
internal sealed class ContextHistoryInput(IContextEngine history, ContextInputScopes scopes, ContextInputFacts facts, JsonSerializerOptions json) : IStepInput, IDescribedBlock
{
    /// <summary>The block id.</summary>
    public const string Id = "input.context.history";

    /// <summary>The section the text is under.</summary>
    public const string Section = "history";

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor { get; } = new(
        BlockId.From(Id), BlockKind.StepInput, Version: 1, Inputs: [], Outputs: [], Outcomes: [], Capability.Tools,
        [
            new ParameterSpec("anchors", ParameterType.Text, Required: true, IsCapabilityField: false),
            new ParameterSpec("window", ParameterType.Text, Required: false, IsCapabilityField: false),
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
            if (ContextInputFacts.Fails(resolved.Error))
            {
                return resolved.Error;
            }

            return Unavailable(resolved.Error.Code + ": " + resolved.Error.Message, notes);
        }

        context = resolved.Value;
        var anchors = ContextInputArguments.Anchors(context, notes);
        if (anchors.Count == 0)
        {
            return Unavailable("this Step named no anchors", notes);
        }

        return await Query(context, anchors, notes, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<InjectionSections>> Query(StepInputContext context, IReadOnlyList<string> anchors, List<string> notes, CancellationToken cancellationToken)
    {
        var window = Window(context, notes);
        if (window.IsError)
        {
            return Unavailable(window.Error.Code + ": " + window.Error.Message, notes);
        }

        var scope = await scopes.For(context.Repo, cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return Unavailable(scope.Error.Code + ": " + scope.Error.Message, notes);
        }

        var view = await history.History(anchors, window.Value, MapRead.Current, scope.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return Unavailable(view.Error.Code + ": " + view.Error.Message, notes);
        }

        var stable = ContextInputSections.Stable(view.Value);
        return ContextInputSections.Filled(Section, ContextHistoryText.History(stable, fence: false), notes, stable, json);
    }

    private static InjectionSections Unavailable(string why, List<string> notes) => ContextInputSections.Unavailable(Section, "context history", why, notes);

    private static Result<HistoryWindow> Window(StepInputContext context, List<string> notes)
    {
        if (!context.Arguments.TryGetValue("window", out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return HistoryWindow.Open;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return HistoryWindow.Of(value.GetString(), before: null);
        }

        return value.ValueKind == JsonValueKind.Object
            ? ObjectWindow(value, notes)
            : ContextErrors.QueryInvalid.With("the window argument must be {since?, before?} or a date text, not " + value.ValueKind.ToString().ToLowerInvariant());
    }

    private static Result<HistoryWindow> ObjectWindow(JsonElement window, List<string> notes)
    {
        string? since = null, before = null;
        foreach (var property in window.EnumerateObject())
        {
            if (property.Name is not ("since" or "before"))
            {
                notes.Add($"window key '{property.Name}' is not since or before and was ignored");
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                return ContextErrors.QueryInvalid.With($"the window's {property.Name} must be date text, not " + property.Value.ValueKind.ToString().ToLowerInvariant());
            }

            if (property.Name == "since")
            {
                since = property.Value.GetString();
            }
            else
            {
                before = property.Value.GetString();
            }
        }

        return HistoryWindow.Of(since, before);
    }
}
