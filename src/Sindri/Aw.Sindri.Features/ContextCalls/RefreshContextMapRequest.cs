using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Keeps the map current with no human step (owner F1, 2026-10-19): a job-class command that builds the draft, lets the cleanup agent settle its review
/// queue and promotes it. The 'context-map-refresh' service starts it when a mapped repo's checkout moves along its default branch; a terminal or the app
/// may start it too; a session may not.
/// </summary>
public sealed record RefreshContextMapRequest : IJob<RefreshContextMapResult>;
