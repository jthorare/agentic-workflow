using System.Text.Json;
using Aw.Meta.Core.Health;
using Aw.Meta.Features.Health;
using Aw.Meta.Host;
using Aw.Meta.Tests.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Meta.Tests.Cli;

public sealed class HealthJsonTests
{
    private static HealthResponse Response(string? oomPolicy) => new(
        Groups: [],
        ProcessCount: 0,
        Host: new HostResources(MemTotalKb: 0, MemAvailableKb: 0, SwapUsedKb: 0, DiskFreeBytes: 0),
        Orphans: [],
        Killed: [],
        Duplicates: [],
        MissingLanes: null,
        HeavyLocks: [],
        MemoryLow: false,
        Attention: false)
    {
        OomPolicy = oomPolicy,
    };

    private static string Serialize(HealthResponse response)
    {
        using var root = new TempDirectory();
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_STATE_DIR"] = root.Path };
        var provider = Composition.Compose(new ServiceCollection(), name => env.GetValueOrDefault(name), new FakeTimeProvider()).BuildServiceProvider();
        using (provider)
        {
            return JsonSerializer.Serialize(response, provider.GetRequiredService<JsonSerializerOptions>());
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void OomPolicy_is_absent_from_the_json_when_null() =>
        Assert.DoesNotContain("oomPolicy", Serialize(Response(oomPolicy: null)), StringComparison.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void OomPolicy_is_present_in_the_json_when_set() =>
        Assert.Contains("\"oomPolicy\":\"warn\"", Serialize(Response(oomPolicy: "warn")), StringComparison.Ordinal);
}
