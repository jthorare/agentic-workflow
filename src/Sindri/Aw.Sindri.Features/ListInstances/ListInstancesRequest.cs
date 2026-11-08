using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.ListInstances;

/// <summary>
/// 'workflow list [--needs-me | --item &lt;id&gt; | --state &lt;s&gt;]' (engine spec §6.3). Ended instances are listed too unless
/// 'open=true' (the CLI's default) drops them; '--query' adds a raw visibility query (for scripts).
/// </summary>
/// <param name="NeedsMe">'true' for what needs you, the Decisions and Tasks sections; 'false' for no such filter. A string so a bad value is the service's error, not the binder's.</param>
/// <param name="Item">Only this item's instances.</param>
/// <param name="State">Only instances in this state ('produce', 'parked', 'done').</param>
/// <param name="Recipe">Only instances of this recipe (the recipe id).</param>
/// <param name="Urgency">Only instances of recipes with this urgency.</param>
/// <param name="Subject">Only instances whose subject has this key, or whose subject label contains this text.</param>
/// <param name="Root">'true' for only top-level instances, 'false' for only children; a string for the same reason as <paramref name="NeedsMe"/>.</param>
/// <param name="Open">'true' to keep only the instances that are not finished (API spec §3.8 'open'); 'false' for only the finished ones, newest first (the finished-runs list of a recipe); a string for the same reason as <paramref name="NeedsMe"/>.</param>
/// <param name="Query">A visibility query on the search attributes, ANDed with the rest.</param>
/// <param name="Address">
/// A CLI address ('T-3/item/1') or bare item: the one instance it names (spec §6.3 'address'), whose id the CLI then
/// uses on the instance routes; SND-WF-001 when there is none. The other filters don't apply with it.
/// </param>
public sealed record ListInstancesRequest(string? NeedsMe = null, ItemKey? Item = null, string? State = null, string? Query = null, string? Address = null,
    string? Recipe = null, string? Urgency = null, string? Subject = null, string? Root = null, string? Open = null)
    : IQuery<InstanceList>;
