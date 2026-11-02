using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class FakeAgentSessionsContractTests : AgentSessionsContract
{
    protected override ValueTask<IAgentSessions> Create() => ValueTask.FromResult<IAgentSessions>(new FakeAgentSessions());

    protected override ValueTask End(IAgentSessions sessions, LaunchId launch, SessionEndReason reason, int? exitCode)
    {
        ((FakeAgentSessions)sessions).End(launch, reason, exitCode);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask EndByLimit(IAgentSessions sessions, LaunchId launch, LimitKind kind, long limit, long peak)
    {
        ((FakeAgentSessions)sessions).EndByLimit(launch, kind, limit, peak);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask Spend(IAgentSessions sessions, LaunchId launch, int sample, long tokens)
    {
        ((FakeAgentSessions)sessions).Spend(launch, sample, tokens);
        return ValueTask.CompletedTask;
    }
}
