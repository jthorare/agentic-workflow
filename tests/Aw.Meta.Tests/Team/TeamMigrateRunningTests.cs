using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Meta.Core;
using Aw.Meta.Core.Ids;
using Aw.Meta.Core.Lanes;
using Aw.Meta.Host;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Team;

[Lane(TestLane.Integration)]
public sealed class TeamMigrateRunningTests : IDisposable
{
    private readonly TempDirectory _root = new();

    [Fact]
    public async Task An_offline_session_named_like_a_lane_does_not_block_the_migrate()
    {
        var catalog = Catalog(new LiveSession(SessionId.From("36f518aa-0000-0000-0000-000000000000"), "sindri-api", Pid: 4242, Status: "offline", Cwd: string.Empty));

        var result = await catalog.Migrate(dryRun: false, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : string.Empty);
        Assert.True(Directory.Exists(Path.Combine(_root.Path, "teams", "sindri")));
    }

    [Fact]
    public async Task A_running_session_named_like_a_lane_still_blocks_the_migrate()
    {
        var catalog = Catalog(new LiveSession(SessionId.From("36f518aa-0000-0000-0000-000000000000"), "sindri-api", Pid: 4242, Status: "busy", Cwd: string.Empty));

        var result = await catalog.Migrate(dryRun: false, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("sindri-api", result.Error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_root.Path, "teams")));
    }

    private FileTeamCatalog Catalog(LiveSession session)
    {
        File.WriteAllText(Path.Combine(_root.Path, "team.json"), """{"team":"sindri","repo":"/w","lanes":["meta","api"],"metaLane":"meta"}""");
        var sessions = new InMemorySessionDirectory();
        sessions.Add(session);
        return new FileTeamCatalog(new TeamLayout(_root.Path, Team: null, Legacy: true, Ambiguous: []), sessions, AwJson.CreateOptions(MetaJsonContext.Default), TimeProvider.System);
    }

    public void Dispose() => _root.Dispose();
}
