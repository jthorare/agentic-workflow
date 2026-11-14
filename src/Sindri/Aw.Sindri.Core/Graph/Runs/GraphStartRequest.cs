using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>One trigger asking for a run.</summary>
/// <param name="Workflow">The workflow.</param>
/// <param name="Slug">Its slug (the run id's first part).</param>
/// <param name="Revision">The revision the run walks.</param>
/// <param name="Occurrence">The run's number among that revision's runs; a trigger that fires twice with one number is one run.</param>
/// <param name="Trigger">What started it, as JSON.</param>
/// <param name="Concurrency">The workflow's concurrency mode.</param>
/// <param name="Mode">Live, or a test run that records every write instead of making it.</param>
public sealed record GraphStartRequest(WorkflowId Workflow, WorkflowSlug Slug, int Revision, int Occurrence, string Trigger, GraphConcurrency Concurrency, string Mode = WorkflowRunMode.Live);
