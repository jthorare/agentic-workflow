using System.Net;
using System.Text.Json;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;

namespace Aw.Sindri.Tests.Requests;

/// <summary>P4 hand-proof defects: an optional body key left out means null, and a session's repo must be one of the approved profile's repos.</summary>
public sealed class RequestBodyAndRepoTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "integration")]
    public async Task A_request_body_without_an_item_key_files_a_repo_less_request()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);

        var answer = await RequestCalls.File(host, """{"kind":"note","text":"No item key."}""", ItemScenarios.Human(host), Token);

        Assert.True(answer.Status == HttpStatusCode.OK, answer.Body);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_decision_body_without_a_note_key_decides()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);
        var (_, decision) = await RequestCalls.Filed(host, "Split it.", ItemScenarios.Session("demo"), Token);
        await ViewScenarios.CatchUp(host, Token);

        var answer = await host.Http.Ask(CallerRequests.Post("/sindri/decisions/" + decision, """{"choice":"accept"}""", ItemScenarios.Human(host)));

        Assert.True(answer.Status == HttpStatusCode.OK, answer.Body);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_session_in_a_repo_outside_the_profile_cannot_file_a_request()
    {
        await using var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);

        var answer = await RequestCalls.File(host, RequestCalls.Body("note", "From gamma."), ItemScenarios.Session("gamma"), Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        using var json = JsonDocument.Parse(answer.Body);
        Assert.Equal("SND-CLI-003", json.RootElement.GetProperty("title").GetString());
        Assert.Contains("'gamma' isn't a repo of the approved profile", answer.Body, StringComparison.Ordinal);
    }
}
