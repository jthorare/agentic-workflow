using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Pages;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'sindri context page list': what it prints for a page and its actions, for an empty list, and the exit codes of the two page errors.</summary>
public sealed class ContextPageVerbsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_list_prints_each_page_with_its_marks_and_code_then_its_actions_with_their_concepts()
    {
        var text = ContextPageVerbs.Text(PagesEngineProxy.OneOwnerPage(), suggested: false);

        Assert.Equal(
            "Schedule week\n  surface:route/GET:/schedule/week  ·  Set by a person  ·  Code found\n    Submit schedule  (Set by a person, No code found yet)  ->  scheduling\n",
            text.ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_suggested_page_says_suggested_and_a_truncated_list_says_how_many_are_shown()
    {
        var view = new PagesView([new PageRow(PageId.From("settings"), NodeKey.From("surface:route/GET:/settings"), "Settings", Repo: null, "suggested", Code: false, [])], Total: 9, Truncated: true, "v13");

        var text = ContextPageVerbs.Text(view, suggested: true);

        Assert.Contains("Suggested  ·  No code found yet", text, StringComparison.Ordinal);
        Assert.Contains("Showing 1 of 9 pages.", text, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(false, "No pages named yet. Name one in the app, or see sindri context page list --suggested.\n")]
    [InlineData(true, "No suggested pages found.\n")]
    public void An_empty_list_says_what_to_do(bool suggested, string expected) =>
        Assert.Equal(expected, ContextPageVerbs.Text(new PagesView([], Total: 0, Truncated: false, "v13"), suggested));

    [Fact, Trait("Lane", "unit")]
    public void Both_page_errors_are_attention_and_exit_1()
    {
        Assert.Equal(1, ContextExit.For(ContextErrors.PageNotFound.With("no page is named 'x'"), toolExit: 2));
        Assert.Equal(1, ContextExit.For(ContextErrors.PageQueryUnknown.With("context_pages: no concept 'x'"), toolExit: 2));
    }

    [Fact, Trait("Lane", "unit")]
    public void Owner_pages_are_the_default_and_the_flags_make_the_query()
    {
        var plain = ContextPageVerbs.Parse(["list"]).Value;
        var all = ContextPageVerbs.Parse(["list", "--suggested", "--concept", "scheduling", "--repo", "app", "--json"]).Value;

        Assert.Equal("?marks=owner", plain.Query);
        Assert.Equal(("?marks=suggested&concept=scheduling&repo=app", true), (all.Query, all.Json));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("set")]
    [InlineData("list --nope")]
    [InlineData("list --concept")]
    [InlineData("list extra")]
    public void Anything_else_is_006(string args) =>
        Assert.Equal("SND-CONTEXT-006", ContextPageVerbs.Parse(args.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Error.Code);
}
