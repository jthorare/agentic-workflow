using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Features.ShowNode;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.ShowNode;

/// <summary>Where a node's file is on the human's disk (#3715): inside the checkout of a repo in scope, else nothing.</summary>
public sealed class SourceLinksLocalPathTests
{
    private const string Repo = "aw-split-panes";
    private static readonly string _root = Path.GetFullPath("/work/aw-split-panes");

    private static string? Local(string repo, string path, ContextScope? scope = null) => SourceLinks.LocalPath(scope ?? MapWorld.Human, SourceTarget.OfFile(repo, path));

    [Fact, Trait("Lane", "unit")]
    public void A_file_in_the_checkout_is_the_checkout_joined_with_its_path() =>
        Assert.Equal(Path.Combine(_root, "src", "a.ts"), Local(Repo, "src/a.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_path_that_climbs_out_of_the_checkout_is_null()
    {
        Assert.Null(Local(Repo, "../x"));
        Assert.Null(Local(Repo, "src/../../x"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rooted_path_is_null() =>
        Assert.Null(Local(Repo, Path.GetFullPath("/etc/passwd")));

    [Fact, Trait("Lane", "unit")]
    public void A_repo_that_is_not_in_scope_is_null() =>
        Assert.Null(Local("not-in-scope", "src/a.ts"));

    [Fact, Trait("Lane", "unit")]
    public void A_session_never_learns_where_a_checkout_is() =>
        Assert.Null(Local(Repo, "src/a.ts", MapWorld.Session(Repo)));

    [Fact, Trait("Lane", "unit")]
    public void Climbing_out_and_back_in_with_other_letter_case_is_inside_only_when_case_is_ignored()
    {
        var target = SourceTarget.OfFile(Repo, "../AW-SPLIT-PANES/src/a.ts");

        Assert.Null(SourceLinks.LocalPath(MapWorld.Human, target, ignoreCase: false));
        Assert.Equal(Path.GetFullPath("/work/AW-SPLIT-PANES/src/a.ts"), SourceLinks.LocalPath(MapWorld.Human, target, ignoreCase: true));
    }
}
