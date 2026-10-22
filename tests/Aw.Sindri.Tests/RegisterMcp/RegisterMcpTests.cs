using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

[Lane(TestLane.Unit)]
public sealed class RegisterMcpTests
{
    private readonly InMemoryProfiles _profiles = new();
    private readonly FakeRepoLocator _locator = new FakeRepoLocator().In("/work/demo-wt/src", "/work/demo");
    private readonly InMemoryEndpointFile _endpoint = new();
    private readonly FakeClaudeCli _claude = new();
    private readonly FakeProviderHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task From_a_worktree_it_prints_the_local_scope_command_for_the_main_checkout_on_the_running_port()
    {
        await Approve(apiPort: null);
        _ = await _endpoint.Write(new ControlEndpoint(new Uri("http://127.0.0.1:7495/"), 42, 1), Token);

        var registration = (await Service().Handle(new RegisterMcpRequest("/work/demo-wt/src", Repo: null, Apply: false), Token)).Value;

        Assert.Equal("demo", registration.Repo.Value);
        Assert.Equal("/work/demo", registration.Checkout);
        Assert.Equal(["mcp", "add", "--transport", "http", "--scope", "local", "sindri", "http://127.0.0.1:7495/sindri/mcp", "--header", "X-Sindri-Repo: demo"],
            registration.Arguments);
        Assert.False(registration.Applied);
        Assert.Empty(_claude.Runs);
    }

    [Fact]
    public void The_server_name_and_url_come_before_the_header_because_claude_reads_every_later_argument_as_a_header()
    {
        var arguments = McpRegistration.Arguments(RepoName.From("demo"), 7495);

        var header = arguments.ToList().IndexOf("--header");
        Assert.True(header > arguments.ToList().IndexOf(McpScope.ServerName), "the name must precede the variadic --header");
        Assert.Equal(arguments.Count - 2, header);   // why: claude mcp add's -H <header...> swallows every argument after it
    }

    [Fact]
    public async Task With_nothing_running_the_port_is_the_profile_api_port_else_the_default()
    {
        await Approve(apiPort: 7444);
        var profilePort = (await Service().Handle(new RegisterMcpRequest("/elsewhere", "demo", Apply: false), Token)).Value;
        await Approve(apiPort: null);
        var defaultPort = (await Service().Handle(new RegisterMcpRequest("/elsewhere", "demo", Apply: false), Token)).Value;

        Assert.EndsWith("127.0.0.1:7444/sindri/mcp", Url(profilePort.Arguments), StringComparison.Ordinal);
        Assert.EndsWith($"127.0.0.1:{ApiSettings.DefaultPort}/sindri/mcp", Url(defaultPort.Arguments), StringComparison.Ordinal);
    }

    private static string Url(IReadOnlyList<string> arguments) => arguments.Single(a => a.StartsWith("http://", StringComparison.Ordinal));

