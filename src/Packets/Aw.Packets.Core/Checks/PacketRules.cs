namespace Aw.Packets.Core.Checks;

/// <summary>The packet rule ids (addendum §17.4 deterministic checks), in the order checks report them.</summary>
public static class PacketRules
{
    /// <summary>Task, workstream, question and example ids are unique.</summary>
    public const string UniqueIds = "PKT-ID-001";

    /// <summary>Every task is in a declared workstream.</summary>
    public const string TaskInWorkstream = "PKT-WS-001";

    /// <summary>Every dependency names a task in the packet.</summary>
    public const string KnownDependencies = "PKT-GRAPH-001";

    /// <summary>The task graph is acyclic.</summary>
    public const string Acyclic = "PKT-GRAPH-002";

    /// <summary>The base sha is a full commit sha that exists in the repository.</summary>
    public const string BaseCommit = "PKT-BASE-001";

    /// <summary>Every write scope exists at the base sha or is declared new.</summary>
    public const string ScopeExists = "PKT-SCOPE-001";

    /// <summary>Tasks that can run concurrently don't share a write scope unless a dependency orders them.</summary>
    public const string ScopeOverlap = "PKT-SCOPE-002";

    /// <summary>Every acceptance example the implementer sees has text.</summary>
    public const string AcceptanceText = "PKT-ACC-001";

    /// <summary>Every acceptance example maps to at least one evidence kind.</summary>
    public const string Evidence = "PKT-EVID-001";

    /// <summary>Every question is answered, or deferred with an owner and a default.</summary>
    public const string Decisions = "PKT-DEC-001";

    /// <summary>Every task with acceptance examples holds at least one out (addendum §17.4: a share held out, per task).</summary>
    public const string HoldoutExists = "PKT-HOLD-001";

    /// <summary>
    /// Held-out examples keep no text in the packet: their text lives in sindri-private state, mounted only into
    /// evaluator Steps and never committed (addendum §17.19). The store itself is checked once it exists.
    /// </summary>
    public const string HoldoutStored = "PKT-HOLD-002";

    /// <summary>
    /// A phase packet's holdout set hash is the one its seal names (line spec LN-PKT4, §9.1): the set the evaluator runs is the set the owner froze with the plan.
    /// Unchecked until a seal is given ('validate --kind phase --seal &lt;file&gt;').
    /// </summary>
    public const string HoldoutSealed = "PKT-HOLD-003";

    /// <summary>Every task fits its size class's budget (unchecked until the budgets are defined).</summary>
    public const string SizeBudget = "PKT-SIZE-001";

    /// <summary>The workflow passes its coverage checks (unchecked until workflow generation exists, addendum §17.7).</summary>
    public const string WorkflowCoverage = "PKT-FLOW-001";
}
