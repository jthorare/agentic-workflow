using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Tests.Resources;

public sealed class ResourcePlannerTests
{
    private const string Root = "/home/x/resources";
    private static readonly ResourceBoxLimits _box = new(8L << 30, 100, TimeSpan.FromHours(4));

    private static ResourceOwner Owner(string text) => ResourceOwner.Parse(text).Value;

    [Fact, Trait("Lane", "unit")]
    public void A_spec_with_a_database_and_a_disk_yields_env_names_and_two_rw_mounts()
    {
        var spec = new ResourceSpec([new DatabaseResource("state")], [new DiskResource("scratch-pad", "512MiB")], null, []);

        var plan = ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root, _box).Value;

        Assert.Equal($"{Root}/workflow/triage/state.db", plan.Env[ResourceEnv.Database("state")]);
        Assert.Equal($"{Root}/workflow/triage/scratch-pad", plan.Env["SINDRI_DISK_SCRATCH_PAD"]);
        Assert.Equal([new ResourceMount($"{Root}/workflow/triage/state.db", true), new ResourceMount($"{Root}/workflow/triage/scratch-pad", true)], plan.Mounts);
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

        var plan = ResourcePlanner.Plan(Owner("toolkit:forge"), spec, Root, _box).Value;

        Assert.Equal("github", plan.Env["SINDRI_CONNECTION_GITHUB"]);
        Assert.Empty(plan.Mounts);
        Assert.Empty(plan.Create);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_node_owner_gets_its_own_folder()
    {
        var spec = new ResourceSpec([new DatabaseResource("state")], [], null, []);

        var plan = ResourcePlanner.Plan(Owner("node:triage/fetch"), spec, Root, _box).Value;

        Assert.Equal($"{Root}/node/triage/fetch/state.db", plan.Env["SINDRI_DB_STATE"]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sandbox_size_above_the_box_is_clamped_and_one_below_it_is_kept()
    {
        var spec = new ResourceSpec([], [], new SandboxResource(Cpu: "50%", Memory: "64GiB", Wall: "45m"), []);

        var limits = ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root, _box).Value.Limits;

        Assert.Equal(8L << 30, limits.MemoryBytes);
        Assert.Equal(50, limits.CpuPercent);
        Assert.Equal(TimeSpan.FromMinutes(45), limits.Wall);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_sandbox_declared_overrides_nothing()
    {
        var limits = ResourcePlanner.Plan(Owner("workflow:triage"), ResourceSpec.Empty, Root, _box).Value.Limits;

        Assert.Equal(new ResourceLimitOverrides(null, null, null), limits);
    }

    [Fact, Trait("Lane", "unit")]
    public void Two_databases_with_one_name_are_a_name_clash()
    {
        var spec = new ResourceSpec([new DatabaseResource("state"), new DatabaseResource("state")], [], null, []);

        var result = ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root, _box);

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

        Assert.Equal("SND-RES-006", ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root, _box).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_disk_size_that_is_not_a_size_is_invalid()
    {
        var spec = new ResourceSpec([], [new DiskResource("scratch", "lots")], null, []);

        Assert.Equal("SND-RES-006", ResourcePlanner.Plan(Owner("workflow:triage"), spec, Root, _box).Error.Code);
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
