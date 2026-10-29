using System.Net;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Projects.Vocabulary;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetProject;
using Aw.Sindri.Features.ListProjects;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Projects;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The project write verbs (projects page spec §5.3): project add, edit, remove, hide and unhide.</summary>
public sealed class CliProjectVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly string[] _addParts = ["\"name\":\"Kitchen\"", "\"state\":\"inProgress\"", "\"links\":[\"https://example.com/a\",\"https://example.com/b\"]", "\"notes\":\"tile\"", "\"target\":\"2026-12-01\""];
    private static readonly string[] _showParts = ["  notes: tile\n", "  version: 4\n", "  Plan: https://example.com/plan\n"];
    private static readonly string[] _editParts = ["\"version\":3", "\"clearState\":true", "\"notes\":\"n\""];
    private static readonly string[] _forms = ["sindri projects ", "sindri project show ", "sindri project add ", "sindri project edit ", "sindri project remove ", "sindri project hide ", "sindri project unhide "];
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IHumanTokenStore Human => InMemoryHumanTokenStore.WithToken();

    private static string Id => ProjectRows.HandMadeId.Value;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static ProjectAccepted Accepted(string project, long version, string name, long sequence) =>
        new(new CommandAccepted(new SequentialIdGenerator().New<CommandId>(), sequence), project, version, name);

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_add_posts_the_fields_and_prints_the_new_id_and_name()
    {
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, Accepted(Id, 1, "Kitchen", 9));

        var code = await Verbs(api).Run(["project", "add", "--name", "Kitchen", "--link", "https://example.com/a", "--link", "https://example.com/b", "--state", "in-progress", "--notes", "tile", "--target", "2026-12-01"], Token);

        Assert.Equal((0, $"{Id} attached: Kitchen (sequence 9)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        var body = api.Calls[^1].Body;
        Assert.All(_addParts, part => Assert.Contains(part, body, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_add_a_linear_key_sends_it_as_typed()
    {
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, Accepted(ProjectRows.LinearId.Value, 1, ProjectRows.LinearKey, 4));

        var code = await Verbs(api).Run(["project", "add", "--linear", ProjectRows.LinearKey], Token);

        Assert.Equal((0, $"{ProjectRows.LinearId.Value} attached: {ProjectRows.LinearKey} (sequence 4)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.Contains($"\"linear\":\"{ProjectRows.LinearKey}\"", api.Calls[^1].Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_add_json_prints_the_answer()
    {
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, Accepted(Id, 1, "Kitchen", 9));

        var code = await Verbs(api).Run(["project", "add", "--name", "Kitchen", "--json"], Token);

        Assert.Equal(0, code);
        Assert.Contains($"\"project\":\"{Id}\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("add")]
    [InlineData("add --name Kitchen --linear linear:project/abc")]
    public async Task Cli_project_add_needs_exactly_one_of_linear_and_name(string line)
    {
        var api = new RoutedApi();

        var code = await Verbs(api).Run(["project", .. line.Split(' ')], Token);

        Assert.Equal(2, code);
        Assert.Contains("SND-CLI-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_edit_posts_the_version_and_the_clear_flags()
    {
        var api = new RoutedApi().On($"/sindri/projects/{Id}/change", HttpStatusCode.OK, Accepted(Id, 4, "Kitchen", 12));

        var code = await Verbs(api).Run(["project", "edit", Id, "--version", "3", "--clear-state", "--notes", "n"], Token);

        Assert.Equal((0, $"{Id} changed (sequence 12)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.All(_editParts, part => Assert.Contains(part, api.Calls[^1].Body, StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--notes")]
    [InlineData("--note")]   // the old name, an alias for one release (owner q158)
    public async Task Cli_project_add_and_edit_take_notes_under_both_names_and_keep_the_line_breaks(string flag)
    {
        var api = new RoutedApi()
            .On("/sindri/projects", HttpStatusCode.OK, Accepted(Id, 1, "Kitchen", 9))
            .On($"/sindri/projects/{Id}/change", HttpStatusCode.OK, Accepted(Id, 4, "Kitchen", 12));

        var added = await Verbs(api).Run(["project", "add", "--name", "Kitchen", flag, "tile\nsink"], Token);
        var addBody = api.Calls[^1].Body;
        var edited = await Verbs(api).Run(["project", "edit", Id, "--version", "3", flag, "tile\nsink"], Token);

        Assert.Equal((0, 0), (added, edited));
        Assert.Contains("\"notes\":\"tile\\nsink\"", addBody, StringComparison.Ordinal);
        Assert.Contains("\"notes\":\"tile\\nsink\"", api.Calls[^1].Body, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("--clear-notes")]
    [InlineData("--clear-note")]
    public async Task Cli_project_edit_clears_the_notes_under_both_flag_names(string flag)
    {
        var api = new RoutedApi().On($"/sindri/projects/{Id}/change", HttpStatusCode.OK, Accepted(Id, 4, "Kitchen", 12));

        var code = await Verbs(api).Run(["project", "edit", Id, "--version", "3", flag], Token);

        Assert.Equal(0, code);
        Assert.Contains("\"clearNotes\":true", api.Calls[^1].Body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_write_without_a_version_reads_the_projects_version_first_and_sends_it()
    {
        var api = new RoutedApi()
            .On($"/sindri/projects/{Id}", HttpStatusCode.OK, Shown(Card(Id, "Kitchen", version: 5), hidden: false))
            .On($"/sindri/projects/{Id}/change", HttpStatusCode.OK, Accepted(Id, 6, "Kitchen", 12))
            .On($"/sindri/projects/{Id}/detach", HttpStatusCode.OK, Accepted(Id, 6, "Kitchen", 13));

        var edited = await Verbs(api).Run(["project", "edit", Id, "--notes", "n"], Token);
        var editBody = api.Calls[^1].Body;
        var removed = await Verbs(api).Run(["project", "remove", Id], Token);

        Assert.Equal((0, 0), (edited, removed));
        Assert.Equal(string.Join(',', ["GET /sindri/status", $"GET /sindri/projects/{Id}", $"POST /sindri/projects/{Id}/change", "GET /sindri/status", $"GET /sindri/projects/{Id}", $"POST /sindri/projects/{Id}/detach"]), string.Join(',', api.Calls.Select(static c => $"{c.Method} {c.Path}")));
        Assert.Contains("\"version\":5", editBody, StringComparison.Ordinal);
        Assert.Equal("""{"version":5}""", api.Calls[^1].Body);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_write_whose_version_read_fails_is_that_error_and_posts_nothing()
    {
        var api = new RoutedApi();

        var code = await Verbs(api).Run(["project", "remove", Id], Token);

        Assert.Equal(2, code);
        Assert.DoesNotContain(api.Calls, static c => c.Method == "POST");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_projects_prints_a_line_per_card_then_the_folds_and_the_freshness()
    {
        var at = new DateTimeOffset(2026, 10, 28, 9, 30, 0, TimeSpan.Zero);
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, new ListProjectsResponse(
            _fine, Empty: null, Viewer: null, [new ProjectFreshnessRow("linear-projects", ProjectFreshnessState.Ok, "synced 4 min ago", at)],
            [Card("linear:project/a", "Checkout rewrite", state: ProjectState.InProgress, issues: 12, at: at), Card(Id, "Kitchen", counting: true), Card("linear:project/b", "Legacy", state: null, issues: null)],
            [Card("linear:project/c", "Old launch", state: ProjectState.Done, issues: 0, at: at)],
            [Card("linear:project/d", "Muted", state: ProjectState.Paused, issues: 1, at: at)]));

        var code = await Verbs(api).Run(["projects"], Token);

        Assert.Equal(0, code);
        Assert.Equal(
            "linear:project/a  in-progress  Checkout rewrite  12 issues  2026-10-28T09:30:00Z\n" +
            $"{Id}  planned  Kitchen  counting…  -\n" +
            "linear:project/b  -  Legacy  -  -\n" +
            "Finished (1)\n  linear:project/c  done  Old launch  0 issues  2026-10-28T09:30:00Z\n" +
            "Hidden (1)\n  linear:project/d  paused  Muted  1 issues  2026-10-28T09:30:00Z\n" +
            "linear-projects synced 4 min ago\n",
            _out.ToString().ReplaceLineEndings("\n"));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_projects_all_asks_for_every_finished_project_and_the_empty_sentence_is_printed()
    {
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, new ListProjectsResponse(_fine, "No projects yet.", Viewer: null, [], [], [], []));

        var code = await Verbs(api).Run(["projects", "--all"], Token);

        Assert.Equal((0, "No projects yet.\n", "?all=true"), (code, _out.ToString().ReplaceLineEndings("\n"), api.Calls[^1].Query));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_projects_json_prints_the_answer_as_the_route_gave_it()
    {
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, new ListProjectsResponse(_fine, Empty: null, Viewer: null, [], [Card(Id, "Kitchen")], [], []));

        var code = await Verbs(api).Run(["projects", "--json"], Token);

        Assert.Equal(0, code);
        Assert.Contains($"\"id\":\"{Id}\"", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_projects_exits_1_while_the_views_stall()
    {
        var api = new RoutedApi().On("/sindri/projects", HttpStatusCode.OK, new ListProjectsResponse(_fine with { Stalled = true }, "Views are stalled.", Viewer: null, [], [], [], []));

        var code = await Verbs(api).Run(["projects"], Token);

        Assert.Equal((1, "Views are stalled.\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_show_prints_the_card_in_full_with_its_links()
    {
        var api = new RoutedApi().On($"/sindri/projects/{Id}", HttpStatusCode.OK, Shown(Card(Id, "Kitchen", version: 4, notes: "tile", links: [new ProjectLink("Plan", "https://example.com/plan")]), hidden: false));

        var code = await Verbs(api).Run(["project", "show", Id], Token);

        var text = _out.ToString().ReplaceLineEndings("\n");
        Assert.Equal(0, code);
        Assert.StartsWith($"{Id}  planned  Kitchen", text, StringComparison.Ordinal);
        Assert.All(_showParts, part => Assert.Contains(part, text, StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_show_of_an_unknown_project_is_SND_PROJECT_404_and_exits_2()
    {
        var api = new RoutedApi().OnRaw($"/sindri/projects/{Id}", HttpStatusCode.NotFound, """{"type":"about:blank","title":"SND-PROJECT-404","status":404,"detail":"No such project"}""");

        var code = await Verbs(api).Run(["project", "show", Id], Token);

        Assert.Equal(2, code);
        Assert.Contains("SND-PROJECT-404", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_reads_say_the_control_plane_is_not_running_with_exit_1()
    {
        using var client = new ControlPlaneClient(new FakeControlPlaneProbe(), SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), Human, _ => ValueTask.FromResult((RepoName?)null)), handler: new RoutedApi());
        var verbs = new ProjectVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));

        var code = await verbs.Run(["projects", "--no-start"], Token);

        Assert.Equal(1, code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_remove_posts_the_detach()
    {
        var api = new RoutedApi().On($"/sindri/projects/{Id}/detach", HttpStatusCode.OK, Accepted(Id, 2, "Kitchen", 15));

        var code = await Verbs(api).Run(["project", "remove", Id, "--version", "1"], Token);

        Assert.Equal((0, $"{Id} removed (sequence 15)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.Equal("""{"version":1}""", api.Calls[^1].Body);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_project_hide_and_unhide_post_the_key_and_the_hide_version()
    {
        var api = new RoutedApi().On("/sindri/projects/hide", HttpStatusCode.OK, Accepted(ProjectRows.LinearKey, 1, ProjectRows.LinearKey, 6));

        var hid = await Verbs(api).Run(["project", "hide", ProjectRows.LinearKey, "--version", "0"], Token);
        var hidBody = api.Calls[^1].Body;
        var shown = await Verbs(api).Run(["project", "unhide", ProjectRows.LinearKey, "--version", "1"], Token);

        Assert.Equal((0, 0), (hid, shown));
        Assert.Equal($"{ProjectRows.LinearKey} hidden (sequence 6)\n{ProjectRows.LinearKey} unhidden (sequence 6)\n", _out.ToString().ReplaceLineEndings("\n"));
        Assert.Equal($$"""{"linear":"{{ProjectRows.LinearKey}}","hidden":true,"version":0}""", hidBody);
        Assert.Contains("\"hidden\":false", api.Calls[^1].Body, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("add --name K --state nope", "SND-PROJECT-004 state:")]
    [InlineData("add --name K --target 12/01/2026", "SND-PROJECT-004 target:")]
    [InlineData("edit nope --version 1 --notes x", "SND-CLI-003")]
    [InlineData("edit prj_x --version one", "SND-CLI-003")]
    [InlineData("remove --version 1", "SND-CLI-003")]
    [InlineData("add --name", "SND-CLI-003")]
    [InlineData("frobnicate", "SND-CLI-")]
    public async Task A_bad_argument_exits_2_before_any_call(string line, string said)
    {
        var api = new RoutedApi();

        var code = await Verbs(api).Run(["project", .. line.Split(' ')], Token);

        Assert.Equal(2, code);
        Assert.Contains(said, _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
    }

    [Fact, Trait("Lane", "unit")]
    public void Usage_names_the_project_verbs() => Assert.All(_forms, form => Assert.Contains(form, Usage.Text, StringComparison.Ordinal));

    private static readonly AsOf _fine = new(7, 7, ProjectedAt: null, Rebuilding: false, Stalled: false);

    private static ProjectCard Card(string key, string name, ProjectState? state = ProjectState.Planned, int? issues = null, bool counting = false, DateTimeOffset? at = null, long version = 1, string? notes = null, IReadOnlyList<ProjectLink>? links = null) =>
        new(key, ProjectOrigin.HandMade, name, Description: null, state, StateSetByOwner: false, Health: null, Lead: null, Target: null, issues, counting, OpenPrs: null, PrScope: null, at, FinishedAt: null, [], links ?? [], notes, Synced: true, version);

    private static GetProjectResponse Shown(ProjectCard card, bool hidden) => new(_fine, Viewer: null, [], card, hidden);

    private ProjectVerbs Verbs(RoutedApi api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), Human, _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new ProjectVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));
    }
}
