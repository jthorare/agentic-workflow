using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Profile;

/// <summary>
/// The profile's 'workflow:' keys (engine spec S1-D11): 'deskTimeout' (default 2d) and 'eventHold' (default 24h), each
/// a duration; a profile without the block keeps its canonical JSON, so an approval made before P4 still holds.
/// </summary>
public sealed class WorkflowSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    [Fact, Trait("Lane", "unit")]
    public void Workflow_times_default_to_2d_and_24h_and_take_what_the_profile_sets()
    {
        Assert.Equal((TimeSpan.FromDays(2), TimeSpan.FromHours(24)), Times(WorkflowTimes.Of(_valid)));
        Assert.Equal((TimeSpan.FromMinutes(1), TimeSpan.FromHours(24)), Times(WorkflowTimes.Of(_valid with { Workflow = new("1m") })));
        Assert.Equal((TimeSpan.FromDays(2), TimeSpan.FromHours(6)), Times(WorkflowTimes.Of(_valid with { Workflow = new(EventHold: "6h") })));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("30s", null, "workflow.deskTimeout: '30s' is not a duration")]
    [InlineData(null, "1 day", "workflow.eventHold: '1 day' is not a duration")]
    public void A_workflow_key_that_isnt_a_duration_names_its_key_path(string? desk, string? hold, string message)
    {
        var error = ProfileRules.Check(_valid with { Workflow = new(desk, hold) }).Error;

        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.StartsWith(message, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_the_workflow_keys_and_a_profile_without_them_keeps_its_canonical_json()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(["workflow.deskTimeout"], ProfileDiff.Between(live, live with { Workflow = new("1m") }));
        Assert.DoesNotContain("workflow", Encoding.UTF8.GetString(AwJson.Serialize(live, SliceHarness.Json)), StringComparison.Ordinal);
        Assert.DoesNotContain("eventHold", Encoding.UTF8.GetString(AwJson.Serialize(live with { Workflow = new("1m") }, SliceHarness.Json)), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_dev_profile_matches_the_schema_and_sets_a_1m_desk_timeout()
    {
        using var files = new ProfileFiles();
        files.Write(ShippedRecipes.Read("dev/profile.yaml"));

        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);

        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal(TimeSpan.FromMinutes(1), WorkflowTimes.Of(loaded.Value.Profile).DeskTimeout);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_worker_reads_the_times_from_the_live_profile_and_defaults_when_it_doesnt_load()
    {
        var profiles = new InMemoryProfiles();
        var times = new ProfileWorkflowTimes(profiles, profiles);
        var ct = TestContext.Current.CancellationToken;

        var missing = await times.Read(ct);
        profiles.Live = new LoadedProfile(_valid with { Workflow = new("1m", "2h") }, ProfileHash.From(new string('a', 64)), "/home/profile.yaml");
        var set = await times.Read(ct);

        Assert.Equal(WorkflowTimes.Defaults, missing);
        Assert.Equal(new WorkflowTimes(TimeSpan.FromMinutes(1), TimeSpan.FromHours(2)), set);
    }

    private static (TimeSpan, TimeSpan) Times(WorkflowTimes times) => (times.DeskTimeout, times.EventHold);
}
