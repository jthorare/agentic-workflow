using System.Net;
using System.Text.Json;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Host;
using Aw.Sindri.Testing.Workflow.ApproveAll;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ApproveAll;
using Aw.Sindri.Tests.Callers;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using static Aw.Sindri.Tests.ApiGoldens.ApproveAllScenarios;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.AppConfirm;

/// <summary>The app's confirmation on the real host (setup-once spec §4): the human token with no terminal issues an app challenge and redeems it for the profile and approve-all; a session never does.</summary>
public sealed class AppConfirmRoutesTests
{
    private const string AppChallenge = "/sindri/workflow/challenges/app";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The Sindri app: the human token, the 'ui' label and no terminal claim.</summary>
    private static (string Name, string Value)[] App(GoldenHost host) =>
        [(SindriHeaders.Caller, "human"), (SindriHeaders.Client, "ui"), (SindriHeaders.Token, host.Token.Value)];

    private static void After(GoldenHost host, TimeSpan time) => ((FakeTimeProvider)host.App.Services.GetRequiredService<TimeProvider>()).Advance(time);

    private static async Task<GoldenHost> Started(FakeShippedRecipes? shipped = null, bool approved = true)
    {
        var host = await Start(shipped ?? new FakeShippedRecipes(ApproveAllFixtures.StepAndItem()), Token);
        if (approved)
        {
            await ViewScenarios.Approve(host, Token);
        }

        return host;
    }

    private static async Task<JsonElement> Issue(GoldenHost host, string action)
    {
        var issued = await host.Http.Ask(Post(AppChallenge, $$"""{"action":"{{action}}"}""", App(host)));
        Assert.True(issued.Status == HttpStatusCode.OK, issued.Body);
        return JsonDocument.Parse(issued.Body).RootElement;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_starts_approve_all_with_no_body()
    {
        await using var host = await Started(approved: false);

        var started = await host.Http.Ask(Post(PlanRoute, json: null, App(host)));
        await JobHost.Settle(host, Token);

        Assert.Equal(HttpStatusCode.OK, started.Status);
        var again = await host.Http.Ask(Get(PlanRoute, Human(host.Token.Value)));
        Assert.Empty(JsonDocument.Parse(again.Body).RootElement.GetProperty("recipes").EnumerateArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_also_redeems_approve_all_by_typing_its_text()
    {
        await using var host = await Started();
        var issued = await Issue(host, "approve-all " + await Digest(host));
        After(host, IssuedChallenge.EarliestAnswer);

        var started = await host.Http.Ask(Post(PlanRoute,
            $$"""{"challenge":"{{issued.GetProperty("challenge").GetString()}}","typed":"{{issued.GetProperty("typedAlternative").GetString()}}"}""", App(host)));

        Assert.Equal(HttpStatusCode.OK, started.Status);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_app_is_still_refused_the_app_challenge_for_any_other_action()
    {
        await using var host = await Started();

        var revoke = await host.Http.Ask(Post(AppChallenge, """{"action":"revoke workflow.step@9ab2d40c1e77"}""", App(host)));
        var stop = await host.Http.Ask(Post(AppChallenge, """{"action":"stop"}""", App(host)));

        Assert.Equal([(HttpStatusCode.Forbidden, "SND-AUTH-002")], new[] { revoke, stop }.Select(static a => (a.Status, a.Code)).Distinct().ToArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_all_before_a_profile_is_approved_has_no_app_challenge()
    {
        await using var host = await Started(approved: false);

        var challenge = await host.Http.Ask(Post(AppChallenge, """{"action":"approve-all c41e07"}""", App(host)));

        Assert.Equal((HttpStatusCode.Conflict, "SND-SETUP-007"), (challenge.Status, challenge.Code));
    }

}
