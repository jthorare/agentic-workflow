using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Forge;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class ForgeDoctorChecksTests
{
    [Fact]
    public async Task No_forge_is_ok()
    {
        var check = await Run(forge: null);

        Assert.Equal((CheckState.Ok, "no forge configured"), (check.State, check.Detail));
    }

    [Fact]
    public async Task A_forge_that_answers_shows_the_host_and_the_login()
    {
        var check = await Run(new ForgeSettings(Adapter: "file", Hosts: ["forge.test"]));

        Assert.Equal((CheckState.Ok, "forge forge.test as sindri"), (check.State, check.Detail));
    }

    [Fact]
    public async Task An_unset_env_credential_fails_naming_the_variable_and_asks_nothing()
    {
        var fake = new FakePullRequests(["github.com"]);

        var check = await Run(new ForgeSettings(Adapter: "github", Hosts: ["github.com"], Credential: "env:MY_FORGE_TOKEN"), fake, _ => null);

        Assert.Equal(CheckState.Fail, check.State);
        Assert.Contains("env:MY_FORGE_TOKEN", check.Detail, StringComparison.Ordinal);
        Assert.NotNull(check.Fix);
        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task A_rejected_credential_fails_without_its_value()
    {
        var fake = new FakePullRequests(["github.com"]) { SelfError = SindriErrors.ForgeRefused.With("AW-FORGE-003: the forge credential is missing or was rejected") };

        var check = await Run(new ForgeSettings(Adapter: "github", Hosts: ["github.com"], Credential: "env:MY_FORGE_TOKEN"), fake, name => name == "MY_FORGE_TOKEN" ? "tok-secret-123" : null);

        Assert.Equal(CheckState.Fail, check.State);
        Assert.Contains("env:MY_FORGE_TOKEN", check.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("tok-secret-123", check.Detail + check.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_command_credential_is_named_by_its_program_alone()
    {
        var fake = new FakePullRequests(["github.com"]) { SelfError = SindriErrors.ForgeRefused.With("rejected") };

        var check = await Run(new ForgeSettings(Adapter: "github", Hosts: ["github.com"], Credential: "command:gh auth token --hostname github.com"), fake, _ => null);

        Assert.Contains("command:gh", check.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("--hostname", check.Detail + check.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unreachable_forge_warns()
    {
        var fake = new FakePullRequests(["forge.test"]) { SelfError = SindriErrors.ForgeUnavailable.With("AW-FORGE-005: down") };

        var check = await Run(new ForgeSettings(Adapter: "file", Hosts: ["forge.test"]), fake);

        Assert.Equal(CheckState.Warn, check.State);
    }

    [Fact]
    public async Task A_forge_that_never_answers_warns_after_the_limit()
    {
        var fake = new FakePullRequests(["forge.test"]) { BeforeSelf = token => Task.Delay(Timeout.Infinite, token) };

        var check = await Run(new ForgeSettings(Adapter: "file", Hosts: ["forge.test"]), fake, limit: TimeSpan.FromMilliseconds(50));

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("did not answer", check.Detail, StringComparison.Ordinal);
    }

    private static async Task<DoctorCheck> Run(ForgeSettings? forge, FakePullRequests? fake = null, Func<string, string?>? environment = null, TimeSpan? limit = null)
    {
        var profiles = new InMemoryProfiles();
        var profile = new SindriProfile(1, "thor", Workflow: forge is null ? null : new WorkflowSettings(Forge: forge));
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHash.From(new string('b', 64)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
        var checks = new ForgeDoctorChecks(profiles, fake ?? new FakePullRequests(["forge.test"]), environment ?? (_ => null), limit ?? TimeSpan.FromSeconds(5));
        return Assert.Single(await checks.Run(TestContext.Current.CancellationToken));
    }
}
