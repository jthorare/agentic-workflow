using System.Globalization;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Projects;

/// <summary>The pure decisions on owner projects (projects page spec §5.1, §5.3, §8): field rules, duplicates, not found, stale versions.</summary>
[Lane(TestLane.Unit)]
public sealed class ProjectDecisionsTests
{
    private readonly CommandId _command = new SequentialIdGenerator().New<CommandId>();

    private static ProjectDraft Hand(string? name = "Kitchen", IReadOnlyList<string>? links = null, string? note = null, ProjectState? state = null) =>
        new(Linear: null, name, state, links, note);

    private Kernel.Result<Core.Events.ProjectAttached> Attach(ProjectDraft draft, IReadOnlyList<ProjectAttachedRow>? attached = null, bool led = false) =>
        ProjectDecisions.Attach(_command, ProjectRows.HandMadeId, draft, attached ?? [], led);

    private static void RefusedOn(string field, Kernel.Error error)
    {
        Expect.Equal(SindriErrors.ProjectInvalid.Code, error.Code);
        Expect.True(error.Message.StartsWith(field + ":", StringComparison.Ordinal));
    }

    [Fact]
    public void A_hand_made_project_attaches_with_its_fields()
    {
        var attached = Attach(new ProjectDraft(Linear: null, "  Kitchen  ", ProjectState.InProgress, ["https://example.com/plan"], "one line", new DateOnly(2026, 11, 14))).Value;

        Expect.Equal((ProjectRows.HandMadeId, null, "Kitchen", ProjectState.InProgress, "one line", new DateOnly(2026, 11, 14), _command),
            (attached.Id, attached.Linear, attached.Name, attached.State, attached.Note, attached.Target, attached.CommandId));
        Expect.Sequence(["https://example.com/plan"], attached.Links);
    }

