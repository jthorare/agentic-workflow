using System.Text.Json.Nodes;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// Issue #2172: when the Linear write has landed and the record store's Commit hits a SQLite error Kernel.Data does not map (SQLITE_ERROR, code 1),
/// <see cref="ILinearWriteThrough.Store"/> answers false ("not stored", so the caller answers SND-LINEAR-013) and nothing throws.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearWriteThroughStoreFailureTests
{
    [Fact]
    public async Task A_store_failure_kernel_data_does_not_map_reads_as_not_stored_and_rolls_back()
    {
        await using var world = await LinearHubWorld.Start(seed => seed.Team("ENG").Issue("ENG", "Fix the login timeout"));
        var id = world.Linear.Issues.Single().Id;
        _ = await world.Tick();
        await world.Update(id, new JsonObject { ["title"] = "Changed title" });

        // why: the trigger fires inside Commit's page write and fails with SQLITE_ERROR (its target table is gone), a code Kernel.Data's SqliteErrors does not map
        world.Sql("CREATE TABLE gone(x); CREATE TRIGGER broken AFTER UPDATE ON source_record BEGIN INSERT INTO gone VALUES (1); END; DROP TABLE gone;");

        var stored = await world.Provider.GetRequiredService<ILinearWriteThrough>().Store(world.Scope, ExternalId.From(id), LinearWrittenFields.None, TestContext.Current.CancellationToken);

        Assert.False(stored);
        Assert.Equal("Fix the login timeout", world.Text($"SELECT title FROM source_record WHERE key = 'linear:issue/{id}'"));
    }
}
