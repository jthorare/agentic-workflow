using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.StartInstance;

/// <summary>
/// 'workflow start &lt;recipe&gt; --item &lt;id&gt; [--attempt next] [--fact &lt;key&gt;=&lt;file&gt;]…' (engine spec rev 8 §5.1,
/// §6.1, §6.3 'POST /workflow/instances'): starts the recipe on the item, or returns the instance that
/// '(recipe, item, attempt)' already has. Fact contents travel as bytes, at most 1 MiB each.
/// </summary>
/// <param name="Recipe">The recipe id ('workflow.step'), or 'id@hash' for that approved version.</param>
/// <param name="Item">The item: an ItemId or one of its tracker-key aliases.</param>
/// <param name="NextAttempt">'--attempt next': a new attempt after the latest, refused while that one is live.</param>
/// <param name="Facts">The input facts by key, each with the file name it came from and its content.</param>
public sealed record StartInstanceRequest(
    string Recipe,
    string Item,
    bool NextAttempt,
    IReadOnlyDictionary<FactKey, StartFact> Facts) : ICommand<StartInstanceResponse>
{
    /// <summary>The instance this start reruns (set by 'RerunInstance', never by a client: the wire body ignores it); null for an ordinary start.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public InstanceAddress? Again { get; init; }
}
