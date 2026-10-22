using Aw.Kernel;

namespace Aw.Sindri.Core.Workflow;

/// <summary>
/// The workflow engine's error catalogue (spec §8; docs/csharp/sindri.md), beside <see cref="SindriErrors"/> and
/// validated together with it. Retired codes stay reserved in <see cref="Retired"/> and are never reused.
/// </summary>
public static class WorkflowErrors
{
    /// <summary>SND-RECIPE-001: validation failed; fix each RCP line.</summary>
    public static readonly ErrorDefinition RecipeInvalid = new("SND-RECIPE-001", ErrorKind.Invalid, "The recipe failed validation");

    /// <summary>SND-RECIPE-002: no recipe with that id or hash.</summary>
    public static readonly ErrorDefinition RecipeNotFound = new("SND-RECIPE-002", ErrorKind.NotFound, "No recipe with that id or hash");

    /// <summary>SND-RECIPE-003: the recipe isn't approved.</summary>
    public static readonly ErrorDefinition RecipeNotApproved = new("SND-RECIPE-003", ErrorKind.Conflict, "The recipe is not approved");

    /// <summary>SND-RECIPE-005: the recipe was revoked.</summary>
    public static readonly ErrorDefinition RecipeRevoked = new("SND-RECIPE-005", ErrorKind.Conflict, "The recipe was revoked");

    /// <summary>SND-RECIPE-006: a block the approval pinned changed since.</summary>
    public static readonly ErrorDefinition RecipePinDrift = new("SND-RECIPE-006", ErrorKind.Conflict, "A pinned block changed since approval");

    /// <summary>SND-RECIPE-007: a recipe with the same id and version has other content.</summary>
    public static readonly ErrorDefinition RecipeVersionTaken = new("SND-RECIPE-007", ErrorKind.Conflict, "Same id and version, other content");

    /// <summary>SND-RECIPE-008: the recipe's evals are missing or failing.</summary>
    public static readonly ErrorDefinition RecipeEvalsFailing = new("SND-RECIPE-008", ErrorKind.Conflict, "The recipe's evals are missing or failing");

    /// <summary>SND-RECIPE-009: a recipe challenge expired, or its subject changed since the preview.</summary>
    public static readonly ErrorDefinition RecipeChallengeStale = new("SND-RECIPE-009", ErrorKind.Conflict, "The recipe challenge expired or its subject changed");

    /// <summary>SND-RECIPE-010: the recipe file can't be read.</summary>
    public static readonly ErrorDefinition RecipeUnreadable = new("SND-RECIPE-010", ErrorKind.NotFound, "The recipe file can't be read");

    /// <summary>SND-WF-001: no such instance.</summary>
    public static readonly ErrorDefinition InstanceNotFound = new("SND-WF-001", ErrorKind.NotFound, "No such instance");

    /// <summary>SND-WF-003: the trigger isn't allowed in the instance's state.</summary>
    public static readonly ErrorDefinition TriggerNotAllowed = new("SND-WF-003", ErrorKind.Conflict, "The trigger isn't allowed here");

    /// <summary>SND-WF-004: a gate refused the transition.</summary>
    public static readonly ErrorDefinition GateRefused = new("SND-WF-004", ErrorKind.Conflict, "A gate refused");

    /// <summary>SND-WF-005: a stale run token.</summary>
    public static readonly ErrorDefinition StaleRunToken = new("SND-WF-005", ErrorKind.Conflict, "A stale run token");

    /// <summary>SND-WF-006: the address matches more than one instance.</summary>
    public static readonly ErrorDefinition AmbiguousAddress = new("SND-WF-006", ErrorKind.Invalid, "An ambiguous address");

    /// <summary>SND-WF-008: a malformed desk report.</summary>
    public static readonly ErrorDefinition MalformedReport = new("SND-WF-008", ErrorKind.Invalid, "A malformed desk report");

    /// <summary>SND-WF-009: the command's nonce was used, expired or is for another version.</summary>
    public static readonly ErrorDefinition NonceRefused = new("SND-WF-009", ErrorKind.Conflict, "The command's nonce was used, expired or is for another version");

    /// <summary>SND-WF-010: no runtime for a named block or resource in this build.</summary>
    public static readonly ErrorDefinition NoRuntime = new("SND-WF-010", ErrorKind.Unavailable, "No runtime for a named block or resource");

    /// <summary>SND-WF-011: the effect kind isn't on the profile's effect allowlist.</summary>
    public static readonly ErrorDefinition EffectNotAllowed = new("SND-WF-011", ErrorKind.Forbidden, "The effect kind isn't on the profile's effect allowlist");

    /// <summary>SND-WF-012: an earlier attempt of this item is still live.</summary>
    public static readonly ErrorDefinition EarlierAttemptLive = new("SND-WF-012", ErrorKind.Conflict, "An earlier attempt of this item is still live");

    /// <summary>SND-WF-013: a workflow challenge expired, or its subject changed.</summary>
    public static readonly ErrorDefinition WorkflowChallengeStale = new("SND-WF-013", ErrorKind.Conflict, "The workflow challenge expired or its subject changed");

    /// <summary>SND-WF-014: '--item' and fact:item's id differ.</summary>
    public static readonly ErrorDefinition ItemMismatch = new("SND-WF-014", ErrorKind.Invalid, "--item and fact:item's id differ");

