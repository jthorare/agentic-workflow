using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>One trigger asking for a run.</summary>
/// <param name="Workflow">The workflow.</param>
/// <param name="Slug">Its slug (the run id's first part).</param>
/// <param name="Revision">The revision the run walks.</param>
/// <param name="Occurrence">The run's number among that revision's runs; a trigger that fires twice with one number is one run.</param>
/// <param name="Trigger">What started it, as JSON.</param>
/// <param name="Concurrency">The workflow's concurrency mode.</param>
/// <param name="Restored">For a re-run from a step (spec §7.3): the node runs copied from the source run, already addressed to the new run id. The starter stores them right after it admits the run and before the walk starts, so the walk skips those steps. Null for every other trigger.</param>
public sealed record GraphStartRequest(WorkflowId Workflow, WorkflowSlug Slug, int Revision, int Occurrence, string Trigger, GraphConcurrency Concurrency, IReadOnlyList<NodeRun>? Restored = null);
