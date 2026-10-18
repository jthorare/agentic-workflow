namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>One owner hook a session would run, with its treatment inside the sandbox (see <see cref="HookInventory"/>).</summary>
/// <param name="Name">The hook's tag: the script's file name without '.sh', or the 'aw:' tag of an inline hook or a mod.</param>
/// <param name="Source">Where it comes from: 'config/hooks/&lt;name&gt;.sh', 'config/settings.json' (inline) or 'mods/&lt;name&gt;'.</param>
/// <param name="Events">The events (and matchers) it is installed on.</param>
/// <param name="AnswersPermissions">True when it prints a 'permissionDecision' of 'allow' (spike item 6).</param>
/// <param name="RewritesInput">True when it prints an 'updatedInput', changing the tool call before the rules are matched.</param>
/// <param name="Treatment">What the sandbox does with it.</param>
/// <param name="Why">The reason for the treatment, in the owner's words where there are some.</param>
/// <param name="RewritePrefix">For <see cref="HookTreatment.KeepDenyRewrites"/>: the prefix the hook puts in front of a command ('rtk ').</param>
public sealed record HookScript(
    string Name,
    string Source,
    string Events,
    bool AnswersPermissions,
    bool RewritesInput,
    HookTreatment Treatment,
    string Why,
    string? RewritePrefix = null);
