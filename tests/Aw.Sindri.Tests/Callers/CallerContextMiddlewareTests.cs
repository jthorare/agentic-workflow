using System.Net;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Host;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Callers;

/// <summary>
/// What stays of the caller gates once every verb is open to agent sessions: a launch secret still turns a caller that claims to be the human into a
/// session, so what it writes is attributed to the session, not the owner (#1002).
/// </summary>
public sealed class CallerContextMiddlewareTests(CallerHost host) : IClassFixture<CallerHost>
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_launch_secret_still_downgrades_a_claimed_human_to_a_session()
    {
        var answer = await host.Http.Ask(Get("/test/caller", [.. Human(host.Token.Value), (SindriHeaders.LaunchSecret, "proof")]));

        Assert.Equal((HttpStatusCode.OK, "Session|"), (answer.Status, answer.Body));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_launch_secret_downgrades_a_claimed_human_before_the_token_check()
    {
        var answer = await host.Http.Ask(Get("/test/caller", [.. Human(token: null), (SindriHeaders.LaunchSecret, "proof")]));

        Assert.Equal((HttpStatusCode.OK, "Session|"), (answer.Status, answer.Body));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_human_claim_with_a_launch_secret_becomes_a_session_with_no_terminal_and_a_session_stays_one()
    {
        var human = new ParsedCaller(CallerClass.Human, Terminal: true, Client: ClientLabel.Cli, Repo: null, UnknownClient: null, Launch: null);

        var downgraded = CallerContextMiddleware.AsLaunched(human, carriesLaunchSecret: true);
        var kept = CallerContextMiddleware.AsLaunched(human, carriesLaunchSecret: false);

        Assert.Equal((CallerClass.Session, false), (downgraded.Class, downgraded.Terminal));
        Assert.Equal((CallerClass.Human, true), (kept.Class, kept.Terminal));
    }
}
