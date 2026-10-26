using Aw.Kernel.Testing;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

public sealed class CallerContextTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData(CallerClass.Human, ClientLabel.Cli, "human:cli")]
    [InlineData(CallerClass.Human, ClientLabel.Ui, "human:ui")]
    [InlineData(CallerClass.Human, ClientLabel.Prover, "human:prover")]
    [InlineData(CallerClass.Human, ClientLabel.None, "human:http")]
    [InlineData(CallerClass.Session, ClientLabel.Cli, "session:cli")]
    [InlineData(CallerClass.Session, ClientLabel.Mcp, "session:mcp")]
    [InlineData(CallerClass.Session, ClientLabel.Prover, "session:http")]
    [InlineData(CallerClass.Service, ClientLabel.None, "session:http")]
    public void Http_composes_the_actor_from_the_class_and_the_label(CallerClass caller, ClientLabel client, string actor) =>
        Assert.Equal(actor, CallerContext.Http(new HttpCaller(caller, Terminal: false, client, Repo: null), new SequentialIdGenerator()).Actor);

    [Theory, Trait("Lane", "unit")]
    [InlineData(CallerClass.Session, true)]
    [InlineData(CallerClass.Human, false)]
    public void Http_carries_the_launch_only_for_a_session(CallerClass caller, bool kept)
    {
        var launch = LaunchId.From("LCH-00000000000000000000000000000001");

        var context = CallerContext.Http(new HttpCaller(caller, Terminal: false, ClientLabel.Cli, Repo: null, launch), new SequentialIdGenerator());

        Assert.Equal(kept ? launch : null, context.Launch);
    }

    [Fact, Trait("Lane", "unit")]
    public void Terminal_is_kept_only_for_a_human()
    {
        Assert.True(A.Caller().AtTerminal().Build().Terminal);
        Assert.False(CallerContext.Http(new HttpCaller(CallerClass.Session, Terminal: true, ClientLabel.Cli, Repo: null), new SequentialIdGenerator()).Terminal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Entering_a_caller_makes_it_current_until_disposed_then_restores_the_previous_one()
    {
        var accessor = new AsyncLocalCallerContextAccessor(new SequentialIdGenerator());
        var human = A.Caller().AtTerminal().Client(ClientLabel.Cli).Build();

        using (accessor.Enter(human))
        {
            using (accessor.EnterService("host"))
            {
                Assert.Equal("service:host", accessor.Current?.Actor);
            }

            Assert.Same(human, accessor.Current);
        }

        Assert.Null(accessor.Current);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_caller_flows_into_awaited_calls()
    {
        var accessor = new AsyncLocalCallerContextAccessor(new SequentialIdGenerator());
        using var entered = accessor.EnterService("heartbeat");

        var seen = await Task.Run(() => accessor.Current?.Actor, TestContext.Current.CancellationToken);

        Assert.Equal("service:heartbeat", seen);
    }
}
