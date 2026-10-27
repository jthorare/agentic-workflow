using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Sandbox.Plan;

/// <summary>The one function behind the rewriting hooks: every denied command is also denied in the form a kept rewriting hook turns it into.</summary>
public sealed class DenyRuleExpansionTests
{
    private static readonly PlanHooks _withRtk = HookPermissionPolicy.ForSession(["block-destructive", "rtk-rewrite"]);

    [Fact, Trait("Lane", "unit")]
    public void Every_denied_command_gets_its_rtk_form_when_rtk_rewrite_is_kept()
    {
        InnerRule[] rules =
        [
            new(InnerRuleEffect.Deny, "Bash", "git push:*"),
            new(InnerRuleEffect.Allow, "Read", string.Empty),
            new(InnerRuleEffect.Deny, "Bash", "gh:*"),
            new(InnerRuleEffect.Deny, "Bash", "env"),
            new(InnerRuleEffect.Deny, "Bash", "printenv*"),
            new(InnerRuleEffect.Deny, "Read", "/secret/**"),
        ];

        var expanded = DenyRuleExpansion.Expand(rules, _withRtk);

        Assert.Equal(
            [
                rules[0], new(InnerRuleEffect.Deny, "Bash", "rtk git push:*"), new(InnerRuleEffect.Deny, "Bash", "*git push*"), rules[1],
                rules[2], new(InnerRuleEffect.Deny, "Bash", "rtk gh:*"), rules[3], new(InnerRuleEffect.Deny, "Bash", "rtk env"),
                rules[4], new(InnerRuleEffect.Deny, "Bash", "rtk printenv*"), rules[5],
            ],
            expanded);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_rewritten_forms_when_no_rewriting_hook_is_installed()
    {
        InnerRule[] rules = [new(InnerRuleEffect.Deny, "Bash", "git push:*"), new(InnerRuleEffect.Deny, "Bash", "env")];

        Assert.Equal(rules, DenyRuleExpansion.Expand(rules, HookPermissionPolicy.ForSession(["block-destructive", "done-gate"])));
    }

    [Fact, Trait("Lane", "unit")]
    public void Allow_rules_and_non_command_denies_are_never_expanded()
    {
        InnerRule[] rules = [new(InnerRuleEffect.Allow, "Bash", "git status:*"), new(InnerRuleEffect.Deny, "Read", "/x/**")];

        Assert.Equal(rules, DenyRuleExpansion.Expand(rules, _withRtk));
    }
}
