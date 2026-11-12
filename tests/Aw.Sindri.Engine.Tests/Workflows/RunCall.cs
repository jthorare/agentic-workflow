using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>One call to the run store as the fault hook sees it: which method, for which run, and for 'SetStatus' the status it writes.</summary>
/// <param name="Op">The store method.</param>
/// <param name="Run">The run the call is about.</param>
/// <param name="Status">The status 'SetStatus' writes; null for any other call.</param>
internal readonly record struct RunCall(RunOp Op, WorkflowRunId Run, WorkflowRunStatus? Status);
