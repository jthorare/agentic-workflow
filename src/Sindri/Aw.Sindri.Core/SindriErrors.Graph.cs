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

    /// <summary>SND-GRAPH-009: another workflow already has that slug.</summary>
    public static readonly ErrorDefinition GraphSlugTaken = new("SND-GRAPH-009", ErrorKind.Conflict, "Another workflow already has that slug");

    /// <summary>SND-GRAPH-010: a workflow with that id already exists.</summary>
    public static readonly ErrorDefinition GraphIdTaken = new("SND-GRAPH-010", ErrorKind.Conflict, "A workflow with that id already exists");

    /// <summary>SND-GRAPH-011: an archived workflow can't be activated.</summary>
    public static readonly ErrorDefinition GraphArchived = new("SND-GRAPH-011", ErrorKind.Conflict, "This workflow is archived; it can't be activated");
}
