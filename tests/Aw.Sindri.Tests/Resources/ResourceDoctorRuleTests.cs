using Aw.Kernel.Testing;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Resources;

namespace Aw.Sindri.Tests.Resources;

[Lane(TestLane.Unit)]
public sealed class ResourceDoctorRuleTests
{
    private static readonly ResourceOwner _owner = ResourceOwner.Parse("workflow:triage").Value;

    private static ResourceEntry Db(string name, long size) => new(_owner, ResourceKind.Database, name, size, $"/r/{name}.db");

    private static ResourceEntry Disk(string name, long size) => new(_owner, ResourceKind.Disk, name, size, $"/r/{name}");

    [Fact]
    public void No_resources_is_ok_and_says_so()
    {
        var check = ResourceDoctorRule.Check([], new Dictionary<string, long>(StringComparer.Ordinal));

        Assert.Equal(("resources", CheckState.Ok), (check.Name, check.State));
        Assert.Equal("no resources provisioned", check.Detail);
    }

    [Fact]
    public void Resources_under_their_limits_are_ok_with_the_count_and_the_total_size()
    {
        var limits = new Dictionary<string, long>(StringComparer.Ordinal) { [ResourceDoctorRule.LimitKey(_owner, "scratch")] = 10L << 20 };

        var check = ResourceDoctorRule.Check([Db("state", 1L << 20), Disk("scratch", 2L << 20)], limits);

        Assert.Equal(CheckState.Ok, check.State);
        Assert.Equal("2 resources, 3MiB in all", check.Detail);
    }

    [Fact]
    public void A_disk_at_ninety_percent_of_its_limit_warns_and_names_the_disk_and_both_sizes()
    {
        var limits = new Dictionary<string, long>(StringComparer.Ordinal) { [ResourceDoctorRule.LimitKey(_owner, "scratch")] = 10L << 20 };

        var check = ResourceDoctorRule.Check([Disk("scratch", 9L << 20), Disk("other", 1L << 20)], limits);

        Assert.Equal(CheckState.Warn, check.State);
        Assert.Contains("'scratch' of workflow:triage", check.Detail, StringComparison.Ordinal);
        Assert.Contains("9MiB of 10MiB", check.Detail, StringComparison.Ordinal);
        Assert.NotNull(check.Fix);
    }

    [Fact]
    public void A_disk_with_no_known_limit_never_warns()
    {
        var check = ResourceDoctorRule.Check([Disk("scratch", 900L << 20)], new Dictionary<string, long>(StringComparer.Ordinal));

        Assert.Equal(CheckState.Ok, check.State);
    }
}
