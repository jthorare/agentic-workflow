using System.Globalization;
using Aw.Packets.App.Features.ValidatePacket;
using Aw.Packets.Core;
using Aw.Packets.Core.Checks;
using Aw.Packets.Testing;

namespace Aw.Packets.Tests.ValidatePacket;

/// <summary>The task limit and the per-rule failure cap.</summary>
public sealed class LimitTests
{
    private static async Task<ValidatePacketResponse> Validate(PacketBuilder packet, InMemoryRepoSnapshot? repo = null)
    {
        var service = new ValidatePacketService(new PacketInspector(
            new InMemoryPacketSource().With("pkt", PacketFiles.Of(packet.Build())),
            repo ?? new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, PacketBuilder.BaseFiles)), new InMemorySizesSource());
        var result = await service.Handle(new ValidatePacketRequest("pkt"), CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private static IReadOnlyList<CheckResult> Failures(ValidatePacketResponse report, string rule) =>
        [.. report.Checks.Where(c => c.Rule == rule && c.State == CheckState.Fail)];

    [Fact, Trait("Lane", "unit")]
    public async Task A_chain_at_the_task_limit_passes()
    {
        var packet = A.Packet();
        for (var i = 0; i < 2_000; i++)
        {
            var id = "t" + i.ToString(CultureInfo.InvariantCulture);
            var task = A.Task(id).Writing("src/a.cs");
            _ = packet.With(i == 0 ? task : task.After("t" + (i - 1).ToString(CultureInfo.InvariantCulture)));
        }

        var report = await Validate(packet);

        Assert.True(report.Passed, string.Join("; ", report.Checks.Where(static c => c.State == CheckState.Fail).Select(static c => c.Rule)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_chain_past_the_old_2000_task_limit_passes()
    {
        var packet = A.Packet();
        for (var i = 0; i < 2_001; i++)
        {
            var task = A.Task("t" + i.ToString(CultureInfo.InvariantCulture)).Writing("src/a.cs");
            _ = packet.With(i == 0 ? task : task.After("t" + (i - 1).ToString(CultureInfo.InvariantCulture)));
        }

        var report = await Validate(packet);

        Assert.True(report.Passed, string.Join("; ", report.Checks.Where(static c => c.State == CheckState.Fail).Select(static c => c.Rule)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rule_reports_at_most_500_failures_and_counts_the_rest()
    {
        var packet = A.Packet();
        for (var i = 0; i < 40; i++)
        {
            _ = packet.With(A.Task("t" + i.ToString(CultureInfo.InvariantCulture)).Writing("src/a.cs"));
        }

        var failures = Failures(await Validate(packet), "PKT-SCOPE-002");

        Assert.Equal(500, failures.Count);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_chain_over_a_large_repo_at_the_task_limit_passes()
    {
        var files = new HashSet<string>(PacketBuilder.BaseFiles, StringComparer.Ordinal);
        for (var i = 0; i < 20_000; i++)
        {
            _ = files.Add("src/f" + i.ToString(CultureInfo.InvariantCulture) + ".cs");
        }

        var packet = A.Packet();
        for (var i = 0; i < 2_000; i++)
        {
            var task = A.Task("t" + i.ToString(CultureInfo.InvariantCulture)).Writing("src/**");
            _ = packet.With(i == 0 ? task : task.After("t" + (i - 1).ToString(CultureInfo.InvariantCulture)));
        }

        var report = await Validate(packet, new InMemoryRepoSnapshot().With(PacketBuilder.BaseSha, files));

        Assert.True(report.Passed);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_omitted_record_counts_the_unlisted_failures_and_gives_a_generic_fix()
    {
        var packet = A.Packet();
        for (var i = 0; i < 40; i++)
        {
            _ = packet.With(A.Task("t" + i.ToString(CultureInfo.InvariantCulture)).Writing("src/a.cs"));
        }

        var report = await Validate(packet);

        var omitted = Assert.Single(report.Checks, static c => c.State == CheckState.Omitted);
        Assert.Equal((PacketRules.ScopeOverlap, 280), (omitted.Rule, omitted.Omitted));
        Assert.DoesNotContain("src/a.cs", omitted.Fix, StringComparison.Ordinal);
        Assert.False(report.Passed);
    }
}
