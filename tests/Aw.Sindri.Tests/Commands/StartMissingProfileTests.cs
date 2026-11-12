using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.GetStatus;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Profile;
using Aw.Sindri.Tests.Server;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>Issue 3114: bare 'sindri' on a home with no profile.yaml says setup isn't finished and exits 0; a profile that exists but won't parse is still a problem and exits 1.</summary>
public sealed class StartMissingProfileTests : IDisposable
{
    private static readonly ControlEndpoint _endpoint = new(new Uri("http://127.0.0.1:7410"), 42, 3);
    private readonly SliceHarness _h = new();
    private readonly NothingRunning _probe = new();
    private readonly SteppedClock _clock = new(new FakeTimeProvider(KnownInstants.Epoch));
    private readonly ControlPlaneClient _client = new(new NothingRunning(), SliceHarness.Json);

    public void Dispose() => _client.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<GetStatusResponse> StatusFor(Aw.Kernel.Result<Aw.Sindri.Core.Profile.LoadedProfile> live)
    {
        _h.Profiles.Live = live;
        var service = new GetStatusService(_h.Held, new ProfileReads(_h.Profiles, _h.Profiles), _h.Store, new ProjectorProgress(_h.Clock),
            new HealthServiceRows(new InMemoryViewStore(SliceHarness.Json), []), new InMemorySessionSnapshot());
        return (await service.Handle(new GetStatusRequest(), Token)).Value;
    }

    private static string Render(GetStatusResponse status) => SummaryText.Render(status, _endpoint, ui: null).ReplaceLineEndings("\n");

    private async Task<int> Exit(GetStatusResponse status)
    {
        var command = new StartCommand(_probe, new FakeControlPlaneLauncher(), _client, new Output(TextWriter.Null, TextWriter.Null, SliceHarness.Json), _clock);
        return await command.Report(status, _endpoint);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_profile_prints_not_set_up_yet_and_points_at_the_app_and_sindri_setup()
    {
        var text = Render(await StatusFor(SindriErrors.ProfileMissing.With("/home/x/profile.yaml doesn't exist")));

        Assert.Contains("  profile   not set up yet: finish setup in the app (or 'sindri setup' to see the steps)\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("docs/csharp/sindri.md", text, StringComparison.Ordinal);
        Assert.DoesNotContain("then and", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_profile_makes_the_ready_line_say_setup_isnt_finished()
    {
        var text = Render(await StatusFor(SindriErrors.ProfileMissing.With("/home/x/profile.yaml doesn't exist")));

        Assert.Contains("  ready     running; setup isn't finished\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("has a problem", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_missing_profile_is_not_exit_1()
    {
        var exit = await Exit(await StatusFor(SindriErrors.ProfileMissing.With("/home/x/profile.yaml doesn't exist")));

        Assert.NotEqual(1, exit);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_that_will_not_parse_is_still_a_problem_and_exit_1()
    {
        var status = await StatusFor(SindriErrors.ProfileInvalid.With("/home/x/profile.yaml: bad yaml"));

        Assert.Contains("  ready     running; the live profile file has a problem\n", Render(status), StringComparison.Ordinal);
        Assert.Equal(1, await Exit(status));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_real_missing_profile_message_has_no_then_and_typo_and_keeps_the_profile_init_fix()
    {
        using var files = new ProfileFiles();

        var loaded = await files.Source.Load(Token);

        Assert.True(loaded.IsError);
        Assert.Equal(SindriErrors.ProfileMissing.Code, loaded.Error.Code);
        Assert.DoesNotContain("then and", loaded.Error.Message, StringComparison.Ordinal);
        Assert.Contains("sindri profile init", loaded.Error.Message, StringComparison.Ordinal);
    }

    private sealed class NothingRunning : IControlPlaneProbe
    {
        public ValueTask<Aw.Kernel.Result<(ControlEndpoint Endpoint, Ping Ping)>> Check(CancellationToken cancellationToken) =>
            ValueTask.FromResult<Aw.Kernel.Result<(ControlEndpoint Endpoint, Ping Ping)>>(SindriErrors.NotRunning.With("nothing answers"));
    }
}
