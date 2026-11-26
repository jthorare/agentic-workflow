using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Projects;
using Aw.Sindri.Ui.ViewModels.Projects;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>#4499 PR 3: the typed branches and items as chips on the project sheet, and as lines in the add form and the sheet's edit form with the CLI's rules (owner/name:branch, a trailing * for a prefix, at most 20 of each).</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectTypedSourcesTests
{
    private const string ItemA = "ITM-0123456789abcdef0123456789abcdef";
    private const string ItemB = "ITM-fedcba9876543210fedcba9876543210";
    private readonly ProjectSheetFixture _f = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static void Typed(JsonObject card)
    {
        card["branches"] = new JsonArray(
            new JsonObject { ["repo"] = "acme/api", ["name"] = "feat/login", ["prefix"] = false },
            new JsonObject { ["repo"] = "acme/api", ["name"] = "fix/", ["prefix"] = true });
        card["items"] = new JsonArray(ItemA);
    }

    private ProjectAddViewModel Add() => new(_f.Server, () => ProjectsFixture.Url);

    [Fact]
    public async Task The_sheet_shows_each_typed_source_as_its_own_chip_in_the_form_the_form_takes()
    {
        _f.Serve(ProjectSheetFixture.Attached, Typed);

        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Equal(["acme/api:feat/login", "acme/api:fix/*", ItemA], sheet.Sources);
    }

    [Fact]
    public async Task A_card_without_typed_sources_has_no_chips()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        Assert.Empty(sheet.Sources);
    }

    [Fact]
    public async Task The_edit_form_opens_with_the_typed_lines_and_an_untouched_form_sends_neither_list()
    {
        _f.Serve(ProjectSheetFixture.Attached, Typed);
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);

        sheet.BeginEdit();
        await sheet.Save(Token);

        Assert.Equal(("acme/api:feat/login\nacme/api:fix/*", ItemA), (sheet.EditBranches, sheet.EditItems));
        var change = Assert.IsType<ProjectChange>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal((null, null), (change.Branches, change.Items));
    }

    [Fact]
    public async Task An_edited_list_is_sent_whole_and_an_emptied_one_is_sent_empty()
    {
        _f.Serve(ProjectSheetFixture.Attached, Typed);
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        (sheet.EditBranches, sheet.EditItems) = ("acme/api:feat/login\n  acme/api:feat/*  \n", string.Empty);

        await sheet.Save(Token);

        var change = Assert.IsType<ProjectChange>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal([new ProjectBranchView("acme/api", "feat/login", Prefix: false), new ProjectBranchView("acme/api", "feat/", Prefix: true)], change.Branches);
        Assert.Empty(change.Items!);
    }

    [Theory]
    [InlineData("feat/login")]
    [InlineData("acme:feat/login")]
    [InlineData("acme/api:")]
    [InlineData(":feat/login")]
    [InlineData("acme/api:feat/*/x")]
    public async Task A_branch_line_that_is_not_owner_name_colon_branch_is_refused_without_a_call(string line)
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();
        sheet.EditBranches = line;

        await sheet.Save(Token);

        Assert.Empty(_f.Server.ProjectWrites);
        Assert.Contains("owner/name:branch", sheet.SaveError, StringComparison.Ordinal);
        Assert.True(sheet.IsEditing);
    }

    [Fact]
    public async Task An_item_that_is_not_an_item_id_or_more_than_twenty_of_a_kind_is_refused_without_a_call()
    {
        var sheet = await _f.Opened(ProjectSheetFixture.Attached, Token);
        sheet.BeginEdit();

        sheet.EditItems = "itm-1";
        await sheet.Save(Token);
        var badItem = sheet.SaveError;
        sheet.EditItems = string.Empty;
        sheet.EditBranches = string.Join('\n', Enumerable.Range(1, 21).Select(static n => "acme/api:feat/" + n.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        await sheet.Save(Token);

        Assert.Empty(_f.Server.ProjectWrites);
        Assert.Contains("ITM-", badItem, StringComparison.Ordinal);
        Assert.Contains("20", sheet.SaveError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_sends_the_typed_lines_as_lists_and_leaves_empty_ones_out()
    {
        var add = Add();
        (add.Name, add.Branches, add.Items) = ("Kitchen", "acme/api:feat/login\nacme/api:feat/*\n\n", ItemA + "\n" + ItemB);

        await add.Save(Token);

        var sent = Assert.IsType<ProjectAttach>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal([new ProjectBranchView("acme/api", "feat/login", Prefix: false), new ProjectBranchView("acme/api", "feat/", Prefix: true)], sent.Branches);
        Assert.Equal([ItemA, ItemB], sent.Items);
    }

    [Fact]
    public async Task Add_without_typed_lines_sends_neither_list()
    {
        var add = Add();
        add.Name = "Kitchen";

        await add.Save(Token);

        var sent = Assert.IsType<ProjectAttach>(Assert.Single(_f.Server.ProjectWrites).Body);
        Assert.Equal((null, null), (sent.Branches, sent.Items));
    }

    [Fact]
    public async Task Add_with_a_bad_line_is_refused_without_a_call_and_keeps_the_fields()
    {
        var add = Add();
        (add.Name, add.Branches) = ("Kitchen", "nonsense");

        await add.Save(Token);

        Assert.Empty(_f.Server.ProjectWrites);
        Assert.Contains("owner/name:branch", add.SaveError, StringComparison.Ordinal);
        Assert.Equal(("Kitchen", "nonsense"), (add.Name, add.Branches));
    }
}
