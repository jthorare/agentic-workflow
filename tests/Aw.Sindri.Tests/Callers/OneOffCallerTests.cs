using Aw.Sindri.Core.Ids;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Callers;

/// <summary>#3466: the sindri CLI inside a one-off agent session runs as the same Unix user and can read the human token file, but must never call as the person.</summary>
public sealed class OneOffCallerTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task A_one_off_session_calls_as_a_session_though_the_human_token_reads()
    {
        var session = AgentSession.Detect(static name => name == AgentSession.OneOffVariable ? "1" : null, interactive: true);
        var headers = CallerHeaders.For(session, InMemoryHumanTokenStore.WithToken(), static _ => ValueTask.FromResult((RepoName?)null));

        var reason = await headers.Resolve(TestContext.Current.CancellationToken);

        Assert.True(session.OneOff);
        Assert.NotNull(reason);
        Assert.Contains(AgentSession.OneOffVariable, reason, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Without_the_marker_a_readable_token_still_makes_the_person()
    {
        var session = AgentSession.Detect(static _ => null, interactive: true);
        var headers = CallerHeaders.For(session, InMemoryHumanTokenStore.WithToken(), static _ => ValueTask.FromResult((RepoName?)null));

        Assert.Null(await headers.Resolve(TestContext.Current.CancellationToken));
    }
}
