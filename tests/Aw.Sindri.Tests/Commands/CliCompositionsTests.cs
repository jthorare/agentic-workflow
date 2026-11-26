using System.Text.Json;
using Aw.Kernel.Telemetry;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Skills;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>#3727: which container a verb runs in, and that the client one holds what Dispatch resolves and nothing heavy.</summary>
public sealed class CliCompositionsTests
{
    private static readonly string _stateRoot = Path.Combine(Path.GetTempPath(), "aw-sindri-di-test");

    [Theory, Trait("Lane", "unit")]
    [InlineData("help", "None")]
    [InlineData("--help", "None")]
    [InlineData("-h", "None")]
    [InlineData("", "Client")]
    [InlineData("--headless", "Client")]
    [InlineData("wf list --json", "Client")]
    [InlineData("wf run demo", "Client")]
    [InlineData("wf validate --slug demo", "Client")]
    [InlineData("wf validate graph.json", "Full")]
    [InlineData("status --json", "Client")]
    [InlineData("items --json", "Client")]
    [InlineData("todo note set x", "Full")]
    [InlineData("context search auth", "Client")]
    [InlineData("context eval backtest", "Client")]
    [InlineData("context eval run", "Full")]
    [InlineData("context pin set auth", "Full")]
    [InlineData("context page set home", "Full")]
    [InlineData("context page list", "Client")]
    [InlineData("index sync", "Client")]
    [InlineData("index connect github", "Full")]
    [InlineData("setup --apply", "Client")]
    [InlineData("setup", "Full")]
    [InlineData("repo add /work/demo", "Client")]
    [InlineData("repo status", "Full")]
    [InlineData("launch probe", "Client")]
    [InlineData("launch init", "Full")]
    [InlineData("profile set a.b c", "Full")]
    [InlineData("doctor", "Full")]
    [InlineData("restart", "Full")]
    [InlineData("workflow list", "Full")]
    [InlineData("frobnicate", "Full")]
    public void A_verb_runs_in_the_container_its_dispatch_arm_needs(string line, string expected)   // why: the enum is internal, so a public theory takes its name
    {
        var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(Enum.Parse<CliComposition>(expected), CliCompositions.For(args));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_client_composition_validates_and_resolves_what_Dispatch_resolves()
    {
        using var provider = ComposeClient().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetRequiredService<TelemetryRuntime>();   // so the container disposes it, as CommandTable does
        using var scope = provider.CreateScope();

        Type[] dispatch = [typeof(JsonSerializerOptions), typeof(TimeProvider), typeof(IControlPlaneProbe), typeof(IControlPlaneLauncher), typeof(IHumanTokenStore), typeof(IApprovedProfile), typeof(IRepoLocator)];

        Assert.All(dispatch, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_client_composition_does_not_start_telemetry_for_what_Dispatch_resolves()
    {
        using var provider = ComposeClient().BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();
        Type[] dispatch = [typeof(JsonSerializerOptions), typeof(TimeProvider), typeof(IControlPlaneProbe), typeof(IControlPlaneLauncher), typeof(IHumanTokenStore), typeof(IApprovedProfile), typeof(IRepoLocator)];

        Assert.All(dispatch, t => Assert.NotNull(scope.ServiceProvider.GetRequiredService(t)));

        Assert.False(provider.GetRequiredService<TelemetryStart>().Started);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_AW_LOG_LEVEL_still_starts_telemetry_in_the_client_composition_so_its_warning_prints()
    {
        using var provider = Composition.ComposeClient(new ServiceCollection(), static name => name == "AW_LOG_LEVEL" ? "loud" : Environment(name), new FakeTimeProvider()).BuildServiceProvider();

        Assert.True(provider.GetRequiredService<TelemetryStart>().Started);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_full_composition_starts_telemetry_at_once()
    {
        using var provider = Composition.Compose(new ServiceCollection(), Environment, new FakeTimeProvider()).BuildServiceProvider();

        Assert.True(provider.GetRequiredService<TelemetryStart>().Started);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_client_composition_holds_no_store_and_no_skill_blocks()
    {
        var services = ComposeClient();

        Assert.DoesNotContain(services, static d => d.ServiceType == typeof(ILedgerStore));
        Assert.DoesNotContain(services, static d => d.ServiceType == typeof(ISkillBlocks));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_client_registration_is_in_the_full_composition_with_the_same_lifetime()
    {
        var full = Composition.Compose(new ServiceCollection(), Environment, new FakeTimeProvider());

        Assert.All(ComposeClient(), client => Assert.Contains(full, d => d.ServiceType == client.ServiceType && d.Lifetime == client.Lifetime));
    }

    private static IServiceCollection ComposeClient() => Composition.ComposeClient(new ServiceCollection(), Environment, new FakeTimeProvider());

    private static string? Environment(string name) => name switch { "AW_STATE_DIR" => _stateRoot, _ => null };
}
