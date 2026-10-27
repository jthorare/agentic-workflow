using Aw.Context.Core.Map;
using Aw.Context.Core.Roles;

namespace Aw.Context.Tests.Roles;

/// <summary>Issue 1105: the doctor row 'context:roles'.</summary>
public sealed class RoleHealthTests
{
    [Fact, Trait("Lane", "unit")]
    public void It_is_ok_when_no_repo_has_an_unassigned_file()
    {
        var check = RoleHealth.Check([new RepoRoles("web-app", [new RoleCount("demo", Demoted: true, 3)], Unassigned: null)]);

        Assert.Equal(("context:roles", MapHealthState.Ok), (check.Name, check.State));
        Assert.Null(check.Fix);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_ok_line_shows_how_many_files_an_ignored_role_dropped_and_how_many_a_demoted_one_holds()
    {
        var check = RoleHealth.Check([new RepoRoles("web-app", [new RoleCount("demo", Demoted: true, 3), new RoleCount("generated", Demoted: false, 0) { Ignored = 312 }], Unassigned: null)]);

        Assert.Equal(MapHealthState.Ok, check.State);
        Assert.Contains("web-app: demo 3, generated 312 dropped", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ignored_role_with_no_recorded_count_keeps_the_plain_line()
    {
        var check = RoleHealth.Check([new RepoRoles("web-app", [new RoleCount("generated", Demoted: false, 0)], Unassigned: null)]);

        Assert.Contains("web-app: generated 0", check.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void It_is_ok_with_nothing_to_count()
        => Assert.Equal(MapHealthState.Ok, RoleHealth.Check([]).State);

    [Fact, Trait("Lane", "unit")]
    public void It_warns_per_repo_with_the_count_and_an_example_and_the_fix()
    {
        var check = RoleHealth.Check([new RepoRoles("web-app", [], new UnassignedRoleFiles(8, ["src/__tests__/a.ts", "src/a.spec.ts"]))]);

        Assert.Equal(MapHealthState.Warn, check.State);
        Assert.Contains("web-app: 8 role-like file(s) match no role glob (src/__tests__/a.ts, src/a.spec.ts)", check.Detail, StringComparison.Ordinal);
        Assert.Equal("add a glob to index.demote or index.ignore in the profile", check.Fix);
    }
}
