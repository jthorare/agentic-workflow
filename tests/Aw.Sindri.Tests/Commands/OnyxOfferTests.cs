using Aw.Sindri.Core.Onyx;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Commands;

// Spec O6: bare `sindri` at a terminal offers to set Onyx up once; no is remembered; a session or a box that has Onyx is never asked.
public sealed class OnyxOfferTests : IDisposable
{
    private readonly OnyxUpRig _rig = new();
    private readonly FakeManagedOnyx _managed = new();
    private readonly StringWriter _error = new();
    private readonly ServiceProvider _services;
    private int _asked;
    private int _ups;

    public OnyxOfferTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IOnyxWorkspace>(_rig.Workspace)
            .AddSingleton(new OnyxFacts(_rig.Runtime, _rig.Probes, new FakeSindriHome(OnyxUpRig.Home)))
            .AddSingleton<IManagedOnyx>(_managed)
            .BuildServiceProvider();
    }

    private static AgentSession Human { get; } = new(Terminal: true, Prover: false);

    public void Dispose() => _services.Dispose();

    private OnyxOffer Offer(AgentSession session, string? answer, int upExit = 0)
        => new(_services, session, _ =>
        {
            _asked++;
            return Task.FromResult(answer);
        }, _ =>
        {
            _ups++;
            return Task.FromResult(upExit);
        }, _error);

    private Task Run(AgentSession session, string? answer, int upExit = 0) => Offer(session, answer, upExit).Run(TestContext.Current.CancellationToken);

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("y")]
    [InlineData("Yes")]
    public async Task Yes_or_enter_runs_onyx_up(string answer)
    {
        await Run(Human, answer);

        Assert.Equal(1, _asked);
        Assert.Equal(1, _ups);
        Assert.False(_rig.Workspace.Files.ContainsKey(_rig.Paths.Declined));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task End_of_input_neither_starts_onyx_nor_remembers_a_no()
    {
        await Run(Human, answer: null);

        Assert.Equal(1, _asked);
        Assert.Equal(0, _ups);
        Assert.False(_rig.Workspace.Files.ContainsKey(_rig.Paths.Declined));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("n")]
    [InlineData("no")]
    [InlineData("later")]
    public async Task No_is_remembered_and_the_offer_is_not_repeated(string answer)
    {
        await Run(Human, answer);
        await Run(Human, "y");

        Assert.Equal(0, _ups);
        Assert.Equal(1, _asked);
        Assert.True(_rig.Workspace.Files.ContainsKey(_rig.Paths.Declined));
        Assert.Contains("sindri onyx up", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_without_a_terminal_is_never_offered_anything()
    {
        await Run(new AgentSession(Terminal: false, Prover: false), "y");

        Assert.Equal(0, _asked);
        Assert.Equal(0, _ups);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_box_where_onyx_is_set_up_is_not_asked()
    {
        _rig.Workspace.State = new OnyxSetupState(3189, new Dictionary<string, string>(StringComparer.Ordinal) { [OnyxSteps.Start] = "x" });

        await Run(Human, "y");

        Assert.Equal(0, _asked);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_profile_that_names_an_onyx_already_is_not_asked(bool managed)
    {
        (_managed.Managed, _managed.Configured) = (managed, true);

        await Run(Human, "y");

        Assert.Equal(0, _asked);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_setup_that_fails_does_not_stop_sindri_and_says_how_to_continue()
    {
        await Run(Human, "y", upExit: 1);

        Assert.Equal(1, _ups);
        Assert.Contains("starting Sindri anyway", _error.ToString(), StringComparison.Ordinal);
        Assert.False(_rig.Workspace.Files.ContainsKey(_rig.Paths.Declined));
    }
}
