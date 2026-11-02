using System.Text.Json;
using Aw.Context.Testing.Engine;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.RunDoctor;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Doctor 'context:linear' (context spec §8.8): configured or not, the teams, and whether the token pointer resolves. No call to Linear.</summary>
public sealed class LinearDoctorTests
{
    private const string Named = """{"sources":{"linear":{"token":"env:LINEAR_KEY","teams":["ENG","APP"]}}}""";
    private static readonly PointerSecrets _noSecrets = new(_ => null, new FakeToolRunner(ToolRun.Missing));
    private static readonly PointerSecrets _keyed = new(name => name == "LINEAR_KEY" ? "lin_api_live-key-0123" : null, new FakeToolRunner(ToolRun.Missing));
    private static readonly RepoEntry[] _repos = [new RepoEntry("split-panes", "/repos/split-panes")];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task No_approved_profile_is_ok_not_configured()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _noSecrets).Check(approved: null, Token);

        Assert.Equal(new DoctorCheck("context:linear", CheckState.Ok, "not configured (no profile is approved)", Fix: null), check);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_profile_with_no_linear_entry_is_ok_not_configured()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _noSecrets).Check(With("{}", _repos), Token);

        Assert.Equal((CheckState.Ok, "not configured (the approved profile names no context.sources.linear.token)"), (check.State, check.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_named_linear_with_no_repo_mapped_warns_that_no_source_runs()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _keyed).Check(With(Named), Token);

        Assert.Equal((CheckState.Warn, "the approved profile maps no repo, so no context source runs, linear included"), (check.State, check.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_pointer_that_does_not_resolve_fails_with_its_SND_SECRET_reason_and_the_guide()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _noSecrets).Check(With(Named, _repos), Token);

        Assert.Equal(CheckState.Fail, check.State);
        Assert.StartsWith("SND-SECRET-002: The env:LINEAR_KEY secret doesn't resolve", check.Detail, StringComparison.Ordinal);
        Assert.Contains("docs/sindri/linear.md", check.Fix, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_resolving_pointer_is_ok_naming_the_teams_and_never_the_key()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _keyed).Check(With(Named, _repos), Token);

        Assert.Equal(new DoctorCheck("context:linear", CheckState.Ok, "configured: teams ENG, APP; env:LINEAR_KEY resolves (not probed: sindri context sources status reads Linear)", Fix: null), check);
        Assert.DoesNotContain("lin_api_live-key-0123", check.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_teams_reads_as_every_team_the_key_can_see()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _keyed).Check(With("""{"sources":{"linear":{"token":"env:LINEAR_KEY"}}}""", _repos), Token);

        Assert.Equal((CheckState.Ok, "configured: every team the key can see; env:LINEAR_KEY resolves (not probed: sindri context sources status reads Linear)"), (check.State, check.Detail));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_setting_the_engine_refuses_fails_with_its_code()
    {
        var check = await new LinearDoctor(FakeContextEngine.Instance, _keyed).Check(With("""{"sources":{"linear":{"token":"env:LINEAR_KEY","url":"http://linear.example/graphql"}}}""", _repos), Token);

        Assert.Equal(CheckState.Fail, check.State);
        Assert.StartsWith("SND-CONTEXT-070:", check.Detail, StringComparison.Ordinal);
    }

    private static LoadedProfile With(string context, IReadOnlyList<RepoEntry>? repos = null)
    {
        var profile = new SindriProfile(1, "thor", Repos: repos, Context: JsonDocument.Parse(context).RootElement.Clone());
        return new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml");
    }
}
