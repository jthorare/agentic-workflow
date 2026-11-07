using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Linear;
using Aw.Sindri.Ui.Testing;

namespace Aw.Sindri.Ui.Tests.Api;

/// <summary>The fake's Linear writes (Linear hub 1b contract): they answer what the real client makes of the same golden, and record what was sent.</summary>
[Lane(TestLane.Unit)]
public sealed class FakeSindriApiLinearWritesTests
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_change_answers_the_queued_answers_in_order_then_the_settable_one_and_records_each_body()
    {
        var fake = new FakeSindriApi();
        fake.QueueLinearChange(RouteAnswer.Problem("linear-change", "conflict"), RouteAnswer.Problem("linear-change", "unknown"));
        fake.LinearChangeAnswer = RouteAnswer.Api("linear-change", "already-applied");
        var opened = new JsonObject { ["title"] = "a" };
        var set = new JsonObject { ["title"] = "b" };

        var first = await fake.LinearChange(_api, "ENG-12", opened, set, Token);
        var second = await fake.LinearChange(_api, "ENG-12", opened, set, Token);
        var third = await fake.LinearChange(_api, "ENG-12", opened, set, Token);

        _ = Assert.IsType<LinearChangeConflict>(first.Value);
        _ = Assert.IsType<LinearWriteUnknown>(second.Value);
        Assert.True(Assert.IsType<LinearChangeApplied>(third.Value).AlreadyApplied);
        Assert.Equal(["ENG-12", "ENG-12", "ENG-12"], fake.LinearChanges.Select(c => c.Key), StringComparer.Ordinal);
        Assert.Equal("b", (string?)fake.LinearChanges[0].Body.Set["title"]);
    }

    [Fact]
    public async Task A_comment_and_a_create_record_what_was_sent_and_a_gate_holds_the_answer()
    {
        var fake = new FakeSindriApi { LinearCommentGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };

        var held = fake.LinearComment(_api, "ENG-12", "c-1", "hi", Token);
        Assert.False(held.IsCompleted);
        fake.LinearCommentGate.SetResult();
        var posted = Assert.IsType<LinearCommentPosted>((await held).Value);
        var created = Assert.IsType<LinearCreated>((await fake.LinearCreate(_api, new LinearCreateBody("c-2", "ENG", "T"), Token)).Value);

        Assert.Equal([new LinearCommentSent("ENG-12", "c-1", "hi")], fake.LinearComments);
        Assert.Equal(("c-2", "ENG-58"), (Assert.Single(fake.LinearCreates).ClientId, created.Issue.Key));
        Assert.False(posted.Slow);
    }

    [Fact]
    public async Task The_connection_calls_answer_their_goldens_and_record_the_keys_saved_and_the_removals()
    {
        var fake = new FakeSindriApi();

        var connection = await fake.LinearConnection(_api, Token);
        var teams = await fake.LinearConnectionTeams(_api, "k", Token);
        var saved = await fake.LinearConnect(_api, "read", "k", ["ENG"], Token);
        var removed = await fake.LinearDisconnectWrite(_api, Token);

        Assert.Equal((2, 2, true, true), (connection.Value.Teams.Count, teams.Value.Teams.Count, saved.Value.Approved, removed.Value.Removed));
        Assert.Equal(["k"], fake.LinearTeamKeys, StringComparer.Ordinal);
        Assert.Equal(new LinearConnectBody("read", "k", ["ENG"]).Kind, Assert.Single(fake.LinearConnects).Kind);
        Assert.Equal(1, fake.LinearDisconnects);
    }
}
