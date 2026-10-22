using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Mcp;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;
using Aw.Sindri.Features.RegisterMcp;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.RegisterMcp;

/// <summary>The work of a RegisterMcp job (setup-once spec §5.4): every installed provider, no Codex trust row, no directory read, and 'mcp.registered' with each provider's outcome; a provider that fails fails the job with its own sentence.</summary>
[Lane(TestLane.Unit)]
public sealed class RegisterMcpJobTests : IDisposable
{
    private readonly SliceHarness _slice = new();
    private readonly FakeClaudeCli _claude = new();
    private readonly FakeProviderHost _host = new();
    private readonly InMemoryEndpointFile _endpoint = new();
    private readonly ResolvedMcpRepos _resolved = new();
    private IDisposable? _caller;

    private static readonly string[] _providers = ["claude", "cursor", "codex"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _caller?.Dispose();

    [Fact]
    public async Task The_job_registers_every_installed_provider_without_the_codex_trust_row_and_records_mcp_registered()
    {
        await Start();
        _caller = _slice.Callers.EnterService("test");   // after the await: the ambient caller of an async method does not flow back
        var request = Admit("demo");

        var done = await Service().Handle(request, Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : string.Empty);
        Assert.True(_providers.SequenceEqual(done.Value.Providers.Select(static p => p.Provider.ToString().ToLowerInvariant()), StringComparer.Ordinal));
        _ = Assert.Single(_claude.Runs);
        Assert.DoesNotContain(_host.Files, f => f.Key == _host.UserConfigPath(ProviderFile.Codex));   // Codex asks the owner once
        var recorded = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.McpRegistered);
        Assert.StartsWith("mcp.registered:", recorded.IdempotencyKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_provider_that_fails_fails_the_job_with_its_sentence_after_the_event_is_recorded()
    {
        await Start();
        _caller = _slice.Callers.EnterService("test");   // after the await: the ambient caller of an async method does not flow back
        _claude.Answer = new ClaudeRun(1, "boom");

        var done = await Service().Handle(Admit("demo"), Token);

        Assert.True(done.IsError);
        Assert.Contains("claude", done.Error.Message, StringComparison.OrdinalIgnoreCase);
        _ = Assert.Single(_slice.Store.Events, static e => e.Envelope.Type == EventTypes.McpRegistered);
    }

    [Fact]
    public async Task A_checkout_that_tracks_the_cursor_file_is_left_alone_and_the_job_names_the_file()
    {
        await Start();
        _caller = _slice.Callers.EnterService("test");   // after the await: the ambient caller of an async method does not flow back
        _ = _host.WithTracked("/work/demo/.cursor/mcp.json");
        _host.Files["/work/demo/.cursor/mcp.json"] = "{}";

        var done = await Service().Handle(Admit("demo"), Token);

        Assert.True(done.IsError);
        Assert.Contains(".cursor/mcp.json", done.Error.Message, StringComparison.Ordinal);
        Assert.Equal("{}", _host.Files["/work/demo/.cursor/mcp.json"]);
    }

    private RegisterMcpJobService Service() =>
        new(_resolved, new AutomaticMcpRegistration(new RegisterMcpService(_slice.Profiles, new FakeRepoLocator(), _endpoint, _claude, _host) { WriteCodexTrust = false }), _slice.Profiles, new FakeJobContextAccessor(),
            new McpEvents(_slice.Held, _slice.Store, _slice.Events, _slice.Callers));

    private RegisterMcpJobRequest Admit(string repo)
    {
        var request = new RegisterMcpJobRequest(repo);
        _ = new RegisterMcpAdmission(_slice.Profiles, _resolved).Admit(request, Token).AsTask().GetAwaiter().GetResult();
        return request;
    }

    private async Task Start()
    {
        _ = await _slice.HoldLease();
        var live = SliceHarness.Profile();
        _ = await _slice.Profiles.Save(live with { Profile = live.Profile with { Repos = [new RepoEntry("demo", "/work/demo")] } }, Token);
    }
}
