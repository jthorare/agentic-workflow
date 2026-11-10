using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>What the report's authority needs to know of a running 'run_agent' node (PR 7): the launch its session runs under, and whether a person took the node over.</summary>
/// <param name="Launch">The node run's launch.</param>
/// <param name="TakenOver">True while a person holds the node: only the human reports it.</param>
internal sealed record NodeReportView(LaunchId Launch, bool TakenOver);
