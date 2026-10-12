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

    /// <summary>SND-WF-002: the instance changed (a stale version).</summary>
    public static readonly ErrorDefinition InstanceChanged = new("SND-WF-002", ErrorKind.Conflict, "The instance changed");

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

    /// <summary>Every live workflow error, for ErrorCatalogue.Validate (together with SindriErrors.All).</summary>
    public static IReadOnlyList<ErrorDefinition> All { get; } =
    [
        RecipeInvalid, RecipeNotFound, RecipeNotApproved, RecipeRevoked, RecipePinDrift, RecipeVersionTaken, RecipeEvalsFailing,
        RecipeChallengeStale, RecipeUnreadable,
        InstanceNotFound, InstanceChanged, TriggerNotAllowed, GateRefused, StaleRunToken, AmbiguousAddress, MalformedReport,
        NonceRefused, NoRuntime, EffectNotAllowed, EarlierAttemptLive, WorkflowChallengeStale, ItemMismatch,
    ];

    /// <summary>
    /// Retired codes (spec §8), reserved so they are never reused: SND-RECIPE-004 (a caller without a terminal now
    /// gets the API's SND-AUTH-002) and SND-WF-007 (authorization is the API's SND-AUTH-002).
    /// </summary>
    public static IReadOnlyList<string> Retired { get; } = ["SND-RECIPE-004", "SND-WF-007"];
}
