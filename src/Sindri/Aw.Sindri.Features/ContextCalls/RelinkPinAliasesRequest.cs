using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The pin alias relink (concept pins spec 3.4): a job-class command that links the stored records to the approved pins by the pins' aliases, on the current map, and touches no other link rule. The
/// 'context-map-refresh' service starts it when the pins' aliases or the current map version differ from the last relink that succeeded; it has no route and no verb of its own, and a session may not start it.
/// </summary>
public sealed record RelinkPinAliasesRequest : IJob<RelinkPinAliasesJobResult>;
