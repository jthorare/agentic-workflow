using System.Net;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Instance;

/// <summary>The instance frame's header and reads (screens §4.2, U8 T3): an address resolved through the list or an id read straight, every header word a served field, and the two failure states.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceFrameViewModelTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");
    private readonly FakeSindriApi _server = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static JsonNode Golden(string scenario) => GoldenFiles.Node("workflow-instance", scenario);

    private static string Text(JsonNode? node) => (string)node!;

    private InstanceFrameViewModel Frame() => new(_server, _clipboard, _clock);

    private async Task<InstanceFrameViewModel> Opened(string scenario)
    {
        var frame = Frame();
        await frame.Open(_api, Text(Golden(scenario)["address"]), Text(Golden(scenario)["id"]), Token);
        return frame;
    }

    // "since 09:14 (3h 12m)": the engine writes it on the status text's first line.
    private static string SinceOf(string scenario)
    {
        var line = Text(Golden(scenario)["current"]!["status"]!["text"]).Split('\n')[0];
        return line[line.IndexOf("since ", StringComparison.Ordinal)..];
    }

    [Theory]
    [InlineData("project-build")]
    [InlineData("item-produce")]
    public async Task Resolves_a_deep_link_address_by_the_list_then_reads_by_id(string scenario)
    {
        var frame = Frame();

        await frame.Open(_api, Text(Golden(scenario)["address"]), id: null, Token);

        Assert.Equal(new InstanceQuery(Address: Text(Golden(scenario)["address"])), _server.LastInstancesQuery);
        Assert.Equal(Text(Golden(scenario)["id"]), _server.LastInstanceId);
        Assert.Equal((FramePhase.Shown, Text(Golden(scenario)["address"])), (frame.Phase, frame.Address));
    }

    [Fact]
    public async Task The_served_Step_runs_show_as_the_current_attempt_and_a_list_of_runs()
    {
        var frame = await Opened("item-produce");

        Assert.Equal("Attempt 2 of 3, with you", frame.StepRuns!.CurrentLine);
        Assert.Equal(2, frame.StepRuns.Rows.Count);
    }

    [Fact]
    public async Task An_engine_owned_instance_names_nobody_on_the_current_attempt()
    {
        var node = Golden("item-produce");
        node["engineOwned"] = new System.Text.Json.Nodes.JsonObject { ["kind"] = "parked", ["reason"] = "waiting" };
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = Frame();

        await frame.Open(_api, Text(node["address"]), Text(node["id"]), Token);

        Assert.Equal("Attempt 2 of 3", frame.StepRuns!.CurrentLine);
    }

    [Fact]
    public async Task A_server_that_serves_no_Step_runs_draws_none()
    {
        var node = Golden("item-produce");
        _ = node.AsObject().Remove("stepRuns");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = Frame();

        await frame.Open(_api, Text(node["address"]), Text(node["id"]), Token);

        Assert.Equal((FramePhase.Shown, string.Empty), (frame.Phase, frame.ProblemText));
        Assert.False(frame.StepRuns!.HasRows);
    }

    [Fact]
    public async Task Opened_from_a_row_reads_by_id_with_no_lookup()
    {
        var frame = await Opened("project-build");

        Assert.Equal((0, Text(Golden("project-build")["id"]), FramePhase.Shown), (_server.InstancesCalls, _server.LastInstanceId, frame.Phase));
    }

    [Fact]
    public async Task Header_draws_the_instance_version_the_status_header_serves_not_the_recipes()
    {
        var node = Golden("dev-desk-round-trip");
        node["current"]!["status"]!["header"]!["version"] = (int)node["recipe"]!["version"]! + 1;
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = Frame();

        await frame.Open(_api, Text(node["address"]), Text(node["id"]), Token);

        var served = (int)node["current"]!["status"]!["header"]!["version"]!;
        Assert.NotEqual((int)node["recipe"]!["version"]!, served);
        var word = "v" + served.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(word, frame.Version);
        Assert.Contains(word, frame.DetailsText, StringComparison.Ordinal);
        Assert.DoesNotContain(word, frame.Identity, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("project-build")]
    [InlineData("escalated-gate")]
    [InlineData("incident-loop")]
    [InlineData("dev-desk-round-trip")]
    public async Task Header_reads_address_title_recipe_hash12_version_state_since(string scenario)
    {
        var frame = await Opened(scenario);

        var golden = Golden(scenario);
        var header = golden["current"]!["status"]!["header"]!;
        Assert.Equal(
            (Text(golden["address"]), Text(golden["title"]), Text(golden["recipe"]!["id"]), Text(header["hash12"]), $"v{header["version"]}", Text(header["state"]), SinceOf(scenario)),
            (frame.Address, frame.Title, frame.Recipe, frame.Hash12, frame.Version, frame.StateText, frame.SinceText));
        Assert.Contains(frame.Recipe + "@" + frame.Hash12, frame.DetailsText, StringComparison.Ordinal);
        Assert.Contains(frame.Address, frame.DetailsText, StringComparison.Ordinal);
        Assert.DoesNotContain(frame.Address, frame.Identity, StringComparison.Ordinal);   // the address is for Details, never the headline
        Assert.DoesNotContain(frame.Recipe, frame.Identity, StringComparison.Ordinal);
        Assert.StartsWith(frame.WorkflowName, frame.Identity, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("project-build")]
    [InlineData("dev-desk-round-trip")]
    public async Task Recipe_link_names_the_pinned_hash(string scenario)
    {
        var frame = await Opened(scenario);
        var golden = Golden(scenario);
        var (id, hash12) = (Text(golden["recipe"]!["id"]), Text(golden["current"]!["status"]!["header"]!["hash12"]));
        var asked = new List<string>();
        frame.RecipeRequested += (_, e) => asked.Add(e.Subject);

        frame.OpenRecipe();

        Assert.Equal(($"{id}@{hash12}", true, "Workflow type ▸"), (frame.RecipeSubject, frame.HasRecipeLink, frame.RecipeLinkText));
        Assert.StartsWith("Workflow type", frame.RecipeLinkName, StringComparison.Ordinal);   // the visible word leads the name
        Assert.Contains(frame.WorkflowName, frame.RecipeLinkName, StringComparison.Ordinal);   // the name a person knows, not id@hash
        Assert.DoesNotContain($"{id}@{hash12}", frame.RecipeLinkName, StringComparison.Ordinal);
        Expect.Sequence([$"{id}@{hash12}"], asked);
        Assert.StartsWith(hash12, Text(golden["recipe"]!["hash"])["sha256:".Length..], StringComparison.Ordinal);   // the pinned version is the one the recipe golden serves
    }

    [Fact]
    public void Recipe_link_is_absent_before_a_read()
    {
        var frame = Frame();
        var asked = 0;
        frame.RecipeRequested += (_, _) => asked++;

        frame.OpenRecipe();

        Assert.Equal((false, string.Empty, 0), (frame.HasRecipeLink, frame.RecipeSubject, asked));
    }

    [Fact]
    public async Task Workflow_name_is_the_served_display_name_else_the_recipe_id_title_cased()
    {
        var node = Golden("project-build");
        var id = Text(node["recipe"]!["id"]);
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var older = Frame();
        await older.Open(_api, Text(node["address"]), Text(node["id"]), Token);
        node["recipe"]!["displayName"] = "Build a project";
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var served = Frame();
        await served.Open(_api, Text(node["address"]), Text(node["id"]), Token);

        Assert.Equal(("Build a project", true), (served.WorkflowName, served.Identity.StartsWith("Build a project", StringComparison.Ordinal)));
        Assert.DoesNotContain("workflow.", older.WorkflowName, StringComparison.Ordinal);
        Assert.False(char.IsLower(older.WorkflowName[0]));
        Assert.Contains(id, older.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Child_runs_stay_closed_until_a_key_wants_focus_on_them()
    {
        var frame = await Opened("project-build");
        Assert.False(frame.Children!.IsOpen);

        frame.Intend(frame.Address, FrameIntent.FocusChildren);

        Assert.True(frame.Children.IsOpen);   // focus can't land in a closed disclosure
    }

    [Fact]
    public async Task Header_adds_detail_only_when_served()
    {
        var escalated = await Opened("escalated-gate");
        var plain = await Opened("incident-loop");

        var detail = Text(Golden("escalated-gate")["current"]!["status"]!["header"]!["detail"]);
        Assert.Equal((true, detail), (escalated.HasDetail, escalated.Detail));
        Assert.Contains($"{escalated.StateText}: {detail}", escalated.Identity, StringComparison.Ordinal);
        Assert.Equal((false, string.Empty), (plain.HasDetail, plain.Detail));
        Assert.DoesNotContain(plain.StateText + ":", plain.Identity, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Subject_line_is_the_served_label_with_Open_in_from_href()
    {
        var linked = await Opened("project-build");
        var plain = await Opened("incident-loop");

        var subject = Golden("project-build")["subject"]!;
        var host = new Uri(Text(subject["href"])).Host;
        Assert.Equal((Text(subject["label"]), true, Text(subject["href"])), (linked.Subject!.Label, linked.Subject.HasHref, linked.Subject.Href));
        Assert.Contains(host, linked.Subject.OpenInText, StringComparison.Ordinal);
        Assert.Contains(host, linked.Subject.OpenInName, StringComparison.Ordinal);
        Assert.Contains(linked.Address, linked.Subject.OpenInName, StringComparison.Ordinal);
        Assert.Equal((Text(Golden("incident-loop")["subject"]!["label"]), false), (plain.Subject!.Label, plain.Subject.HasHref));
    }

    [Fact]
    public async Task Initial_focus_is_a_read_action_never_Take_over_or_Cancel()
    {
        var linked = await Opened("project-build");
        var plain = await Opened("incident-loop");
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-instance", "not-found");
        var missing = await Opened("project-build");

        Assert.Equal((FrameFocus.OpenIn, FrameFocus.CopyStatus, FrameFocus.Back), (linked.InitialFocus, plain.InitialFocus, missing.InitialFocus));
        string[] commands = ["TakeOver", "Cancel", "Takeover"];
        InstanceFrameViewModel[] frames = [linked, plain, missing];
        Assert.All(frames, f => Assert.DoesNotContain(f.InitialFocus.ToString(), commands, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Not_found_shows_the_served_detail_with_Back()
    {
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-instance", "not-found");

        var frame = await Opened("project-build");

        Assert.Equal((FramePhase.NotFound, Text(GoldenFiles.Node("workflow-instance", "not-found")["detail"]), Labels.Back), (frame.Phase, frame.ProblemText, frame.BackText));
        Assert.Null(frame.Band);
    }

    [Fact]
    public async Task An_address_no_row_answers_to_is_not_found()
    {
        var frame = Frame();

        var absent = Text(Golden("project-build")["address"]) + "/absent";   // an address the list does not serve

        await frame.Open(_api, absent, id: null, Token);

        Assert.Equal((FramePhase.NotFound, Labels.InstanceNotFound(absent)), (frame.Phase, frame.ProblemText));
    }

    [Fact]
    public async Task Unreadable_shows_the_shared_state_with_Back()
    {
        var body = (JsonObject)GoldenFiles.Node("workflow-instance", "project-build");
        _ = body.Remove("states");
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        var frame = await Opened("project-build");

        Assert.Equal((FramePhase.Unreadable, Labels.StripUnreadable, Labels.Back, FrameFocus.Back), (frame.Phase, frame.ProblemText, frame.BackText, frame.InitialFocus));
    }

    [Theory]
    [InlineData("failed", "This run failed.")]
    [InlineData("unresolved", "This run didn't work out.")]
    [InlineData("cancelled", "This run was cancelled.")]
    [InlineData("resolved", "This run is done.")]
    [InlineData("alreadySatisfied", "This run is done.")]
    [InlineData(null, "This run is done.")]
    public async Task The_finished_line_follows_the_terminal_cells_outcome(string? outcome, string expected)
    {
        var node = (JsonObject)GoldenFiles.Node("workflow-instance", "done");
        var terminal = node["states"]!.AsArray().Single(s => (bool)s!["terminal"]! && Text(s["id"]) == Text(node["state"]))!;
        terminal["outcome"] = outcome;
        _server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, node.ToJsonString());
        var frame = Frame();

        await frame.Open(_api, Text(node["address"]), Text(node["id"]), Token);

        Assert.Equal(expected, frame.Note);
    }

    [Fact]
    public async Task A_re_read_that_finds_the_instance_done_keeps_the_content_and_says_done()
    {
        var frame = await Opened("project-build");
        var band = frame.Band;
        _server.WorkflowAnswer = RouteAnswer.Api("workflow-instance", "done");

        await frame.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, Labels.InstanceDone, Text(Golden("done")["current"]!["status"]!["header"]!["state"])), (frame.Phase, frame.Note, frame.StateText));
        Assert.NotNull(frame.Band);
        Assert.NotSame(band, frame.Band);
    }

    [Fact]
    public async Task A_re_read_that_no_longer_finds_the_instance_keeps_what_was_shown_and_says_so()
    {
        var frame = await Opened("project-build");
        _server.WorkflowAnswer = RouteAnswer.Problem("workflow-instance", "not-found");

        await frame.Refresh(_api, Token);

        Assert.Equal((FramePhase.Shown, Labels.InstanceGone, Text(Golden("project-build")["title"])), (frame.Phase, frame.Note, frame.Title));
    }

    [Fact]
    public async Task Open_in_hands_the_subjects_link_to_the_launcher()
    {
        var launcher = new FakeLauncher();
        var scenario = "project-build";
        var detail = FixtureInstances.Detail(scenario);
        var line = new SubjectLineViewModel(detail.Subject, detail.Address, launcher);

        await line.OpenIn(Token);

        Assert.True(line.HasHref);
        Expect.Sequence([new Uri(line.Href).AbsoluteUri], launcher.Opened);
        var plain = new SubjectLineViewModel(detail.Subject with { Href = "ftp://example.test/x" }, detail.Address, launcher);
        await plain.OpenIn(Token);
        _ = Assert.Single(launcher.Opened);
    }
}
