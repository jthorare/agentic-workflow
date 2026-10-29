using System.Net;
using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Projects;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Projects;

namespace Aw.Sindri.Tests.Commands;

/// <summary>The project write verbs (projects page spec §5.3): project add, edit, remove, hide and unhide.</summary>
public sealed class CliProjectVerbsTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly string[] _addParts = ["\"name\":\"Kitchen\"", "\"state\":\"inProgress\"", "\"links\":[\"https://example.com/a\",\"https://example.com/b\"]", "\"note\":\"tile\"", "\"target\":\"2026-12-01\""];
    private static readonly string[] _editParts = ["\"version\":3", "\"clearState\":true", "\"note\":\"n\""];
    private static readonly string[] _forms = ["sindri project add ", "sindri project edit ", "sindri project remove ", "sindri project hide ", "sindri project unhide "];
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

        var code = await Verbs(api).Run(["project", "add", "--name", "Kitchen", "--link", "https://example.com/a", "--link", "https://example.com/b", "--state", "in-progress", "--note", "tile", "--target", "2026-12-01"], Token);

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

        var code = await Verbs(api).Run(["project", "edit", Id, "--version", "3", "--clear-state", "--note", "n"], Token);

        Assert.Equal((0, $"{Id} changed (sequence 12)\n"), (code, _out.ToString().ReplaceLineEndings("\n")));
        Assert.All(_editParts, part => Assert.Contains(part, api.Calls[^1].Body, StringComparison.Ordinal));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("edit", "--note x")]
    [InlineData("remove", "")]
    [InlineData("hide", "")]
    [InlineData("unhide", "")]
    public async Task The_writes_that_need_a_version_refuse_without_one_before_any_call(string verb, string rest)
    {
        var api = new RoutedApi();
        var project = verb.EndsWith("hide", StringComparison.Ordinal) ? ProjectRows.LinearKey : Id;

        var code = await Verbs(api).Run(["project", verb, project, .. rest.Split(' ', StringSplitOptions.RemoveEmptyEntries)], Token);

        Assert.Equal(2, code);
        Assert.Contains("pass --version N", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(api.Calls);
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
    [InlineData("edit nope --version 1 --note x", "SND-CLI-003")]
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

    private ProjectVerbs Verbs(RoutedApi api)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), Human, _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        return new ProjectVerbs(client, new Output(_out, _error, SliceHarness.Json), (_, _) => Task.FromResult<Kernel.Error?>(null));
    }
}