    [Fact]
    public void A_linear_project_attaches_without_a_name_and_drops_one_typed()
    {
        var attached = Attach(new ProjectDraft(ProjectRows.LinearKey, Name: "ignored")).Value;

        Expect.Equal((ProjectRows.LinearKey, null), (attached.Linear, attached.Name));
        Expect.Equal(0, attached.Links.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_hand_made_project_without_a_name_is_SND_PROJECT_004_on_name(string? name) => RefusedOn("name", Attach(Hand(name)).Error);

    [Fact]
    public void A_name_over_200_characters_is_SND_PROJECT_004_on_name() => RefusedOn("name", Attach(Hand(new string('n', 201))).Error);

    [Theory]
    [InlineData("http://example.com/a")]
    [InlineData("ftp://example.com/a")]
    [InlineData("example.com/a")]
    [InlineData("https://example.com/a?token=abc")]
    [InlineData("https://example.com/a#section")]
    [InlineData("https:///a")]
    [InlineData("")]
    public void A_link_that_is_not_a_plain_https_url_is_SND_PROJECT_004_on_links(string link) => RefusedOn("links", Attach(Hand(links: [link])).Error);

    [Fact]
    public void Ten_links_are_fine_and_eleven_are_SND_PROJECT_004_on_links()
    {
        var ten = Enumerable.Range(0, 10).Select(static n => string.Create(CultureInfo.InvariantCulture, $"https://example.com/{n}")).ToList();

        Expect.Equal(10, Attach(Hand(links: ten)).Value.Links.Count);
        RefusedOn("links", Attach(Hand(links: [.. ten, "https://example.com/10"])).Error);
    }

    [Fact]
    public void A_note_of_200_characters_is_fine_and_201_is_SND_PROJECT_004_on_note()
    {
        Expect.Equal(200, Attach(Hand(note: new string('x', 200))).Value.Note!.Length);
        RefusedOn("note", Attach(Hand(note: new string('x', 201))).Error);
    }

    [Theory]
    [InlineData("two\nlines")]
    [InlineData("two\rlines")]
    public void A_note_over_more_than_one_line_is_SND_PROJECT_004_on_note(string note) => RefusedOn("note", Attach(Hand(note: note)).Error);

    [Fact]
    public void A_state_word_that_is_not_one_of_the_six_is_SND_PROJECT_004_on_state() => RefusedOn("state", Attach(Hand(state: (ProjectState)42)).Error);

    [Theory]
    [InlineData("linear:project/")]
    [InlineData("https://linear.app/team/project/x-1")]
    [InlineData("project/abc")]
    public void A_linear_key_that_is_not_a_project_key_is_SND_PROJECT_004_on_linear(string key) => RefusedOn("linear", Attach(new ProjectDraft(key)).Error);

    [Fact]
    public void Attaching_a_key_that_is_already_attached_is_SND_PROJECT_001()
    {
        var result = Attach(new ProjectDraft(ProjectRows.LinearKey), [ProjectRows.Attached()]);

        Expect.Equal(SindriErrors.ProjectAlreadyOnPage.Code, result.Error.Code);
        Expect.Equal("That project is already on your page.", result.Error.Message);
    }

    [Fact]
    public void Attaching_a_key_the_owner_leads_is_SND_PROJECT_001() =>
        Expect.Equal(SindriErrors.ProjectAlreadyOnPage.Code, Attach(new ProjectDraft(ProjectRows.LinearKey), led: true).Error.Code);

    [Fact]
    public void Hand_made_projects_never_collide_with_each_other() =>
        Expect.True(Attach(Hand(), [ProjectRows.HandMade()], led: true).IsSuccess);

    [Fact]
    public void Change_moves_the_version_and_carries_only_the_asked_fields()
    {
        var changed = ProjectDecisions.Change(_command, ProjectRows.HandMadeId, ProjectRows.HandMade(version: 3), new ProjectEdit(Note: "later", Target: new DateOnly(2027, 1, 2)), seenVersion: 3).Value;

        Expect.Equal((ProjectRows.HandMadeId, 4L, "later", new DateOnly(2027, 1, 2), null, null, null), (changed.Id, changed.Version, changed.Note, changed.Target, changed.Name, changed.State, changed.Links));
        Expect.Equal((false, false, false), (changed.ClearState, changed.ClearNote, changed.ClearTarget));
    }

    [Fact]
    public void Change_names_a_cleared_field_by_its_flag()
    {
        var changed = ProjectDecisions.Change(_command, ProjectRows.LinearId, ProjectRows.Attached(), new ProjectEdit(ClearState: true, ClearNote: true, ClearTarget: true), seenVersion: 1).Value;

        Expect.Equal((true, true, true, null, null, null), (changed.ClearState, changed.ClearNote, changed.ClearTarget, changed.State, changed.Note, changed.Target));
    }

    [Fact]
    public void Change_replaces_the_whole_link_list()
    {
        var changed = ProjectDecisions.Change(_command, ProjectRows.HandMadeId, ProjectRows.HandMade(), new ProjectEdit(Links: ["https://example.com/a"]), seenVersion: 1).Value;

        Expect.Sequence(["https://example.com/a"], changed.Links!);
    }

    [Fact]
    public void Change_of_a_missing_project_is_SND_PROJECT_404_naming_it()
    {
        var result = ProjectDecisions.Change(_command, ProjectRows.HandMadeId, project: null, new ProjectEdit(Note: "x"), seenVersion: 1);

        Expect.Equal(SindriErrors.ProjectNotFound.Code, result.Error.Code);
        Expect.Equal($"No project {ProjectRows.HandMadeId.Value}. `sindri projects` lists them.", result.Error.Message);
    }

    [Fact]
    public void Change_at_a_stale_version_is_SND_PROJECT_409()
    {
        var result = ProjectDecisions.Change(_command, ProjectRows.HandMadeId, ProjectRows.HandMade(version: 3), new ProjectEdit(Note: "x"), seenVersion: 2);

        Expect.Equal(SindriErrors.ProjectConflict.Code, result.Error.Code);
        Expect.Equal("This project changed since you read it. Read it again and retry.", result.Error.Message);
    }

    [Fact]
    public void Change_applies_the_field_rules_to_what_it_sets()
    {
        var project = ProjectRows.HandMade();

        RefusedOn("links", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(Links: ["http://example.com"]), 1).Error);
        RefusedOn("note", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(Note: new string('x', 201)), 1).Error);
        RefusedOn("name", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(Name: "  "), 1).Error);
        RefusedOn("state", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(State: (ProjectState)42), 1).Error);
    }

    [Fact]
    public void A_linear_project_has_no_name_of_its_own_to_change() =>
        RefusedOn("name", ProjectDecisions.Change(_command, ProjectRows.LinearId, ProjectRows.Attached(), new ProjectEdit(Name: "Mine"), 1).Error);

    [Fact]
    public void A_change_that_changes_nothing_is_SND_PROJECT_004() =>
        Expect.Equal(SindriErrors.ProjectInvalid.Code, ProjectDecisions.Change(_command, ProjectRows.HandMadeId, ProjectRows.HandMade(), new ProjectEdit(), 1).Error.Code);

    [Fact]
    public void Setting_and_clearing_the_same_field_is_SND_PROJECT_004_on_that_field()
    {
        var project = ProjectRows.HandMade();

        RefusedOn("state", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(State: ProjectState.Done, ClearState: true), 1).Error);
        RefusedOn("note", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(Note: "x", ClearNote: true), 1).Error);
        RefusedOn("target", ProjectDecisions.Change(_command, project.Id, project, new ProjectEdit(Target: new DateOnly(2027, 1, 1), ClearTarget: true), 1).Error);
    }

    [Fact]
    public void Detach_bumps_the_version()
    {
        var detached = ProjectDecisions.Detach(_command, ProjectRows.HandMadeId, ProjectRows.HandMade(version: 5), seenVersion: 5).Value;

        Expect.Equal((ProjectRows.HandMadeId, 6L, _command), (detached.Id, detached.Version, detached.CommandId));
    }

    [Fact]
    public void Detach_of_a_missing_project_is_404_and_at_a_stale_version_409()
    {
        Expect.Equal(SindriErrors.ProjectNotFound.Code, ProjectDecisions.Detach(_command, ProjectRows.HandMadeId, project: null, seenVersion: 1).Error.Code);
        Expect.Equal(SindriErrors.ProjectConflict.Code, ProjectDecisions.Detach(_command, ProjectRows.HandMadeId, ProjectRows.HandMade(version: 2), seenVersion: 1).Error.Code);
    }

    [Fact]
    public void A_led_project_never_hidden_hides_from_version_0_to_1()
    {
        var hidden = ProjectDecisions.Hide(_command, ProjectRows.LinearKey, hidden: true, current: null, seenVersion: 0).Value;

        Expect.Equal((ProjectRows.LinearKey, true, 1L, _command), (hidden.Linear, hidden.Hidden, hidden.Version, hidden.CommandId));
    }

    [Fact]
    public void Unhide_moves_a_hidden_project_on_to_the_next_version()
    {
        var row = new ProjectHiddenRow(ProjectRows.LinearKey, Hidden: true, Version: 1, Sequence: 4);

        var unhidden = ProjectDecisions.Hide(_command, ProjectRows.LinearKey, hidden: false, row, seenVersion: 1).Value;

        Expect.Equal((false, 2L), (unhidden.Hidden, unhidden.Version));
    }

    [Fact]
    public void Hide_at_a_stale_version_is_SND_PROJECT_409()
    {
        var row = new ProjectHiddenRow(ProjectRows.LinearKey, Hidden: true, Version: 1, Sequence: 4);

        Expect.Equal(SindriErrors.ProjectConflict.Code, ProjectDecisions.Hide(_command, ProjectRows.LinearKey, hidden: false, row, seenVersion: 0).Error.Code);
        Expect.Equal(SindriErrors.ProjectConflict.Code, ProjectDecisions.Hide(_command, ProjectRows.LinearKey, hidden: true, current: null, seenVersion: 3).Error.Code);
    }

    [Fact]
    public void Hide_of_a_key_that_is_not_a_project_key_is_SND_PROJECT_004_on_linear() =>
        RefusedOn("linear", ProjectDecisions.Hide(_command, "prj_01J9Z3K5M7Q2R4T6V8WXYZ0123", hidden: true, current: null, seenVersion: 0).Error);

    [Fact]
    public void A_project_that_is_not_synced_lists_the_teams_in_SND_PROJECT_003()
    {
        var error = ProjectDecisions.NotSynced(["ENG", "OPS"]);

        Expect.Equal(SindriErrors.ProjectNotSynced.Code, error.Code);
        Expect.Equal("Sindri doesn't sync that project. It syncs these Linear teams: ENG, OPS. Add the team to `context.sources.linear.teams`.", error.Message);
    }

    [Fact]
    public void The_five_project_codes_are_registered_once()
    {
        var codes = new[] { SindriErrors.ProjectAlreadyOnPage, SindriErrors.ProjectNotSynced, SindriErrors.ProjectInvalid, SindriErrors.ProjectNotFound, SindriErrors.ProjectConflict };

        Expect.Sequence(["SND-PROJECT-001", "SND-PROJECT-003", "SND-PROJECT-004", "SND-PROJECT-404", "SND-PROJECT-409"], codes.Select(static e => e.Code));
        Expect.True(codes.All(static e => SindriErrors.All.Contains(e)));
    }
}
