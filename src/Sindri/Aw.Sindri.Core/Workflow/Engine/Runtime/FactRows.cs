using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The fact rows of an instance (UI screens E-20, API spec §3.8): one per input and then one per output the recipe
/// declares, in declaration order. An input the instance holds, or an output it has recorded, is produced (with who and
/// when from the provenance); an output the open desk task still owes is pending, with what completing it needs (whether
/// it is required, the checks and the completing action); any other is pending. The in-progress status is evidence
/// progress, engine P7's.
/// </summary>
public static class FactRows
{
    /// <summary>The rows of <paramref name="instance"/>.</summary>
    /// <param name="instance">The interpreter's state.</param>
    /// <param name="interpreted">The recipe it runs and its input facts.</param>
    /// <param name="task">The open desk task, if any.</param>
    /// <param name="provenance">Who produced each fact and when.</param>
    /// <param name="contents">The stored text of the link inputs; a link's ref shows its URL, host and title from it.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<FactRow> Of(
        WorkflowInstance instance,
        InterpretedRecipe interpreted,
        DeskTask? task,
        IReadOnlyDictionary<FactKey, FactProvenance> provenance,
        IReadOnlyDictionary<FactKey, string>? contents = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(interpreted);
        ArgumentNullException.ThrowIfNull(provenance);
        var recipe = interpreted.Recipe;
        var held = new Held(instance, interpreted, task, provenance, contents);
        return
        [
            .. recipe.Inputs.Select(i => Row(new Declared(i.Key, i.Label, SubjectKinds.TypeOf(i), FactDirection.Input, From: null), held)),
            .. recipe.Outputs.Select(o => Row(new Declared(o.Key, o.Label, o.Type ?? Recipes.FactTypes.Default, FactDirection.Output, o.From), held)),
        ];
    }

    private static FactRow Row(Declared fact, Held held)
    {
        var produced = held.Instance.Facts.TryGetValue(fact.Key, out var value);
        var spec = produced ? null : held.Task?.Outputs.FirstOrDefault(o => o.Fact == fact.Key);
        var status = value is not null ? FactStatus.Produced : FactStatus.Pending;   // an output the desk task owes is pending, with what completing it needs (API spec §3.8)
        var stamp = produced ? held.Provenance.GetValueOrDefault(fact.Key) : null;
        var name = held.Interpreted.Inputs.TryGetValue(fact.Key, out var input) ? input.Name : null;
        return new FactRow(
            fact.Key.Value,
            fact.Label ?? SubjectRefs.Bare(fact.Key.Value),
            FactTypes.Parse(fact.Type),
            fact.Direction,
            fact.From,
            status,
            Summary(status, value, name),
            stamp?.At,
            stamp?.By,
            Ref(fact, value, name, held.Instance.Item, held.Contents?.GetValueOrDefault(fact.Key)),
            Progress: null,   // why: evidence progress is engine P7's
            Pending(spec, held, fact.Key));
    }

    /// <summary>What completing the open desk task needs for an output it still owes: whether it is required, the engine's check on it and the completing action.</summary>
    private static FactPending? Pending(OutputSpec? spec, Held held, FactKey key) =>
        spec is null || held.Task is not { } task
            ? null
            : new FactPending(StepOutputs.CallerWritesSomewhere(spec), DeskChecks.Applies(task, spec) ? [DeskChecks.Id(held.Interpreted, key)] : [], ActionId.From("CompleteStep"));

    /// <summary>The per-type handle (API spec §3.8): a file's name and hash, an item's id, nothing for text, and the fact key for the types a viewer fetches through the facts route.</summary>
    private static FactRef? Ref(Declared fact, FactValue? value, string? name, ItemKey item, string? content)
    {
        if (value is null)
        {
            return null;
        }

        return FactTypes.Parse(fact.Type) switch
        {
            FactType.Text => null,
            FactType.File => new FactRef(name, Size: null, StatusText.Prefixed(value.Sha256), Path: null, ItemId: null, Url: null, Host: null, Title: null, FactKey: null),
            FactType.Item => new FactRef(Name: null, Size: null, Hash: null, Path: null, item, Url: null, Host: null, Title: null, FactKey: null),
            FactType.Link when content is not null => LinkRef(fact.Key, content),
            FactType.Link or FactType.ScopeMap or FactType.Packet or FactType.Evidence or FactType.Decision or FactType.Payload or FactType.Findings or _ =>
                new FactRef(Name: null, Size: null, Hash: null, Path: null, ItemId: null, Url: null, Host: null, Title: null, fact.Key.Value),
        };
    }

    /// <summary>A link's handle: its URL, host and title, the same fields the subject's href and label come from, and the fact key.</summary>
    private static FactRef LinkRef(FactKey key, string content)
    {
        var (url, title) = SubjectRefs.LinkOf(content);
        return new FactRef(Name: null, Size: null, Hash: null, Path: null, ItemId: null, url.Length > 0 ? url : null, SubjectRefs.Host(url), title, key.Value);
    }

    private static string Summary(FactStatus status, FactValue? value, string? name) => status switch
    {
        FactStatus.Produced when value is not null => name ?? $"sha256:{StatusText.Digest(value.Sha256)}",
        FactStatus.Pending or FactStatus.InProgress or _ => "not produced yet",
    };

    private sealed record Declared(FactKey Key, string? Label, string? Type, FactDirection Direction, string? From);

    private sealed record Held(WorkflowInstance Instance, InterpretedRecipe Interpreted, DeskTask? Task, IReadOnlyDictionary<FactKey, FactProvenance> Provenance, IReadOnlyDictionary<FactKey, string>? Contents);
}
