using System.Text;
using System.Text.Json;
using Aw.Kernel.Json;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Features.OnyxAccount;
using Aw.Sindri.Features.OnyxDown;
using Aw.Sindri.Features.OnyxUp;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Host.Onyx;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri onyx up' and 'onyx down' as OnyxVerbs runs them, over the fake rig: who may run them, what they print where, and the exit codes.</summary>
[Collection(ProcessEnvironment.Name)]
public sealed class OnyxUpCommandTests : IDisposable
{
    private const string HandOver = "needs you: signup (run `sindri onyx up` at a terminal)";   // the account stage without a terminal

    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(SindriJsonContext.Default);

    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly OnyxUpRig _rig = new();
    private readonly ServiceProvider _services;
    private readonly FakeOnyxPrompts _prompts = new() { Interactive = false };
    private readonly FakeBrowser _browser = new();
    private readonly FakeOnyxProvisioning _provisioning = new();
    private readonly FakeSecretWriter _secrets = new();
    private readonly FakeOnyxProfileWriter _profile = new();

    public OnyxUpCommandTests()
    {
        _rig.Progress.Forward = new ConsoleOnyxProgress(_error);
        _services = new ServiceCollection()
            .AddSingleton(_rig.Service())
            .AddSingleton<IOnyxProgress>(_rig.Progress)
            .AddSingleton<IService<OnyxAccountRequest, OnyxAccountResponse>>(_ => new OnyxAccountService(_rig.Workspace, _prompts, _browser, new OnyxKeyMinter(_prompts, _provisioning, _secrets, _secrets, new FakeRandomHex(), _rig.Progress), _profile, _rig.Progress))
            .AddSingleton<IBrowser>(_browser)
            .AddSingleton<IOnyxWorkspace>(_rig.Workspace)
            .AddSingleton<Core.Profile.IProfileSource>(new StubProfile())
            .AddSingleton<IService<OnyxDownRequest, OnyxDownResponse>>(
                new OnyxDownService(_rig.Workspace, new OnyxFacts(_rig.Runtime, _rig.Probes, new FakeSindriHome(OnyxUpRig.Home)), _rig.Runtime))
            .BuildServiceProvider();
    }

    public void Dispose() => _services.Dispose();

    private OnyxVerbs Verbs() => new(_services, new Output(_out, _error, _json));