    [Fact]
    public async Task Outside_every_profile_repo_or_with_an_unknown_repo_it_is_024()
    {
        await Approve(apiPort: null);

        var outside = await Service().Handle(new RegisterMcpRequest("/elsewhere", Repo: null, Apply: false), Token);
        var unknown = await Service().Handle(new RegisterMcpRequest("/work/demo", "nope", Apply: false), Token);

        Assert.Equal("SND-CONTEXT-024", outside.Error.Code);
        Assert.Equal("SND-CONTEXT-024", unknown.Error.Code);
        Assert.Contains("one of: demo", unknown.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_runs_claude_once_in_the_main_checkout()
    {
        await Approve(apiPort: 7444);

        var applied = (await Service().Handle(new RegisterMcpRequest("/work/demo-wt/src", Repo: null, Apply: true), Token)).Value;

        Assert.True(applied.Applied);
        var run = Assert.Single(_claude.Runs);
        Assert.Equal(applied.Arguments, run.Arguments);
        Assert.Equal("/work/demo", run.Directory);
    }

    [Theory]
    [InlineData("/work/my demo")]
    [InlineData("/work/-rf")]
    [InlineData("/work/a;b$(touch x)`id`&|\"'")]
    public async Task A_checkout_path_with_spaces_dashes_or_shell_characters_is_only_the_working_directory_never_an_argument(string checkout)
    {
        await Approve(apiPort: 7444, checkout);

        var applied = (await Service().Handle(new RegisterMcpRequest(Directory: string.Empty, "demo", Apply: true), Token)).Value;

        var run = Assert.Single(_claude.Runs);
        Assert.Equal(checkout, run.Directory);
        Assert.Equal(["mcp", "add", "--transport", "http", "--scope", "local", "sindri", "http://127.0.0.1:7444/sindri/mcp", "--header", "X-Sindri-Repo: demo"], run.Arguments);   // why: the argv is fixed; only the validated repo name varies
        Assert.DoesNotContain(run.Arguments, a => a.Contains(checkout, StringComparison.Ordinal));
        Assert.Equal(run.Arguments, applied.Arguments);
    }

    [Theory]
    [InlineData("a-b")]
    [InlineData("a.b_c-9")]
    public void The_repo_name_is_the_only_variable_argument_and_cannot_lead_with_a_dash_or_hold_a_space(string name)
    {
        var arguments = McpRegistration.Arguments(RepoName.From(name), 7495);

        Assert.Equal("X-Sindri-Repo: " + name, arguments[^1]);
        Assert.Equal(10, arguments.Count);
        Assert.False(RepoName.TryFrom("-rf", out _));
        Assert.False(RepoName.TryFrom("a b", out _));
        Assert.False(RepoName.TryFrom("a;b", out _));
    }

    [Fact]
    public async Task A_failing_or_missing_claude_is_a_failed_result_with_a_fix()
    {
        await Approve(apiPort: null);
        _claude.Answer = new ClaudeRun(1, "MCP server sindri already exists in local config");
        var failed = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true), Token)).Value.Providers.Single();
        _claude.Answer = null;
        var missing = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true), Token)).Value.Providers.Single();

        Assert.Equal(ProviderOutcome.Failed, failed.Outcome);
        Assert.Contains("already exists", failed.Detail, StringComparison.Ordinal);
        Assert.Contains("run it by hand in /work/demo", failed.Fix, StringComparison.Ordinal);
        Assert.Equal(ProviderOutcome.Failed, missing.Outcome);
    }

    [Fact]
    public async Task Claude_is_unchanged_when_the_entry_is_right_and_replaced_when_the_port_moved()
    {
        await Approve(apiPort: 7444);
        _host.Files["/home/u/.claude.json"] = Claude(7444);
        var unchanged = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Claude]), Token)).Value;
        Assert.Equal(ProviderOutcome.Unchanged, unchanged.Providers.Single().Outcome);
        Assert.Empty(_claude.Runs);

        _host.Files["/home/u/.claude.json"] = Claude(7000);
        var replaced = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Claude]), Token)).Value;

        Assert.Equal(ProviderOutcome.Registered, replaced.Providers.Single().Outcome);
        Assert.Equal(["mcp", "remove", "--scope", "local", "sindri"], _claude.Runs[0].Arguments);
        Assert.Equal("add", _claude.Runs[1].Arguments[1]);
    }

    [Fact]
    public async Task All_providers_each_get_their_own_result_and_the_files_are_written_in_the_checkout()
    {
        await Approve(apiPort: 7444);
        var all = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, _allProviders), Token)).Value;

        Assert.Equal([ProviderOutcome.Registered, ProviderOutcome.Registered, ProviderOutcome.Registered], all.Providers.Select(p => p.Outcome));
        Assert.Contains("7444", _host.Files["/work/demo/.cursor/mcp.json"], StringComparison.Ordinal);
        Assert.Contains("7444", _host.Files["/work/demo/.codex/config.toml"], StringComparison.Ordinal);

        var again = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Cursor, ProviderFile.Codex]), Token)).Value;
        Assert.Equal([ProviderOutcome.Unchanged, ProviderOutcome.Unchanged], again.Providers.Select(p => p.Outcome));
    }

    [Fact]
    public async Task Without_apply_nothing_is_written_or_run()
    {
        await Approve(apiPort: null);

        var plan = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: false, _allProviders), Token)).Value;

        Assert.All(plan.Providers, p => Assert.Equal(ProviderOutcome.Registered, p.Outcome));
        Assert.DoesNotContain(_host.Files.Keys, k => k.StartsWith("/work", StringComparison.Ordinal));
        Assert.Empty(_claude.Runs);
    }

    [Fact]
    public async Task A_provider_that_is_not_installed_is_skipped_and_the_others_still_register()
    {
        await Approve(apiPort: null);
        _ = _host.WithoutProvider(ProviderFile.Codex).WithoutProvider(ProviderFile.Cursor);

        var all = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, _allProviders), Token)).Value;

        Assert.Equal([ProviderOutcome.Registered, ProviderOutcome.Skipped, ProviderOutcome.Skipped], all.Providers.Select(p => p.Outcome));
        Assert.False(_host.Files.ContainsKey("/work/demo/.codex/config.toml"));
    }

    [Fact]
    public async Task Codex_in_an_untrusted_project_is_written_and_the_trust_row_is_added_to_the_user_config()
    {
        await Approve(apiPort: null);
        _host.Files["/home/u/.codex/config.toml"] = "# mine\nmodel = \"x\"\n";

        var codex = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Codex]), Token)).Value.Providers.Single();

        Assert.Equal(ProviderOutcome.Registered, codex.Outcome);
        Assert.Contains("registered; trusted this project in /home/u/.codex/config.toml", codex.Detail, StringComparison.Ordinal);
        Assert.True(_host.Files.ContainsKey("/work/demo/.codex/config.toml"));
        Assert.Equal("# mine\nmodel = \"x\"\n\n[projects.\"/work/demo\"]\ntrust_level = \"trusted\"\n", _host.Files["/home/u/.codex/config.toml"]);
    }

    [Fact]
    public async Task Codex_with_an_existing_trust_level_other_than_trusted_is_never_overwritten()
    {
        await Approve(apiPort: null);
        const string user = "[projects.\"/work/demo\"]\ntrust_level = \"untrusted\"\n";
        _host.Files["/home/u/.codex/config.toml"] = user;

        var codex = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Codex]), Token)).Value.Providers.Single();

        Assert.Equal(ProviderOutcome.NeedsTrust, codex.Outcome);
        Assert.Contains("project trust is \"untrusted\", not changed", codex.Detail, StringComparison.Ordinal);
        Assert.Equal(user, _host.Files["/home/u/.codex/config.toml"]);
    }

    [Theory]
    [InlineData(".cursor/mcp.json", ProviderFile.Cursor)]
    [InlineData(".codex/config.toml", ProviderFile.Codex)]
    public async Task A_tracked_project_file_that_would_change_is_failed_not_edited(string relative, ProviderFile provider)
    {
        await Approve(apiPort: null);
        var path = "/work/demo/" + relative;
        _host.Files[path] = provider == ProviderFile.Cursor ? "{}" : "model = \"x\"\n";
        _ = _host.WithTracked(path);

        var result = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [provider]), Token)).Value.Providers.Single();

        Assert.Equal(ProviderOutcome.Failed, result.Outcome);
        Assert.Contains("is tracked by git, so it is refused and left unchanged", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("sindri", _host.Files[path], StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unwritable_or_malformed_file_fails_that_provider_only()
    {
        await Approve(apiPort: null);
        _host.Files["/work/demo/.cursor/mcp.json"] = "{ nope";
        _ = _host.Unwritable.Add("/work/demo/.codex/config.toml");

        var all = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, _allProviders), Token)).Value;

        Assert.Equal([ProviderOutcome.Registered, ProviderOutcome.Failed, ProviderOutcome.Failed], all.Providers.Select(p => p.Outcome));
        Assert.Contains("add the sindri entry to .cursor/mcp.json by hand (\"mcpServers\"", all.Providers[1].Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_checkout_whose_path_needs_escaping_gets_one_trust_row_and_a_rerun_writes_nothing()
    {
        const string checkout = "/work/my \"demo\" \\ repo";
        await Approve(apiPort: null, checkout);

        var first = (await Service().Handle(new RegisterMcpRequest(checkout, "demo", Apply: true, [ProviderFile.Codex]), Token)).Value.Providers.Single();
        var user = _host.Files["/home/u/.codex/config.toml"];
        var again = (await Service().Handle(new RegisterMcpRequest(checkout, "demo", Apply: true, [ProviderFile.Codex]), Token)).Value.Providers.Single();

        Assert.Equal(ProviderOutcome.Registered, first.Outcome);
        Assert.Equal(ProviderOutcome.Unchanged, again.Outcome);
        Assert.Equal(user, _host.Files["/home/u/.codex/config.toml"]);
        Assert.Equal(1, user.Split("[projects.").Length - 1);
    }

    [Fact]
    public async Task With_the_trust_switch_off_codex_needs_trust_and_the_user_config_is_not_written()
    {
        await Approve(apiPort: null);
        const string user = "model = \"x\"\n";
        _host.Files["/home/u/.codex/config.toml"] = user;

        var codex = (await new RegisterMcpService(_profiles, _locator, _endpoint, _claude, _host) { WriteCodexTrust = false }.Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Codex]), Token)).Value.Providers.Single();

        Assert.Equal(ProviderOutcome.NeedsTrust, codex.Outcome);
        Assert.Contains("the project isn't trusted", codex.Detail, StringComparison.Ordinal);
        Assert.Equal(user, _host.Files["/home/u/.codex/config.toml"]);
        Assert.True(_host.Files.ContainsKey("/work/demo/.codex/config.toml"));
    }

    [Fact]
    public async Task Without_apply_codex_previews_the_trust_row_and_writes_nothing()
    {
        await Approve(apiPort: null);

        var codex = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: false, [ProviderFile.Codex]), Token)).Value.Providers.Single();

        Assert.Contains("would register; trust this project in /home/u/.codex/config.toml", codex.Detail, StringComparison.Ordinal);
        Assert.Empty(_host.Files);
    }

    [Fact]
    public async Task A_changed_claude_entry_is_previewed_without_apply_and_a_failed_removal_is_reported()
    {
        await Approve(apiPort: 7444);
        _host.Files["/home/u/.claude.json"] = Claude(7000);

        var preview = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: false, [ProviderFile.Claude]), Token)).Value.Providers.Single();
        _claude.Answer = new ClaudeRun(1, "no such server");
        var removal = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [ProviderFile.Claude]), Token)).Value.Providers.Single();

        Assert.StartsWith("would replace the entry, then run: claude mcp add", preview.Detail, StringComparison.Ordinal);
        Assert.Equal((ProviderOutcome.Failed, "removing the old entry failed"), (removal.Outcome, removal.Detail));
        _ = Assert.Single(_claude.Runs);
    }

    [Theory]
    [InlineData(".cursor/mcp.json", ProviderFile.Cursor)]
    [InlineData(".codex/config.toml", ProviderFile.Codex)]
    public async Task The_tracked_file_refusal_names_the_file_and_the_entry_to_add_by_hand(string relative, ProviderFile provider)
    {
        await Approve(apiPort: 7444);
        var path = "/work/demo/" + relative;
        _host.Files[path] = provider == ProviderFile.Cursor ? "{}" : "model = \"x\"\n";
        _ = _host.WithTracked(path);

        var fix = (await Service().Handle(new RegisterMcpRequest("/work/demo", "demo", Apply: true, [provider]), Token)).Value.Providers.Single().Fix;

        Assert.Contains(relative, fix, StringComparison.Ordinal);
        Assert.Contains("http://127.0.0.1:7444/sindri/mcp", fix, StringComparison.Ordinal);
        Assert.Contains("X-Sindri-Repo", fix, StringComparison.Ordinal);
        Assert.Contains("untrack", fix, StringComparison.Ordinal);
        if (provider == ProviderFile.Codex)
        {
            Assert.Contains("[mcp_servers.sindri]\nurl = \"http://127.0.0.1:7444/sindri/mcp\"\nhttp_headers = ", fix, StringComparison.Ordinal);
        }
    }

    private static readonly ProviderFile[] _allProviders = [ProviderFile.Claude, ProviderFile.Cursor, ProviderFile.Codex];

    private static string Claude(int port) =>
        $$"""{"projects":{"/work/demo":{"mcpServers":{"sindri":{"type":"http","url":"http://127.0.0.1:{{port.ToString(CultureInfo.InvariantCulture)}}/sindri/mcp","headers":{"X-Sindri-Repo":"demo"} } } } } }""";

    private RegisterMcpService Service() => new(_profiles, _locator, _endpoint, _claude, _host);

    private async Task Approve(int? apiPort, string checkout = "/work/demo")
    {
        var live = SliceHarness.Profile();
        var profile = live.Profile with { Repos = [new RepoEntry("demo", checkout)], Api = apiPort is { } port ? new ApiSettings(port) : null };
        _ = await _profiles.Save(live with { Profile = profile }, Token);
    }
}
