using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>How the CLI classes itself and the caller headers it sends (API spec §6 "Who sends what", §3.1 repo).</summary>
public sealed class CliCallerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData(true, null, false)]
    [InlineData(false, null, false)]
    [InlineData(true, "1", true)]
    public void Detect_reads_only_the_terminal_and_the_prover_marker(bool terminal, string? prover, bool expectedProver)
    {
        var session = AgentSession.Detect(n => n == "AW_SINDRI_PROVER" ? prover : "1", terminal);   // every other variable is set: an agent's environment changes nothing

        Assert.Equal((terminal, expectedProver), (session.Terminal, session.Prover));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Human_cli_sends_caller_terminal_client_and_token()
    {
        var tokens = new InMemoryHumanTokenStore();
        _ = await tokens.Mint(Token);

        var sent = await Sent(CallerHeaders.For(Human(), tokens, Repo("demo")));

        Assert.Equal(("human", "1", "cli", "token-1", "demo"),
            (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Terminal), Header(sent, SindriHeaders.Client), Header(sent, SindriHeaders.Token), Header(sent, SindriHeaders.Repo)));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("LCH-00000000000000000000000000000001", "LCH-00000000000000000000000000000001")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Detect_reads_the_launch_variable(string? value, string? expected) =>
        Assert.Equal(expected, AgentSession.Detect(n => n == "AW_SINDRI_LAUNCH" ? value : null, interactive: false).Launch);

    [Fact, Trait("Lane", "unit")]
    public async Task The_cli_sends_the_launch_header_when_it_has_a_launch()
    {
        var launched = await Sent(CallerHeaders.For(Human() with { Launch = "LCH-00000000000000000000000000000001" }, new InMemoryHumanTokenStore(), Repo(name: null)));
        var owned = await Sent(CallerHeaders.For(Human(), InMemoryHumanTokenStore.WithToken(), Repo(name: null)));

        Assert.Equal("LCH-00000000000000000000000000000001", Header(launched, SindriHeaders.Launch));
        Assert.Null(Header(owned, SindriHeaders.Launch));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_launched_cli_calls_as_a_session_with_no_token_even_when_the_token_is_readable()
    {
        var headers = CallerHeaders.For(Human() with { Launch = "LCH-00000000000000000000000000000001" }, InMemoryHumanTokenStore.WithToken(), Repo(name: null));

        var sent = await Sent(headers);

        Assert.Equal(("session", "LCH-00000000000000000000000000000001", null, null), (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Launch), Header(sent, SindriHeaders.Token), Header(sent, SindriHeaders.Terminal)));
        Assert.Contains("AW_SINDRI_LAUNCH", headers.SessionReason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Prover_marker_labels_the_human_cli_prover()
    {
        var sent = await Sent(CallerHeaders.For(Human() with { Prover = true }, InMemoryHumanTokenStore.WithToken(), Repo(name: null)));

        Assert.Equal(("human", "prover"), (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Client)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_token_is_read_when_the_call_is_sent_so_a_fresh_start_counts()
    {
        var tokens = new InMemoryHumanTokenStore();
        var headers = CallerHeaders.For(Human(), tokens, Repo(name: null));
        _ = await tokens.Mint(Token);
        _ = await tokens.Mint(Token);   // 'sindri' started the control plane after the headers were made

        var sent = await Sent(headers);

        Assert.Equal("token-2", Header(sent, SindriHeaders.Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_with_no_readable_token_is_a_session_and_sends_no_token_and_no_terminal()
    {
        var sent = await Sent(CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), new InMemoryHumanTokenStore(), Repo("demo")));

        Assert.Equal(("session", null, "cli", null, "demo"),
            (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Terminal), Header(sent, SindriHeaders.Client), Header(sent, SindriHeaders.Token), Header(sent, SindriHeaders.Repo)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Cli_with_a_readable_token_is_a_human_caller_with_or_without_a_terminal()
    {
        var tokens = InMemoryHumanTokenStore.WithToken();

        var noTerminal = await Sent(CallerHeaders.For(new AgentSession(Terminal: false, Prover: false), tokens, Repo(name: null)));

        Assert.Equal(("human", null, "cli", "token-1"),
            (Header(noTerminal, SindriHeaders.Caller), Header(noTerminal, SindriHeaders.Terminal), Header(noTerminal, SindriHeaders.Client), Header(noTerminal, SindriHeaders.Token)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_token_that_cant_be_read_makes_the_cli_a_session_and_names_why()
    {
        var headers = CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), new UnreadableTokenStore(), Repo(name: null));

        var sent = await Sent(headers);

        Assert.Equal(("session", "the per-start human token isn't readable"), (Header(sent, SindriHeaders.Caller), headers.SessionReason));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Repo_header_comes_from_the_main_checkout_and_the_longest_profile_path()
    {
        var approvals = await Approved(new RepoEntry("work", "/work"), new RepoEntry("demo", "/work/demo"));
        var locator = new FakeRepoLocator().In("/work/demo/.claude/worktrees/x", "/work/demo");

        var sent = await Sent(CallerHeaders.For(Human(), new InMemoryHumanTokenStore(),
            ct => CallerHeaders.RepoOf(approvals, locator, "/work/demo/.claude/worktrees/x", ct)));

        Assert.Equal("demo", Header(sent, SindriHeaders.Repo));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/elsewhere", true)]
    [InlineData("/work/demo", false)]
    public async Task Outside_any_profile_repo_the_repo_header_is_omitted(string directory, bool approved)
    {
        var approvals = approved ? await Approved(new RepoEntry("demo", "/work/demo")) : new InMemoryProfiles();
        var locator = new FakeRepoLocator().In("/work/demo", "/work/demo");

        var sent = await Sent(CallerHeaders.For(new AgentSession(Terminal: false, Prover: false), new InMemoryHumanTokenStore(),
            ct => CallerHeaders.RepoOf(approvals, locator, directory, ct)));

        Assert.Null(Header(sent, SindriHeaders.Repo));
    }

    private static AgentSession Human() => new(Terminal: true, Prover: false);

    private static Func<CancellationToken, ValueTask<RepoName?>> Repo(string? name) =>
        _ => ValueTask.FromResult(name is null ? (RepoName?)null : RepoName.From(name));

    private static async Task<InMemoryProfiles> Approved(params RepoEntry[] repos)
    {
        var profiles = new InMemoryProfiles();
        var loaded = SliceHarness.Profile();
        _ = await profiles.Save(loaded with { Profile = loaded.Profile with { Repos = repos } }, Token);
        return profiles;
    }

    private static async Task<HttpRequestMessage> Sent(CallerHeaders headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://127.0.0.1:7410/sindri/status"));
        await headers.Apply(request, Token);
        return request;
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

    /// <summary>A token store whose file can't be read.</summary>
    private sealed class UnreadableTokenStore : IHumanTokenStore
    {
        public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken) => throw new NotSupportedException("the CLI never mints");

        public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Result<HumanToken?>>(Core.SindriErrors.NotRunning.With("unreadable"));

        public ValueTask<Result<Unit>> Remove(HumanToken mine, CancellationToken cancellationToken) => throw new NotSupportedException("the CLI never removes");
    }
}
