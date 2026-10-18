using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Infrastructure.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The aw:fleet hook's state files (collector spec §4.1 step 4, §5.4): the contract fixtures read as the manifest says; pruning is safe.</summary>
[UnsupportedOSPlatform("windows")]
public sealed class FleetStateFilesTests
{
    private static readonly AgentId[] _providers = [AgentId.From("claude-code"), AgentId.From("codex"), AgentId.From("cursor")];

    [Fact, Trait("Lane", "integration")]
    public void Every_valid_fixture_reads_and_no_invalid_one_does()
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SessionFixtures.Fleet, "manifest.json")));
        var valid = manifest.RootElement.GetProperty("fixtures").EnumerateArray()
            .Where(static f => f.GetProperty("schema").GetString() == "valid")
            .Select(static f => f.GetProperty("file").GetString()).ToList();

        var read = new FleetStateFiles(SessionFixtures.Fleet, _providers, pruning: false).Read(TestContext.Current.CancellationToken).Value;

        Assert.NotEmpty(valid);
        Assert.Equal(valid.Count, read.Count);
        var asking = read.Single(static f => f.State == HookState.Asking && f.Tool == "Bash" && f.Provider.Value == "claude-code" && f.Cwd is not null && f.Transcript is not null && f.AgentPid is not null);
        Assert.Equal(AskingReason.Approval, asking.Reason);
    }

    [Fact, Trait("Lane", "integration")]
    public void Prune_deletes_only_old_dead_files_in_a_marked_private_directory()
    {
        using var fleet = new TempFleet();
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        fleet.Write("claude-code", "old-dead", now.AddHours(-25));
        fleet.Write("claude-code", "old-live", now.AddHours(-25));
        fleet.Write("claude-code", "recent-dead", now.AddHours(-1));
        fleet.Write("codex", "unmarked-old", now.AddHours(-30), marker: false);

        var pruned = fleet.Files(pruning: true).Prune([new SessionRef(AgentId.From("claude-code"), SessionId.From("old-live"))], now);

        Assert.Equal(1, pruned);
        Assert.Equal(["old-live.json", "recent-dead.json"], fleet.Names("claude-code"));
        Assert.Equal(["unmarked-old.json"], fleet.Names("codex"));
    }

    [Fact, Trait("Lane", "integration")]
    public void Prune_does_nothing_under_a_test_override_in_a_git_work_tree_or_for_a_file_that_does_not_validate()
    {
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        using var fleet = new TempFleet();
        fleet.Write("claude-code", "old-dead", now.AddHours(-48));
        File.WriteAllText(Path.Combine(fleet.Root, "claude-code", "notes.json"), """{ "hello": "world" }""");

        Assert.Equal(0, fleet.Files(pruning: false).Prune([], now));
        Directory.CreateDirectory(Path.Combine(fleet.Root, ".git"));
        Assert.Equal(0, fleet.Files(pruning: true).Prune([], now));
        Directory.Delete(Path.Combine(fleet.Root, ".git"));
        Assert.Equal(1, fleet.Files(pruning: true).Prune([], now));
        Assert.Equal(["notes.json"], fleet.Names("claude-code"));
    }

    [Fact, Trait("Lane", "integration")]
    public void Prune_caps_a_provider_directory_at_512_files_dropping_the_oldest_dead_first()
    {
        using var fleet = new TempFleet();
        var now = new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < FleetStateFiles.MaxPerProvider + 3; i++)
        {
            fleet.Write("claude-code", "s" + i.ToString("D4", CultureInfo.InvariantCulture), now.AddMinutes(-i));
        }

        var pruned = fleet.Files(pruning: true).Prune([], now);

        Assert.Equal(3, pruned);
        Assert.Equal(FleetStateFiles.MaxPerProvider, fleet.Names("claude-code").Count);
        Assert.DoesNotContain("s0514.json", fleet.Names("claude-code"), StringComparer.Ordinal);
    }
}
