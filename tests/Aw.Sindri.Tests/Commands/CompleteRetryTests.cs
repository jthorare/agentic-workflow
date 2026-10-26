using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri workflow complete' from a launched session waits for a restarting control plane (issue #1000); a human's call, and any refusal, do not.</summary>
[Lane(TestLane.Unit)]
public sealed class CompleteRetryTests : IDisposable
{
    private static readonly FakeControlPlaneProbe _probe = FakeControlPlaneProbe.For(new ControlEndpoint(new Uri("http://127.0.0.1:7410"), 42, 3), new Ping(42, 3));
    private static readonly string[] _args = ["ITM-1/step/1", "--run", "1.1", "--outcome", "done", "--nonce", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"];
    private readonly StepClock _clock = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private int _ensured;

    public void Dispose()
    {
        _out.Dispose();
        _error.Dispose();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<int> Complete(RoutedApi api, int downFor, bool launched)
    {
        var client = new ControlPlaneClient(_probe, SliceHarness.Json,
            CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), InMemoryHumanTokenStore.WithToken(), _ => ValueTask.FromResult((RepoName?)null)), handler: api);
        var desk = new RecipeDesk(client, TextReader.Null, _ => Task.FromResult(++_ensured <= downFor ? SindriErrors.NotRunning.With("the control plane went away") : null),
            launched ? static name => name == "AW_SINDRI_LAUNCH_SECRET" ? "a-secret" : null : null);
        await using var services = new ServiceCollection().AddSingleton<TimeProvider>(_clock).BuildServiceProvider();
        return await _clock.Drive(CompleteCommand.Run(_args, desk, services, new Output(_out, _error, SliceHarness.Json), Token));
    }

    private static RoutedApi NoSuchInstance() => new RoutedApi().OnRaw("/sindri/workflow/instances", HttpStatusCode.NotFound,
        """{"type":"about:blank","title":"SND-WF-001","status":404,"detail":"'ITM-1/step/1' names no one instance"}""");

    [Fact]
    public async Task A_launched_session_waits_out_a_restart_and_then_reaches_the_control_plane()
    {
        var api = NoSuchInstance();

        var code = await Complete(api, downFor: 2, launched: true);

        Assert.Equal(2, code);   // the refusal after it came back
        Assert.Equal(3, _ensured);
        Assert.Equal(2, _error.ToString().Split('\n').Count(static l => l.StartsWith("SND-API-001 ", StringComparison.Ordinal) && l.Contains("retrying in", StringComparison.Ordinal)));
        _ = Assert.Single(api.Calls, static c => c.Path == "/sindri/workflow/instances");   // a refusal is asked once
        Assert.Contains("SND-WF-001", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_control_plane_that_stays_down_ends_in_exit_1_after_the_whole_window()
    {
        var started = _clock.GetUtcNow();

        var code = await Complete(new RoutedApi(), downFor: int.MaxValue, launched: true);

        Assert.Equal(1, code);
        Assert.Equal(ConnectRetry.Default, _clock.GetUtcNow() - started);
        Assert.Contains("SND-API-001 the control plane went away", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_human_without_a_launch_secret_is_told_at_once()
    {
        var code = await Complete(new RoutedApi(), downFor: int.MaxValue, launched: false);

        Assert.Equal(1, code);
        Assert.Equal(1, _ensured);
        Assert.DoesNotContain("retrying", _error.ToString(), StringComparison.Ordinal);
    }
}
