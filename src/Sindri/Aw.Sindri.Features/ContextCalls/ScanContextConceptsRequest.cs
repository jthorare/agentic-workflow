using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// The concept scan (owner, q110): a job-class command that has a model (cheap for a small map, strong for a large one) read the newest map, propose product concepts and test them with the map search, and stores the result as concept hints beside that version.
/// The 'context-concept-scan' service starts it once for each new map version; it has no route and no verb of its own, and a session may not start it.
/// </summary>
public sealed record ScanContextConceptsRequest : IJob<ScanContextConceptsResult>;
