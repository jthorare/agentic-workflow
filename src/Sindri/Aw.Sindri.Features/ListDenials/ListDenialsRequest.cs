using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Features.ListDenials;

/// <summary>The denial counters of the sandboxed launches (GET /sindri/sandbox/denials; sandbox spec §3.6).</summary>
/// <param name="Launch">Only this launch, or null for every launch with a counter.</param>
/// <param name="Since">Only denials last seen at or after this time, or null for all.</param>
/// <param name="Item">Only the launches of this item, or null for every item.</param>
public sealed record ListDenialsRequest(LaunchId? Launch = null, DateTimeOffset? Since = null, ItemId? Item = null) : IQuery<ListDenialsResponse>;
