using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Facts;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The pure Projects page build (projects page spec §3, §3.1, §6.2): cards, overrides, folds, order, sentences and the freshness line.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectCardsTests
{
    private const string KeyA = "linear:project/aaaa";
    private const string KeyB = "linear:project/bbbb";
    private static readonly DateTimeOffset _now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProjectPerson _owner = new("linear:user/me", "Joi");

    private static LinearProjectFact Fact(string key = KeyA, string title = "Alpha", string? state = "started", bool led = true, DateTimeOffset? lastActivity = null, DateTimeOffset? closed = null) =>
        new(key, title, "desc", "https://linear.app/ws/project/" + title, state, "onTrack", new DateTimeOffset(2026, 11, 14, 0, 0, 0, TimeSpan.Zero),
            led ? _owner : new ProjectPerson("linear:user/other", "Sam"), led, _now.AddDays(-30), closed, OpenIssues: 7, Counting: false, lastActivity);

    private static ProjectAttachedRow Row(ProjectId? id = null, string? linear = KeyA, string? name = null, ProjectState? state = null, IReadOnlyList<string>? links = null, DateTimeOffset? finished = null, long version = 3) =>
        new(id ?? ProjectRows.LinearId, linear, name, state, links ?? [], "my note", Target: null, version, _now.AddDays(-60), finished, _now.AddDays(-2), Sequence: 5);

    private static ProjectFactsSnapshot Snapshot(IReadOnlyList<LinearProjectFact> projects, bool firstRead = true, ProjectPerson? owner = null) =>
        new(owner ?? _owner, projects, projects, ["ENG"], firstRead);

    private static Result<IReadOnlyList<ProjectSourceFreshness>> Synced(params ProjectSourceFreshness[] rows) => rows;

    private static ProjectsPage Page(
        IReadOnlyList<LinearProjectFact>? facts = null,
        IReadOnlyList<ProjectAttachedRow>? attached = null,
        IReadOnlyList<ProjectHiddenRow>? hidden = null,
        bool all = false,
        bool configured = true,
        bool firstRead = true,
        ProjectPerson? owner = null,
        Result<ProjectFactsSnapshot>? factsResult = null,
        Result<IReadOnlyList<ProjectSourceFreshness>>? freshness = null) =>
        ProjectCards.Build(
            new ProjectPageInputs(attached ?? [], hidden ?? [], factsResult ?? Snapshot(facts ?? [], firstRead, owner ?? _owner), freshness ?? Synced(), configured),
            _now,
            all);

    [Fact]
    public void Build_makes_a_led_card_with_the_Linear_key_and_hide_version_zero()
    {
        var card = Page([Fact()]).Projects.Single();

        Expect.Equal((KeyA, ProjectOrigin.Led, "Alpha", ProjectState.InProgress, ProjectHealth.OnTrack, 7, 0L, true),
            (card.Key, card.Origin, card.Name, card.State, card.Health, card.OpenIssues, card.Version, card.Lead!.You));
        Expect.Equal(new DateOnly(2026, 11, 14), card.Target);
        Expect.Equal("Linear", card.Links.Single().Label);
    }

    [Fact]
    public void Build_moves_a_hidden_led_project_to_Hidden_with_the_hide_version_even_when_finished()
    {
        var page = Page([Fact(state: "completed", closed: _now.AddDays(-1))], hidden: [new ProjectHiddenRow(KeyA, Hidden: true, Version: 4, Sequence: 9)]);

        Expect.Equal((0, 0, 1), (page.Projects.Count, page.Finished.Count, page.Hidden.Count));
        Expect.Equal(4L, page.Hidden.Single().Version);
    }

    [Fact]
    public void Build_shows_an_unhidden_led_project_again_with_its_hide_version()
    {
        var page = Page([Fact()], hidden: [new ProjectHiddenRow(KeyA, Hidden: false, Version: 2, Sequence: 9)]);

        Expect.Equal(2L, page.Projects.Single().Version);
    }

    [Fact]
    public void Build_applies_the_owners_state_over_Linears_on_an_attached_project()
    {
        var row = Row(state: ProjectState.Paused, links: ["https://example.com/plan"]);
        var card = Page([Fact(led: false)], [row]).Projects.Single();

        Expect.Equal(("prj_01J9Z3K5M7Q2R4T6V8WXYZ0124", ProjectOrigin.Attached, "Alpha", ProjectState.Paused, true, 3L, "my note"),
            (card.Key, card.Origin, card.Name, card.State, card.StateSetByOwner, card.Version, card.Note));
        Expect.Sequence(["Linear", "https://example.com/plan"], card.Links.Select(static l => l.Label));
        Expect.Equal(("Sam", false), (card.Lead!.Name, card.Lead.You));
    }

    [Fact]
    public void Build_takes_Linears_state_on_an_attached_project_without_an_override()
    {
        var card = Page([Fact(led: false, state: "planned")], [Row()]).Projects.Single();

        Expect.Equal((ProjectState.Planned, false), (card.State, card.StateSetByOwner));
    }

    [Fact]
    public void Build_marks_an_attached_project_the_store_no_longer_holds_as_not_synced()
    {
        var withName = Page([], [Row(name: "Old name")]).Projects.Single();
        var withoutName = Page([], [Row()]).Projects.Single();

        Expect.Equal((false, "Old name", null), (withName.Synced, withName.Name, withName.OpenIssues));
        Expect.Equal(KeyA, withoutName.Name);
    }

    [Fact]
    public void Build_makes_a_hand_made_card_from_its_row()
    {
        var row = Row(ProjectRows.HandMadeId, linear: null, name: "Kitchen", state: ProjectState.Planned, links: ["https://example.com/k"]);
        var card = Page(attached: [row]).Projects.Single();

        Expect.Equal((ProjectOrigin.HandMade, "Kitchen", ProjectState.Planned, row.EditedAt, null, null, null), (card.Origin, card.Name, card.State, card.LastActivity, card.OpenIssues, card.Lead, card.Health));
        Expect.Equal(new ProjectLink("https://example.com/k", "https://example.com/k"), card.Links.Single());
    }

    [Fact]
    public void Build_keeps_a_project_finished_3_days_ago_and_drops_one_finished_20_days_ago()
    {
        var page = Page([Fact(KeyA, "Recent", "completed", closed: _now.AddDays(-3)), Fact(KeyB, "Old", "canceled", closed: _now.AddDays(-20))]);

        Expect.Equal(0, page.Projects.Count);
        Expect.Sequence(["Recent"], page.Finished.Select(static c => c.Name));
    }

    [Fact]
    public void Build_keeps_old_finished_projects_with_all_newest_first()
    {
        var page = Page([Fact(KeyA, "Old", "canceled", closed: _now.AddDays(-20)), Fact(KeyB, "Recent", "completed", closed: _now.AddDays(-3))], all: true);

        Expect.Sequence(["Recent", "Old"], page.Finished.Select(static c => c.Name));
    }

    [Fact]
    public void Build_keeps_a_finished_project_that_has_no_finish_date()
    {
        var page = Page(attached: [Row(ProjectRows.HandMadeId, linear: null, name: "Done thing", state: ProjectState.Done)]);

        Expect.Equal(1, page.Finished.Count);
    }

    [Fact]
    public void Build_orders_by_state_group_then_newest_activity_then_name()
    {
        var facts = new[]
        {
            Fact("linear:project/1", "Backlogged", "backlog", lastActivity: _now),
            Fact("linear:project/2", "Paused one", "paused", lastActivity: _now),
            Fact("linear:project/3", "Planned one", "planned", lastActivity: _now),
            Fact("linear:project/4", "Stateless", state: null, lastActivity: _now),
            Fact("linear:project/5", "Old active", "started", lastActivity: _now.AddDays(-5)),
            Fact("linear:project/6", "New active", "started", lastActivity: _now.AddDays(-1)),
            Fact("linear:project/7", "No activity b", "started"),
            Fact("linear:project/8", "No activity a", "started"),
        };

        Expect.Sequence(
            ["New active", "Old active", "No activity a", "No activity b", "Planned one", "Paused one", "Backlogged", "Stateless"],
            Page(facts).Projects.Select(static c => c.Name));
    }

    [Fact]
    public void Build_orders_Hidden_by_name()
    {
        var page = Page([Fact(KeyB, "Zed"), Fact(KeyA, "Abe")], hidden: [new ProjectHiddenRow(KeyA, Hidden: true, Version: 1, Sequence: 1), new ProjectHiddenRow(KeyB, Hidden: true, Version: 1, Sequence: 2)]);

        Expect.Sequence(["Abe", "Zed"], page.Hidden.Select(static c => c.Name));
    }

    [Theory]
    [InlineData("backlog", ProjectState.Backlog)]
    [InlineData("planned", ProjectState.Planned)]
    [InlineData("unstarted", ProjectState.Planned)]
    [InlineData("started", ProjectState.InProgress)]
    [InlineData("paused", ProjectState.Paused)]
    [InlineData("completed", ProjectState.Done)]
    [InlineData("canceled", ProjectState.Canceled)]
    [InlineData("cancelled", ProjectState.Canceled)]
    [InlineData("mystery", null)]
    [InlineData(null, null)]
    public void Build_maps_Linears_status_type_to_the_six_states(string? raw, ProjectState? expected)
    {
        var page = Page([Fact(state: raw, closed: _now)], all: true);

        Expect.Equal(expected, page.Projects.Concat(page.Finished).Single().State);
    }

    [Theory]
    [InlineData("onTrack", ProjectHealth.OnTrack)]
    [InlineData("atRisk", ProjectHealth.AtRisk)]
    [InlineData("offTrack", ProjectHealth.OffTrack)]
    [InlineData("other", null)]
    public void Build_maps_Linears_health(string raw, ProjectHealth? expected)
    {
        var fact = Fact() with { Health = raw };

        Expect.Equal(expected, Page([fact]).Projects.Single().Health);
    }

    [Fact]
    public void Build_uses_Linears_closed_date_as_finished_only_for_Done_and_Canceled()
    {
        var page = Page([Fact(KeyA, "Open", "started", closed: _now), Fact(KeyB, "Shut", "completed", closed: _now)]);

        Expect.True(page.Projects.Single().FinishedAt is null);
        Expect.Equal(_now, page.Finished.Single().FinishedAt);
    }

    [Fact]
    public void Build_without_Context_shows_attached_and_hand_made_cards_by_name_only_and_drops_led_ones()
    {
        var error = SindriErrors.ProjectFactsUnavailable.With("context is down\nsecond line");
        var page = Page(
            attached: [Row(name: "Named"), Row(ProjectRows.HandMadeId, linear: null, name: "Kitchen", state: ProjectState.Planned)],
            factsResult: error);

        Expect.Equal("Linear and GitHub data unavailable: context is down", page.Empty);
        Expect.True(page.Viewer is null);
        Expect.Equal(2, page.Projects.Count);
        var linear = page.Projects.Single(static c => c.Origin == ProjectOrigin.Attached);
        Expect.Equal(("Named", true, null), (linear.Name, linear.Synced, linear.OpenIssues));
        Expect.Equal(new ProjectFreshnessRow("context", ProjectFreshnessState.Failing, "context is down", At: null), page.Freshness.Single(static r => r.Source == "context"));
    }

    [Fact]
    public void Build_says_to_connect_Linear_when_it_is_not_configured_and_nothing_is_attached()
        => Expect.Equal("No projects yet. Connect Linear or add one by hand.", Page(configured: false).Empty);

    [Fact]
    public void Build_says_Linear_is_still_syncing_before_the_first_read()
        => Expect.Equal("Linear is still syncing. Projects you lead show up when it's done.", Page(firstRead: false).Empty);

    [Fact]
    public void Build_names_the_owner_when_they_lead_nothing()
    {
        Expect.Equal("You don't lead any Linear projects as Joi. Attach one you drive, or add one by hand.", Page().Empty);
        Expect.Equal("You don't lead any Linear projects. Attach one you drive, or add one by hand.", Page(owner: new ProjectPerson("linear:user/me", Name: null)).Empty);
    }

    [Fact]
    public void Build_has_no_sentence_when_there_are_cards_and_names_the_viewer()
    {
        var page = Page([Fact()]);

        Expect.Equal((null, "Joi"), (page.Empty, page.Viewer));
    }

    [Fact]
    public void Build_has_no_sentence_for_a_page_with_only_an_attached_project()
        => Expect.True(Page(attached: [Row(ProjectRows.HandMadeId, linear: null, name: "Kitchen")], configured: false).Empty is null);

    [Fact]
    public void Build_writes_the_freshness_line_in_source_order_with_minutes_and_hours()
    {
        var freshness = Synced(
            new ProjectSourceFreshness("other", Usable: true, LastSync: _now.AddSeconds(-30), LastCode: null),
            new ProjectSourceFreshness("github", Usable: true, LastSync: _now.AddMinutes(-125), LastCode: null),
            new ProjectSourceFreshness("linear", Usable: true, LastSync: _now.AddMinutes(-59), LastCode: null),
            new ProjectSourceFreshness("linear-projects", Usable: true, LastSync: _now.AddMinutes(-4), LastCode: null));

        var rows = Page(freshness: freshness).Freshness;

        Expect.Sequence(["linear-projects", "linear", "github", "other"], rows.Select(static r => r.Source));
        Expect.Sequence(["synced 4 min ago", "synced 59 min ago", "synced 2 h ago", "synced just now"], rows.Select(static r => r.Text));
        Expect.Equal(ProjectFreshnessState.Ok, rows[0].State);
        Expect.Equal(_now.AddMinutes(-4), rows[0].At);
    }

    [Fact]
    public void Build_marks_a_failing_source_with_its_code_and_an_unread_one_as_syncing()
    {
        var freshness = Synced(
            new ProjectSourceFreshness("linear", Usable: true, LastSync: _now.AddMinutes(-1), LastCode: "LINEAR-TOKEN"),
            new ProjectSourceFreshness("github", Usable: false, LastSync: null, LastCode: null));

        var rows = Page(freshness: freshness).Freshness;

        Expect.Equal((ProjectFreshnessState.Failing, "needs attention (LINEAR-TOKEN)"), (rows[0].State, rows[0].Text));
        Expect.Equal((ProjectFreshnessState.Syncing, "still syncing"), (rows[1].State, rows[1].Text));
    }

    [Fact]
    public void Build_adds_a_context_row_when_only_the_freshness_read_failed()
    {
        var page = Page([Fact()], freshness: SindriErrors.ProjectFactsUnavailable.With("no status"));

        Expect.True(page.Empty is null);
        Expect.Equal(new ProjectFreshnessRow("context", ProjectFreshnessState.Failing, "no status", At: null), page.Freshness.Single());
    }
}
