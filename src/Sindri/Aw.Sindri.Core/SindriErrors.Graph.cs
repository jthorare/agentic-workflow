using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-GRAPH-001: the text is not a workflow graph (bad JSON, an unknown member or a wrong type).</summary>
    public static readonly ErrorDefinition GraphInvalid = new("SND-GRAPH-001", ErrorKind.Invalid, "That is not a workflow graph");

    /// <summary>SND-GRAPH-002: the validator found problems; the message lists them.</summary>
    public static readonly ErrorDefinition GraphFindings = new("SND-GRAPH-002", ErrorKind.Invalid, "The workflow graph has problems");

    /// <summary>SND-GRAPH-003: a node names a block type nobody has.</summary>
    public static readonly ErrorDefinition GraphUnknownBlock = new("SND-GRAPH-003", ErrorKind.Invalid, "Unknown block type");

    /// <summary>SND-GRAPH-004: a reference names something that isn't upstream or doesn't exist.</summary>
    public static readonly ErrorDefinition GraphBadReference = new("SND-GRAPH-004", ErrorKind.Invalid, "A reference doesn't point at an upstream output");

    /// <summary>SND-GRAPH-005: a condition isn't in the condition language.</summary>
    public static readonly ErrorDefinition GraphBadExpression = new("SND-GRAPH-005", ErrorKind.Invalid, "A condition can't be read");

    /// <summary>SND-GRAPH-006: a cron or timezone can't be read.</summary>
    public static readonly ErrorDefinition GraphBadCron = new("SND-GRAPH-006", ErrorKind.Invalid, "A cron or timezone can't be read");

    /// <summary>SND-GRAPH-007: the save was made on a revision that is no longer the latest.</summary>
    public static readonly ErrorDefinition GraphStaleRevision = new("SND-GRAPH-007", ErrorKind.Conflict, "This workflow changed since you read it. Read it again and retry.");

    /// <summary>SND-GRAPH-008: no such workflow or revision.</summary>
    public static readonly ErrorDefinition GraphNotFound = new("SND-GRAPH-008", ErrorKind.NotFound, "No such workflow");

    /// <summary>SND-GRAPH-009: another workflow already has that short id.</summary>
    public static readonly ErrorDefinition GraphSlugTaken = new("SND-GRAPH-009", ErrorKind.Conflict, "Another workflow already has that short id");

    /// <summary>SND-GRAPH-010: a workflow with that id already exists.</summary>
    public static readonly ErrorDefinition GraphIdTaken = new("SND-GRAPH-010", ErrorKind.Conflict, "A workflow with that id already exists");

    /// <summary>SND-GRAPH-011: an archived workflow can't be activated.</summary>
    public static readonly ErrorDefinition GraphArchived = new("SND-GRAPH-011", ErrorKind.Conflict, "This workflow is archived; it can't be activated");

    /// <summary>SND-GRAPH-100: a run with that id exists (a re-delivered trigger or a catch-up overlap).</summary>
    public static readonly ErrorDefinition GraphRunIdTaken = new("SND-GRAPH-100", ErrorKind.Conflict, "A run with that id already exists");

    /// <summary>SND-GRAPH-101: no such run.</summary>
    public static readonly ErrorDefinition GraphRunNotFound = new("SND-GRAPH-101", ErrorKind.NotFound, "No such workflow run");

    /// <summary>SND-GRAPH-102: a block could not finish; the message says why.</summary>
    public static readonly ErrorDefinition GraphBlockFailed = new("SND-GRAPH-102", ErrorKind.Invalid, "A block could not finish");

    /// <summary>SND-GRAPH-105: the workflow has no active revision, so a trigger can't start it.</summary>
    public static readonly ErrorDefinition GraphNotActive = new("SND-GRAPH-105", ErrorKind.Conflict, "This workflow is not active; activate a revision to run it");

    /// <summary>SND-GRAPH-106: a manual run's payload is not a JSON object.</summary>
    public static readonly ErrorDefinition GraphBadPayload = new("SND-GRAPH-106", ErrorKind.Invalid, "A manual run's payload must be a JSON object");

    /// <summary>SND-GRAPH-107: the run already ended, so it can't be cancelled.</summary>
    public static readonly ErrorDefinition GraphRunEnded = new("SND-GRAPH-107", ErrorKind.Conflict, "This run already ended");

    /// <summary>SND-GRAPH-110: the run is not waiting on an agent at that node (the node is not a 'run_agent' node, has not started, or has ended), so there is nothing to take over.</summary>
    public static readonly ErrorDefinition GraphNodeNotWaiting = new("SND-GRAPH-110", ErrorKind.Conflict, "The run is not waiting on an agent at that node");

    /// <summary>SND-GRAPH-111: nobody holds the 'run_agent' node, so there is nothing to hand back.</summary>
    public static readonly ErrorDefinition GraphNodeNotTakenOver = new("SND-GRAPH-111", ErrorKind.Conflict, "That agent node is not taken over");

    /// <summary>SND-GRAPH-103: a prompt node's answer did not fit its 'output' schema.</summary>
    public static readonly ErrorDefinition GraphPromptAnswer = new("SND-GRAPH-103", ErrorKind.Invalid, "The model's answer does not fit the prompt's output schema");

    /// <summary>SND-GRAPH-113: a prompt step's field came out empty because a reference in it had no value (Workflows page spec §6.1).</summary>
    public static readonly ErrorDefinition GraphPromptFieldEmpty = new("SND-GRAPH-113", ErrorKind.Invalid, "A prompt field came out empty");

    /// <summary>SND-GRAPH-104: run controls (complete a Step, take over, hand back) are not available until the graph engine runs agents.</summary>
    public static readonly ErrorDefinition GraphRunControlsPending = new("SND-GRAPH-104", ErrorKind.Unavailable, "This run control returns with the graph engine's agent blocks");

    /// <summary>SND-GRAPH-108: a reference read the output of a node that the store cut at 64 KB.</summary>
    public static readonly ErrorDefinition GraphOutputTruncated = new("SND-GRAPH-108", ErrorKind.Invalid, "A referenced node output was cut at 64 KB");

    /// <summary>SND-GRAPH-109: a session that a run of this workflow launched tried to start the workflow again (a loop); the message names the run.</summary>
    public static readonly ErrorDefinition GraphRunLoop = new("SND-GRAPH-109", ErrorKind.Conflict, "A run of this workflow can't start the workflow again");

    /// <summary>SND-GRAPH-012: an edit op can't apply (an unknown node, an id already used, an edge that is missing or doubled); nothing was changed.</summary>
    public static readonly ErrorDefinition GraphOpInvalid = new("SND-GRAPH-012", ErrorKind.Invalid, "A graph edit could not be applied");

    /// <summary>SND-GRAPH-120: a workflow start would be a fourth call deep (or deeper than 'workflow.callDepth' allows).</summary>
    public static readonly ErrorDefinition GraphCallDepth = new("SND-GRAPH-120", ErrorKind.Invalid, "A workflow call is nested too deep");

    /// <summary>SND-GRAPH-121: a run asked to start its own workflow, directly or through the run that called it.</summary>
    public static readonly ErrorDefinition GraphSelfStart = new("SND-GRAPH-121", ErrorKind.Invalid, "A run can't start its own workflow");

    /// <summary>SND-GRAPH-122: an 'http' node's request was refused before it was sent: the host is not on 'workflow.http.hosts', the address is loopback, or the request carries a credential.</summary>
    public static readonly ErrorDefinition GraphHttpRefused = new("SND-GRAPH-122", ErrorKind.Invalid, "An http request was refused");

    /// <summary>SND-GRAPH-123: an 'http' node's request was sent but did not give an acceptable answer (a status outside 'expect', a redirect to another host, a body over 1 MB, a network failure).</summary>
    public static readonly ErrorDefinition GraphHttpFailed = new("SND-GRAPH-123", ErrorKind.Unavailable, "An http request failed");

    /// <summary>SND-GRAPH-130: no 'ask_owner' node of the run waits for an answer now.</summary>
    public static readonly ErrorDefinition GraphNotWaiting = new("SND-GRAPH-130", ErrorKind.Conflict, "No question waits at that node");

    /// <summary>SND-GRAPH-124: a 'workflow_result' node found no result: the run is not that workflow's, is not finished, or the workflow has no succeeded run.</summary>
    public static readonly ErrorDefinition GraphResultUnavailable = new("SND-GRAPH-124", ErrorKind.Invalid, "No finished run to read a result from");

    /// <summary>SND-GRAPH-112: a 'replace_node' op named a node that is not the trigger; only the trigger can be replaced (Workflows page spec decision 5).</summary>
    public static readonly ErrorDefinition GraphOnlyTriggerReplaceable = new("SND-GRAPH-112", ErrorKind.Invalid, "Only the trigger can be replaced.");

    /// <summary>SND-GRAPH-119: the contract exists, the behaviour does not yet (Workflows page P0 stubs: the test, pin, trigger-events and templates routes, and the replace_node op).</summary>
    public static readonly ErrorDefinition GraphNotBuilt = new("SND-GRAPH-119", ErrorKind.Unavailable, "That part of workflows is not built yet");
    /// <summary>SND-GRAPH-131: a node reads a field of a node that failed and continued (onFailure 'continue'); the reader fails instead of reading nothing.</summary>
    public static readonly ErrorDefinition GraphReferenceToContinuedFailure = new("SND-GRAPH-131", ErrorKind.Invalid, "A reference reads a node that failed and continued");
}