    /// <summary>SND-WF-015: an effect couldn't be sent: its block replied an outcome no event declares, or every retry failed.</summary>
    public static readonly ErrorDefinition EffectFailed = new("SND-WF-015", ErrorKind.Unavailable, "An effect couldn't be sent");

    /// <summary>SND-WF-016: a Step input has no runtime in this build (missing, or a descriptor only); raised at approve and at start.</summary>
    public static readonly ErrorDefinition StepInputNoRuntime = new("SND-WF-016", ErrorKind.Unavailable, "A Step input has no runtime in this build");

    /// <summary>SND-WF-017: the text a pull refilled differs from what the run recorded; nothing is served.</summary>
    public static readonly ErrorDefinition InjectionChanged = new("SND-WF-017", ErrorKind.Conflict, "The input's text changed since the run filled it");

    /// <summary>SND-WF-018: a Step input, re-run on a cache miss, filled differently from what the run recorded; the Step does not run on different data.</summary>
    public static readonly ErrorDefinition InputChanged = new("SND-WF-018", ErrorKind.Conflict, "The input changed since it was recorded");

    /// <summary>SND-WF-019: a filled plan is invalid: a bad section name, a section over 256 KiB, or a plan over 1 MiB.</summary>
    public static readonly ErrorDefinition InjectionInvalid = new("SND-WF-019", ErrorKind.Invalid, "A Step input filled an invalid plan");
    /// <summary>SND-BLOCK-001: a fact a block Step reads can't be read; the run ends as step.failed with the message as its reason.</summary>
    public static readonly ErrorDefinition BlockFactUnreadable = new("SND-BLOCK-001", ErrorKind.Unavailable, "A fact a block Step reads can't be read");

    /// <summary>SND-BLOCK-002: the approved profile a block Step's providers come from can't be read; the run ends as step.failed 'profile-unreadable: ...'.</summary>
    public static readonly ErrorDefinition BlockProfileUnreadable = new("SND-BLOCK-002", ErrorKind.Unavailable, "The approved profile can't be read for a block Step");

    /// <summary>SND-BLOCK-003: the item's repo couldn't be read, so a block Step's per-repo keys can't be resolved; the run ends as step.failed 'repo-unreadable'.</summary>
    public static readonly ErrorDefinition BlockRepoUnreadable = new("SND-BLOCK-003", ErrorKind.Unavailable, "The item's repo couldn't be read for a block Step");

    /// <summary>SND-BLOCK-004: a Step's run area file can't be written or read; the Step fails with the message, and a line is never dropped.</summary>
    public static readonly ErrorDefinition BlockRunAreaUnavailable = new("SND-BLOCK-004", ErrorKind.Unavailable, "A Step's run area file can't be written or read");

    /// <summary>SND-BLOCK-020: the phase has no hidden set in the holdout store (nothing was written or sealed for it); the Step fails with the message.</summary>
    public static readonly ErrorDefinition HoldoutSetMissing = new("SND-BLOCK-020", ErrorKind.NotFound, "The phase has no hidden set in the holdout store");

    /// <summary>SND-BLOCK-021: a file of the holdout store can't be read, parsed or written; the Step fails with the message, and the owner replaces the phase's hidden set.</summary>
    public static readonly ErrorDefinition HoldoutStoreUnavailable = new("SND-BLOCK-021", ErrorKind.Unavailable, "The holdout store can't be read or written");

    /// <summary>SND-RECIPE-011: a recipe is not one this build ships (its 'id@hash' is not in the embedded set); approve-all names it (setup-once spec §7.6).</summary>
    public static readonly ErrorDefinition RecipeNotShipped = new("SND-RECIPE-011", ErrorKind.Conflict, "The recipe is not one this build ships");

    /// <summary>Every live workflow error, for ErrorCatalogue.Validate (together with SindriErrors.All).</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        RecipeInvalid, RecipeNotFound, RecipeNotApproved, RecipeRevoked, RecipePinDrift, RecipeVersionTaken, RecipeEvalsFailing,
        RecipeChallengeStale, RecipeUnreadable, RecipeNotShipped,
        InstanceNotFound, TriggerNotAllowed, GateRefused, StaleRunToken, AmbiguousAddress, MalformedReport,
        NonceRefused, NoRuntime, EffectNotAllowed, EarlierAttemptLive, WorkflowChallengeStale, ItemMismatch, EffectFailed,
        StepInputNoRuntime, InjectionChanged, InputChanged, InjectionInvalid, BlockFactUnreadable, BlockProfileUnreadable, BlockRepoUnreadable, BlockRunAreaUnavailable,
        HoldoutSetMissing, HoldoutStoreUnavailable,
    ];

    /// <summary>
    /// Retired codes (spec §8), reserved so they are never reused: SND-RECIPE-004 (a caller without a terminal now
    /// gets the API's SND-AUTH-002), SND-WF-002 (rev 8: Updates serialize per workflow, so there is no version
    /// conflict) and SND-WF-007 (authorization is the API's SND-AUTH-002).
    /// </summary>
    public static IReadOnlyList<string> Retired { get; } = ["SND-RECIPE-004", "SND-WF-002", "SND-WF-007"];
}
