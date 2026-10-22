using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Finishes a held map (code index spec 6.4): a job-class command the 'context-map-refresh' service starts as itself once the publish decision says a held draft goes ahead or is dropped.
/// It asks the decision again under its own grant, runs the cleanup pass once more and promotes. It has no route or verb of its own; a session may not start it.
/// </summary>
public sealed record PublishContextMapRequest : IJob<PublishContextMapResult>;
