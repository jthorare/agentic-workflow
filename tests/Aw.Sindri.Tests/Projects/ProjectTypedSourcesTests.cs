using System.Text;
using Aw.Kernel;
using Aw.Kernel.Events;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Projects.Sources;
using Aw.Sindri.Core.Projects.TruthSources;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Tests.Projects;

/// <summary>#4499 PR 1: a project names branches, branch prefixes and work items as typed sources. The decisions validate them, the event and the views row carry them (an old row reads as none), the card shows them, and the truth counts a PR on that branch and an item by id.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectTypedSourcesTests
{
    private const string Item = "ITM-0123456789abcdef0123456789abcdef";
    private const string Other = "ITM-fedcba9876543210fedcba9876543210";
    private static readonly DateTimeOffset _now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly string[] _apiLink = ["https://github.com/acme/api"];
    private readonly CommandId _command = new SequentialIdGenerator().New<CommandId>();

    private static BranchSource Branch(string name, bool prefix = false, string repo = "acme/api") => new(repo, name, prefix);

    private Result<ProjectAttached> Attach(IReadOnlyList<BranchSource>? branches = null, IReadOnlyList<string>? items = null, IReadOnlyList<string>? links = null) =>
        ProjectDecisions.Attach(_command, ProjectRows.HandMadeId, new ProjectDraft(Linear: null, "Kitchen", State: null, links ?? _apiLink, Notes: null, Target: null, branches, items), [], led: false);

    private static void RefusedOn(string field, Error error)
    {
        Assert.Equal(SindriErrors.ProjectInvalid.Code, error.Code);
        Assert.StartsWith(field + ":", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_attaches_with_typed_branches_prefixes_and_items_in_its_event()
    {
        var attached = Attach([Branch("feat/login-form"), Branch("fix/", prefix: true)], [Item, Other]).Value;

        Assert.Equal([Branch("feat/login-form"), Branch("fix/", prefix: true)], attached.Branches);
        Assert.Equal([Item, Other], attached.Items);
    }

    [Theory]
    [InlineData("feat..x")]
    [InlineData("-lead")]
    [InlineData("has space")]
    [InlineData("end.lock")]
    [InlineData("trail/")]
    [InlineData("a//b")]
    [InlineData("tilde~1")]
    [InlineData("at@{brace")]
    [InlineData("")]
    public void A_branch_name_that_git_would_refuse_is_refused_naming_branches(string name) => RefusedOn("branches", Attach([Branch(name)]).Error);

    [Theory]
    [InlineData("feat ")]
    [InlineData("..")]
    [InlineData("a:b")]
    public void A_prefix_that_could_not_start_a_branch_name_is_refused(string prefix) => RefusedOn("branches", Attach([Branch(prefix, prefix: true)]).Error);

    [Fact]
    public void A_prefix_ending_in_a_slash_is_fine_because_it_starts_a_branch_name() => Assert.True(Attach([Branch("feat/", prefix: true)]).IsSuccess);

    [Fact]
    public void More_than_twenty_of_a_kind_is_refused()
    {
        var branches = Enumerable.Range(1, 21).Select(n => Branch("feat/" + n.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();
        var items = Enumerable.Range(1, 21).Select(static n => "ITM-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture)).ToList();

        RefusedOn("branches", Attach(branches).Error);
        RefusedOn("items", Attach(items: items).Error);
        Assert.True(Attach([.. branches.Take(20)], [.. items.Take(20)]).IsSuccess);
    }

    [Fact]
    public void A_typed_branch_for_a_repo_with_no_github_link_is_accepted() =>
        Assert.True(Attach([Branch("feat/x", repo: "acme/other")], links: []).IsSuccess);

    [Theory]
    [InlineData("acme")]
    [InlineData("acme/")]
    [InlineData("acme/a/b")]
    [InlineData("ac me/api")]
    public void A_repo_without_the_owner_name_shape_is_refused_naming_branches(string repo) => RefusedOn("branches", Attach([Branch("feat/x", repo: repo)]).Error);

    [Theory]
    [InlineData("itm-123")]
    [InlineData("ITM-xyz")]
    [InlineData("ITM-0123456789ABCDEF0123456789ABCDEF")]
    public void An_item_id_without_the_id_shape_is_refused_naming_items(string id) => RefusedOn("items", Attach(items: [id]).Error);

    [Fact]
    public void A_change_replaces_the_typed_lists_a_missing_list_keeps_them()
    {
        var row = ProjectRows.HandMade(version: 1) with { Links = [.. _apiLink], Branches = [Branch("feat/old")], Items = [Item] };

        var replaced = ProjectDecisions.Change(_command, ProjectRows.HandMadeId, row, new ProjectEdit(Branches: [Branch("feat/new")], Items: []), seenVersion: 1).Value;
        var kept = ProjectDecisions.Change(_command, ProjectRows.HandMadeId, row, new ProjectEdit(Notes: "later"), seenVersion: 1).Value;

        Assert.Equal([Branch("feat/new")], replaced.Branches);
        Assert.Empty(replaced.Items!);
        Assert.Null(kept.Branches);
        Assert.Null(kept.Items);
    }

    [Fact]
    public void The_view_row_folds_the_typed_lists_and_an_old_row_without_them_reads_as_none()
    {
        var recorded = Recorded();
        var attached = new ProjectAttached(_command, ProjectRows.HandMadeId, Linear: null, "Kitchen", State: null, _apiLink, Target: null, Branches: [Branch("feat/x")], Items: [Item]);

        var row = ProjectsProjection.Fold(attached, recorded);
        var kept = ProjectsProjection.Fold(row, Changed(notes: "n"), recorded);
        var replaced = ProjectsProjection.Fold(row, Changed(branches: [], items: [Other]), recorded);
        var old = AwJson.Deserialize<ProjectAttachedRow>(Encoding.UTF8.GetBytes("""{"id":"prj_01J9Z3K5M7Q2R4T6V8WXYZ0123","linear":null,"name":"Kitchen","state":null,"links":[],"notes":null,"target":null,"version":1,"attachedAt":"2026-11-01T10:00:00+00:00","finishedAt":null,"editedAt":"2026-11-01T10:00:00+00:00","sequence":3}"""), SliceHarness.Json).Value;

        Assert.Equal([Branch("feat/x")], row.Branches);
        Assert.Equal([Item], row.Items);
        Assert.Equal([Branch("feat/x")], kept.Branches);
        Assert.Equal([Item], kept.Items);
        Assert.Empty(replaced.Branches!);
        Assert.Equal([Other], replaced.Items);
        Assert.Null(old.Branches);
        Assert.Null(old.Items);
    }

    [Fact]
    public void The_card_shows_the_typed_lists_and_a_card_without_them_has_none()
    {
        var typed = ProjectRows.HandMade() with { Links = [.. _apiLink], Branches = [Branch("feat/x")], Items = [Item] };
        var plain = ProjectRows.HandMade() with { Id = ProjectId.From("prj_01J9Z3K5M7Q2R4T6V8WXYZ0199") };

        var page = ProjectCards.Build(new ProjectPageInputs([typed, plain], [], new ProjectFactsSnapshot(Owner: null, [], [], ["ENG"], FirstReadDone: true), Results.Success<IReadOnlyList<ProjectSourceFreshness>>([]), LinearConfigured: true), _now, all: false);

        var shown = page.Projects.Single(c => c.Key == typed.Id.Value);
        Assert.Equal([Branch("feat/x")], shown.Branches);
        Assert.Equal([Item], shown.Items);
        Assert.Null(page.Projects.Single(c => c.Key == plain.Id.Value).Branches);
    }

    private ProjectChanged Changed(string? notes = null, IReadOnlyList<BranchSource>? branches = null, IReadOnlyList<string>? items = null) =>
        new(_command, ProjectRows.HandMadeId, Version: 2, Name: null, State: null, ClearState: false, Links: null, Target: null, ClearTarget: false, Notes: notes, Branches: branches, Items: items);

    private static LedgerEvent Recorded() =>
        new(Sequence: 7, Epoch: 1, new EventEnvelope<string>(Guid.Empty, "project.attached", 1, _now, "human", "local", 0, Guid.Empty, CausationId: null, TraceId: null, "{}"), IdempotencyKey: "k", Item: null);

    private static ReviewPull Pull(int number, string? head, string repo = "acme/api") =>
        new(repo, number, "PR " + number.ToString(System.Globalization.CultureInfo.InvariantCulture), "sam", Draft: false, _now.AddDays(-5), _now.AddDays(-1), "abc", [], [], ReviewersRead: true, [], "https://github.com/" + repo + "/pull/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture), Mergeable: null, HeadRef: head);

    private static ProjectTruthInputs Inputs(ProjectSources sources, IReadOnlyList<ReviewPull>? pulls = null, IReadOnlyList<ProjectTruthItem>? items = null) =>
        new(sources, new ProjectTruthFacts(pulls ?? [], new Dictionary<string, ReviewSummary>(StringComparer.Ordinal), items ?? [], [], Owner: null), _now);

    [Fact]
    public void A_typed_branch_and_a_typed_prefix_count_the_pr_on_that_branch_and_nothing_else()
    {
        var sources = ProjectTruths.SourcesOf([], [], [], [Branch("feat/login-form"), Branch("fix/", prefix: true)], typedItems: null);
        ReviewPull[] pulls = [Pull(1, "feat/login-form"), Pull(2, "feat/login-form-2"), Pull(3, "fix/crash"), Pull(4, head: null), Pull(5, "feat/login-form", repo: "acme/web")];

        var truth = ProjectTruths.Build(Inputs(sources, pulls));

        Assert.Equal([1, 3], truth.OpenPrs.Select(static p => p.Number).Order());
    }

    [Fact]
    public void A_prefix_matches_the_whole_name_or_a_slash_boundary_and_not_a_longer_word()
    {
        var sources = ProjectTruths.SourcesOf([], [], [], [Branch("feat", prefix: true), Branch("fix/", prefix: true)], typedItems: null);
        ReviewPull[] pulls = [Pull(1, "feat"), Pull(2, "feat/x"), Pull(3, "feature-x"), Pull(4, "fix"), Pull(5, "fix/y"), Pull(6, "fixture")];

        var truth = ProjectTruths.Build(Inputs(sources, pulls));

        Assert.Equal([1, 2, 4, 5], truth.OpenPrs.Select(static p => p.Number).Order());
    }

    [Fact]
    public void The_same_branch_given_as_a_link_and_typed_counts_once()
    {
        var sources = ProjectTruths.SourcesOf(["https://github.com/acme/api/tree/feat/login-form"], [], [], [Branch("feat/login-form")], typedItems: null);

        var truth = ProjectTruths.Build(Inputs(sources, [Pull(1, "feat/login-form")]));

        Assert.Equal([Branch("feat/login-form")], sources.Branches);
        Assert.Equal([1], truth.OpenPrs.Select(static p => p.Number));
    }

    [Fact]
    public void A_typed_item_id_counts_the_item_with_no_link_and_an_unnamed_item_with_no_link_is_not_counted()
    {
        var sources = ProjectTruths.SourcesOf([], [], [], typedBranches: null, [Item]);
        ProjectTruthItem[] items = [new("acme/api", Done: false, [], ItemId.From(Item)), new("acme/api", Done: true, [], ItemId.From(Other)), new("acme/api", Done: false, ["https://github.com/acme/api/pull/9"])];

        var truth = ProjectTruths.Build(Inputs(sources, items: items));

        Assert.Equal((1, 0, 1), (truth.OpenItems, truth.DoneItems, truth.TotalItems));
    }
}
