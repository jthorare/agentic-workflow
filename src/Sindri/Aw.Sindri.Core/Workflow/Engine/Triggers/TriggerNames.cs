namespace Aw.Sindri.Core.Workflow.Engine.Triggers;

/// <summary>
/// The trigger names (spec §3.5, §5.4, §5.5): what a recipe's 'on' says for the recipe triggers, what an event's trigger
/// 'kind' records, and what an eval line's 'trigger.kind' says (§9.2).
/// </summary>
public static class TriggerNames
{
    /// <summary>A Step run reported an outcome.</summary>
    public const string StepCompleted = "step.completed";

    /// <summary>A Step run failed.</summary>
    public const string StepFailed = "step.failed";

    /// <summary>A wait's event, answer, resource or child arrived (a child's terminal outcome and 'cancelled' arrive as this).</summary>
    public const string WaitSatisfied = "wait.satisfied";

    /// <summary>The prefix of a recipe command: 'command.&lt;verb&gt;'.</summary>
    public const string CommandPrefix = "command.";

    /// <summary>'workflow answer' and its variants.</summary>
    public const string Answer = "answer";

    /// <summary>'workflow takeover'.</summary>
    public const string Takeover = "takeover";

    /// <summary>'workflow handback'.</summary>
    public const string Handback = "handback";

    /// <summary>'workflow cancel', the tracker, or a cancelled parent.</summary>
    public const string Cancel = "cancel";

    /// <summary>A state or retry timer fired.</summary>
    public const string Deadline = "deadline";

    /// <summary>A takeover's idle reminder fired.</summary>
    public const string TakeoverIdle = "takeoverIdle";

    /// <summary>The wake after a revoke.</summary>
    public const string RecipeRevoked = "recipe.revoked";

    /// <summary>The wake after a startup finds pins that no longer match the build.</summary>
    public const string RecipeDrifted = "recipe.drifted";

    /// <summary>A drift approval's resume of one parked instance (§4.1).</summary>
    public const string RecipeApproved = "recipe.approved";

    /// <summary>An effect's recheck refused one of the instance's effects (§5.7).</summary>
    public const string OutboxRefused = "outbox.refused";

    /// <summary>The child parked (with 'onParked: propagate' the parent mirrors it).</summary>
    public const string ChildParked = "child.parked";

    /// <summary>The child resumed from its park.</summary>
    public const string ChildResumed = "child.resumed";

    /// <summary>The child escalated (always propagates).</summary>
    public const string ChildEscalated = "child.escalated";

    /// <summary>UI spec E-6: an 'item.open-requested' that takes the instance over; the launcher service (API P10a) sends it through 'IInstanceRuntime.OpenRequest' and waits for its recorded result before a session opens.</summary>
    public const string OpenRequest = "open-request";
}
