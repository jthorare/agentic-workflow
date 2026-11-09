using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Resources;
using Aw.Sindri.Core.Sandbox.Plan;

namespace Aw.Sindri.Tests.Resources;

public sealed class ResourcePlannerTests
{
    private const string Root = "/home/x/resources";
    private const long Gib = 1L << 30;

    private static ResourceOwner Owner(string text) => ResourceOwner.Parse(text).Value;

    [Fact, Trait("Lane", "unit")]
    public void A_spec_with_a_database_and_a_disk_yields_env_names_and_two_rw_mounts()
    {
        var spec = new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch-pad", "512MiB")], null, []);

        var plan = ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root).Value;

        Assert.Equal($"{Root}/workflow/triage/state.db", plan.Env[ResourceEnv.Database("state")]);
        Assert.Equal($"{Root}/workflow/triage/scratch-pad", plan.Env["SINDRI_DISK_SCRATCH_PAD"]);
        Assert.Equal([new ResourceMount("state", $"{Root}/workflow/triage/state.db"), new ResourceMount("scratch-pad", $"{Root}/workflow/triage/scratch-pad")], plan.Mounts);
        Assert.Equal([new PlannedResource(ResourceKind.Database, "state", $"{Root}/workflow/triage/state.db", null), new PlannedResource(ResourceKind.Disk, "scratch-pad", $"{Root}/workflow/triage/scratch-pad", 512L << 20)], plan.Create);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_env_names_are_the_public_constants()
    {
        Assert.Equal("SINDRI_DB_", ResourceEnv.DatabasePrefix);
        Assert.Equal("SINDRI_DISK_", ResourceEnv.DiskPrefix);
        Assert.Equal("SINDRI_CONNECTION_", ResourceEnv.ConnectionPrefix);
        Assert.Equal("SINDRI_CONNECTION_GITHUB", ResourceEnv.Connection("github"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_connection_adds_its_name_to_the_env_and_no_mount()
    {
        var spec = new ResourceSpec([], [], null, [new ConnectionResource("github")]);

        var plan = ResourcePlanner.Plan(Owner("toolkit:forge"), spec, Root).Value;

        Assert.Equal("github", plan.Env["SINDRI_CONNECTION_GITHUB"]);
        Assert.Empty(plan.Mounts);
        Assert.Empty(plan.Create);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_owner_gets_its_own_folder()
    {
        var spec = new ResourceSpec([new DatabaseResource("state")], [], null, []);

        var plan = ResourcePlanner.Plan(Owner("node:triage/fetch"), spec, Root).Value;

        Assert.Equal($"{Root}/node/triage/fetch/state.db", plan.Env["SINDRI_DB_STATE"]);
    }

    private static PlanLimits Resolve(ResourceLimitOverride? over) =>
        LimitsResolver.Resolve(new LimitsInput(new SandboxLimits(Memory: "8GiB", Cpu: "25%"), AttendedSettings: null, Attended: false, StepTimeout: TimeSpan.FromHours(4), LongMax: null, RamBytes: 64 * Gib, Cores: 4, Override: over));

    [Fact, Trait("Lane", "unit")]
    public void A_sandbox_size_above_the_profile_is_clamped_by_the_resolver_and_one_below_it_is_kept()
    {
        var big = new ResourceSpec([], [], new SandboxResource(Cpu: "50%", Memory: "64GiB", Wall: "45m"), []);
        var small = new ResourceSpec([], [], new SandboxResource(Cpu: "10%", Memory: "2GiB", Wall: null), []);

        var clamped = Resolve(ResourcePlanner.Plan(Owner("workflow:triage"), big, Root).Value.Limits);
        var kept = Resolve(ResourcePlanner.Plan(Owner("workflow:triage"), small, Root).Value.Limits);

        Assert.Equal(8 * Gib, clamped.MemoryMaxBytes);
        Assert.Equal(100, clamped.CpuQuotaPercent);   // 4 cores x the profile's 25%, not 4 x the declared 50%
        Assert.Equal(2 * Gib, kept.MemoryMaxBytes);
        Assert.Equal(40, kept.CpuQuotaPercent);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_declared_wall_becomes_the_step_timeout_and_only_tightens()
    {
        var spec = new ResourceSpec([], [], new SandboxResource(Cpu: null, Memory: null, Wall: "45m"), []);

        var limits = ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root).Value.Limits;

        Assert.Equal(new ResourceLimitOverride(null, null, TimeSpan.FromMinutes(45)), limits);
        Assert.True(Resolve(limits).Wall < Resolve(null).Wall);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_sandbox_declared_overrides_nothing()
    {
        Assert.Null(ResourcePlanner.Plan(Owner("workflow:triage"), ResourceSpec.Empty, Root).Value.Limits);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("4g")]
    [InlineData("512m")]
    [InlineData("lots")]
    public void A_sandbox_memory_the_size_parser_would_reject_is_invalid_here_not_in_the_resolver(string memory)
    {
        var spec = new ResourceSpec([], [], new SandboxResource(Cpu: null, Memory: memory, Wall: null), []);

        Assert.Equal("SND-RES-006", ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_databases_with_one_name_are_a_name_clash()
    {
        var spec = new ResourceSpec([new DatabaseResource("state"), new DatabaseResource("state")], [], null, []);

        var result = ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root);

        Assert.Equal("SND-RES-002", result.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("../evil")]
    [InlineData("Up")]
    [InlineData("a/b")]
    [InlineData("")]
    public void A_name_that_is_not_lowercase_letters_digits_and_dashes_is_invalid(string name)
    {
        var spec = new ResourceSpec([new DatabaseResource(name)], [], null, []);

        Assert.Equal("SND-RES-006", ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_disk_size_that_is_not_a_size_is_invalid()
    {
        var spec = new ResourceSpec([], [new DiskResource("scratch", "lots")], null, []);

        Assert.Equal("SND-RES-006", ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root).Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("workflow:../x")]
    [InlineData("node:only-slug")]
    [InlineData("workflow:a/b")]
    [InlineData("thing:x")]
    [InlineData("nocolon")]
    public void An_owner_that_could_leave_the_resources_root_does_not_parse(string text)
    {
        Assert.Equal("SND-RES-006", ResourceOwner.Parse(text).Error.Code);
    }
}