    [Fact, Trait("Lane", "unit")]
    public async Task Up_prints_url_version_and_what_the_account_stage_needs_on_stdout_and_progress_on_stderr()
    {
        var code = await Verbs().Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        var lines = _out.ToString().TrimEnd('\n').Split('\n');
        Assert.Equal(
            [
                "url       http://127.0.0.1:3189",
                "version   v4.9.0",
                "ran       fetch, config, pull, start",
                "skipped   none",
                HandOver,
            ],
            lines,
            StringComparer.Ordinal);
        Assert.Contains("fetch: running\n", _error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Contains("start: running", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("running\n", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_names_the_skipped_steps_on_a_rerun()
    {
        _ = await Verbs().Up(json: false, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();

        _ = await Verbs().Up(json: false, TestContext.Current.CancellationToken);

        Assert.Contains("ran       none", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("skipped   fetch, config, pull, start", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_json_prints_the_response_only()
    {
        var code = await Verbs().Up(json: true, TestContext.Current.CancellationToken);

        var summary = AwJson.Deserialize<OnyxUpSummary>(Encoding.UTF8.GetBytes(_out.ToString()), _json).Value;
        Assert.Equal(0, code);
        Assert.Equal("http://127.0.0.1:3189", summary.Up.Url);
        Assert.True(summary.Account!.NeedsHuman);
        Assert.DoesNotContain("USER_AUTH", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_json_documents_steps_as_an_array_of_step_and_outcome()
    {
        _ = await Verbs().Up(json: true, TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(_out.ToString());
        var steps = document.RootElement.GetProperty("up").GetProperty("steps").EnumerateArray().Select(static e => (e.GetProperty("step").GetString(), e.GetProperty("outcome").GetString())).ToArray();
        Assert.Equal([("fetch", "ran"), ("config", "ran"), ("pull", "ran"), ("start", "ran")], steps);
        Assert.False(document.RootElement.GetProperty("up").TryGetProperty("ran", out _));
        Assert.False(document.RootElement.GetProperty("up").TryGetProperty("skipped", out _));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_hand_over_line_follows_the_response()
    {
        var needsYou = new OnyxUpResponse(3189, "http://127.0.0.1:3189", "v4.9.0", [], [new OnyxStepResult("fetch", OnyxStepOutcome.Ran), new OnyxStepResult("account", OnyxStepOutcome.NeedsHuman)]);
        await using var provider = new ServiceCollection().AddSingleton<IService<OnyxUpRequest, OnyxUpResponse>>(new StubUp(needsYou)).BuildServiceProvider();

        var code = await new OnyxVerbs(provider, new Output(_out, _error, _json)).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("needs you: account", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(HandOver, _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task With_a_terminal_up_continues_with_the_account_shows_the_approval_and_prints_the_connector_checklist()
    {
        _prompts.Interactive = true;
        _prompts.Answer("owner@example.com", "owner-login-value-0042");
        var approvals = new List<string?>();
        var verbs = new OnyxVerbs(_services, new Output(_out, _error, _json), (hash, _, _) =>
        {
            approvals.Add(hash);
            return Task.FromResult(hash is null ? 1 : 0);   // the preview says not approved, then the typed challenge succeeds
        });

        var code = await verbs.Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("key       keychain:onyx/sindri-onyx", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("Linear:", _out.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("owner-login-value-0042", _out.ToString() + _error, StringComparison.Ordinal);
        Assert.Equal(2, approvals.Count);
        Assert.Null(approvals[0]);
        Assert.NotNull(approvals[1]);
        Assert.Contains(_browser.Opened, static u => u.AbsolutePath == "/admin/add-connector");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_declined_approval_leaves_the_section_written_and_says_how_to_approve_later()
    {
        _prompts.Interactive = true;
        _prompts.Answer("owner@example.com", "owner-login-value-0042");
        var verbs = new OnyxVerbs(_services, new Output(_out, _error, _json), (_, _, _) => Task.FromResult(1));

        var code = await verbs.Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("written but not approved yet: run `sindri profile approve`", _out.ToString(), StringComparison.Ordinal);
        Assert.True(_profile.Section is not null);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_json_with_an_inline_approval_keeps_standard_output_one_document()
    {
        _prompts.Interactive = true;
        _prompts.Answer("owner@example.com", "owner-login-value-0042");
        var verbs = new OnyxVerbs(_services, new Output(_out, _error, _json), async (hash, to, ct) =>
        {
            _ = await to.Text(hash is null ? "preview text\n" : "Type 'approve' to approve it: ", 0);
            return hash is null ? 1 : 0;
        });

        var code = await verbs.Up(json: true, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        using var document = JsonDocument.Parse(_out.ToString());
        Assert.Equal("keychain:onyx/sindri-onyx", document.RootElement.GetProperty("account").GetProperty("keyPointer").GetString()?[..25]);
        Assert.Contains("preview text", _error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("preview text", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_approval_the_preview_already_shows_approved_skips_the_typed_challenge()
    {
        _prompts.Interactive = true;
        _prompts.Answer("owner@example.com", "owner-login-value-0042");
        var approvals = new List<string?>();
        var verbs = new OnyxVerbs(_services, new Output(_out, _error, _json), (hash, _, _) =>
        {
            approvals.Add(hash);
            return Task.FromResult(0);
        });

        var code = await verbs.Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        _ = Assert.Single(approvals);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_an_approval_callback_the_offer_is_skipped_and_the_checklist_still_prints()
    {
        _prompts.Interactive = true;
        _prompts.Answer("owner@example.com", "owner-login-value-0042");

        var code = await Verbs().Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("Linear:", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_cannot_be_loaded_counts_as_not_approved_and_says_why()
    {
        await using var provider = new ServiceCollection()
            .AddSingleton(_rig.Service())
            .AddSingleton<IOnyxProgress>(_rig.Progress)
            .AddSingleton<IService<OnyxAccountRequest, OnyxAccountResponse>>(_ => new OnyxAccountService(_rig.Workspace, _prompts, _browser, new OnyxKeyMinter(_prompts, _provisioning, _secrets, _secrets, new FakeRandomHex(), _rig.Progress), _profile, _rig.Progress))
            .AddSingleton<IBrowser>(_browser)
            .AddSingleton<IOnyxWorkspace>(_rig.Workspace)
            .AddSingleton<Core.Profile.IProfileSource>(new BrokenProfile())
            .BuildServiceProvider();
        _prompts.Interactive = true;
        _prompts.Answer("owner@example.com", "owner-login-value-0042");

        var code = await new OnyxVerbs(provider, new Output(_out, _error, _json), (_, _, _) => Task.FromResult(0)).Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("profile unreadable", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("written but not approved yet", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cancelled_account_stage_says_to_run_up_again()
    {
        using var source = new CancellationTokenSource();
        _prompts.Interactive = true;
        var verbs = new OnyxVerbs(_services, new Output(_out, _error, _json), async (_, _, _) =>
        {
            await source.CancelAsync();
            throw new OperationCanceledException(source.Token);
        });
        _prompts.Answer("owner@example.com", "owner-login-value-0042");

        var code = await verbs.Up(json: false, source.Token);

        Assert.Equal(Output.InterruptedExit, code);
        Assert.Contains("interrupted; run `sindri onyx up` again to resume", _error.ToString() + _out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Open_before_setup_says_to_run_up_and_after_the_account_opens_the_connectors_page()
    {
        Assert.Equal(1, await Verbs().Open(TestContext.Current.CancellationToken));
        Assert.Contains("sindri onyx up", _error.ToString(), StringComparison.Ordinal);

        _rig.Workspace.State = new OnyxSetupState(3190, new Dictionary<string, string>(StringComparer.Ordinal));
        Assert.Equal(0, await Verbs().Open(TestContext.Current.CancellationToken));
        Assert.Equal("/auth/signup", Assert.Single(_browser.Opened).AbsolutePath);

        _rig.Workspace.State = new OnyxSetupState(3190, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Signup] = "x" });
        _ = await Verbs().Open(TestContext.Current.CancellationToken);
        Assert.Equal("/admin/add-connector", _browser.Opened[^1].AbsolutePath);
        Assert.Equal(3190, _browser.Opened[^1].Port);
    }

    private sealed class StubProfile : Core.Profile.IProfileSource
    {
        public ValueTask<Kernel.Result<Core.Profile.LoadedProfile>> Load(CancellationToken cancellationToken)
            => ValueTask.FromResult<Kernel.Result<Core.Profile.LoadedProfile>>(new Core.Profile.LoadedProfile(
                new Core.Profile.SindriProfile(Core.Profile.SindriProfile.CurrentSchemaVersion, "tester"),
                Core.Ids.ProfileHash.From(new string('c', 64)),
                "/home/profile.yaml"));
    }

    private sealed class BrokenProfile : Core.Profile.IProfileSource
    {
        public ValueTask<Kernel.Result<Core.Profile.LoadedProfile>> Load(CancellationToken cancellationToken)
            => ValueTask.FromResult<Kernel.Result<Core.Profile.LoadedProfile>>(Core.SindriErrors.InvalidInput.With("profile unreadable"));
    }

    private sealed class StubUp(OnyxUpResponse response) : IService<OnyxUpRequest, OnyxUpResponse>
    {
        public ValueTask<Kernel.Result<OnyxUpResponse>> Handle(OnyxUpRequest request, CancellationToken cancellationToken) => ValueTask.FromResult<Kernel.Result<OnyxUpResponse>>(response);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_maps_SND_ONYX_003_to_exit_1_with_the_fix_text()
    {
        _rig.Probes.FreeBytes = 1L << 30;

        var code = await Verbs().Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-003", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Free space on that volume, then run this again", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_maps_a_busy_lock_to_exit_1()
    {
        using var held = _rig.Workspace.TryLock().Value;

        var code = await Verbs().Up(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-API-009", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_interrupted_mid_pull_exits_130_with_the_resume_line_and_releases_the_lock()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _rig.Runtime.OnCall = operation =>
        {
            if (operation == "Pull")
            {
                source.Cancel();
            }
        };

        var code = await Verbs().Up(json: false, source.Token);

        Assert.Equal(130, code);
        Assert.Contains("interrupted; run `sindri onyx up` again to resume", _error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, _out.ToString());
        Assert.False(_rig.Workspace.LockHeld);
        Assert.DoesNotContain("Up", _rig.Runtime.Calls, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Up_interrupted_in_the_middle_of_a_dot_row_ends_the_row_before_the_resume_line()
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _rig.Runtime.OnCall = operation =>
        {
            if (operation == "Pull")
            {
                _rig.Progress.Tick();
                _rig.Progress.Tick();
                _rig.Progress.Tick();
                source.Cancel();
            }
        };

        _ = await Verbs().Up(json: false, source.Token);

        var error = _error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("...\ninterrupted; run `sindri onyx up` again to resume\n", error, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_interrupted_exits_130_and_releases_the_lock()
    {
        _ = await Verbs().Up(json: false, TestContext.Current.CancellationToken);
        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _rig.Runtime.OnCall = operation =>
        {
            if (operation == "Stop")
            {
                source.Cancel();
            }
        };

        var code = await Verbs().Down(json: true, source.Token);

        Assert.Equal(130, code);
        Assert.Contains("interrupted", _error.ToString(), StringComparison.Ordinal);
        Assert.False(_rig.Workspace.LockHeld);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ctrl_C_and_SIGTERM_are_listened_for_during_onyx_up_and_down_only()
    {
        Assert.True(Interrupt.Streams(["onyx", "up"]));
        Assert.True(Interrupt.Streams(["onyx", "up", "--json"]));
        Assert.True(Interrupt.Streams(["onyx", "down"]));
        Assert.False(Interrupt.Streams(["onyx", "status"]));
        Assert.False(Interrupt.Streams(["onyx"]));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_prints_stopped_and_exits_0()
    {
        _ = await Verbs().Up(json: false, TestContext.Current.CancellationToken);
        _out.GetStringBuilder().Clear();

        var code = await Verbs().Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("Onyx stopped", _out.ToString(), StringComparison.Ordinal);
        Assert.All(_rig.Runtime.Containers, static c => Assert.Equal("exited", c.State, StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_on_a_box_never_set_up_says_so_and_exits_0()
    {
        var code = await Verbs().Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(0, code);
        Assert.Contains("stopped", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("nothing was running", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Down_maps_a_remote_docker_to_exit_1()
    {
        _ = await Verbs().Up(json: false, TestContext.Current.CancellationToken);
        _rig.Runtime.Facts = FakeContainerRuntime.Healthy with { Endpoint = "tcp://10.0.0.5:2375" };

        var code = await Verbs().Down(json: false, TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains("SND-ONYX-010", _error.ToString(), StringComparison.Ordinal);
    }
}
