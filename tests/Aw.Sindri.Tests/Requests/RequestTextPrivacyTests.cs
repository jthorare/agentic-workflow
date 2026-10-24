using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.Requests;

/// <summary>
/// A request's text is for the human who decides it (lead requirement, P4): the stream and a session's ledger read send 'request.filed'
/// in its public form (the catalog's RequestFiledPublic, no text); only a human's ledger read shows the stored payload whole.
/// </summary>
public sealed class RequestTextPrivacyTests
{
    private const string Text = "PRIVATE-REQUEST-TEXT split the checkout flow";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<GoldenHost> Filed()
    {
        var host = await GoldenHost.Start(temporary: true, Token, ItemScenarios.ProfileYaml);
        await ViewScenarios.Approve(host, Token);
        _ = await RequestCalls.Filed(host, Text, ItemScenarios.Session("demo"), Token, kind: "note");
        return host;
    }

    [Fact, Trait("Lane", "integration")]
    public async Task The_event_stream_shows_request_filed_without_its_text()
    {
        await using var host = await Filed();

        var human = await StreamScenarios.Frames(host, "/sindri/events?types=request.filed", ItemScenarios.Human(host), Token);
        var session = await StreamScenarios.Frames(host, "/sindri/events?types=request.filed", ItemScenarios.Session("demo"), Token);

        foreach (var frames in new[] { human, session })
        {
            Assert.Contains("\"type\": \"request.filed\"", frames, StringComparison.Ordinal);
            Assert.Contains("\"kind\": \"note\"", frames, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE-REQUEST-TEXT", frames, StringComparison.Ordinal);
            Assert.DoesNotContain("\"text\"", frames, StringComparison.Ordinal);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_sessions_ledger_read_shows_the_stored_event_whole_like_a_humans()
    {
        await using var host = await Filed();

        var body = await RequestCalls.Read(host, "/sindri/ledger?type=request.filed", ItemScenarios.Session("demo"), Token);

        Assert.Contains("request.filed", body, StringComparison.Ordinal);
        Assert.Contains(Text, body, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_humans_ledger_read_shows_the_stored_event_whole()
    {
        await using var host = await Filed();

        var body = await RequestCalls.Read(host, "/sindri/ledger?type=request.filed", ItemScenarios.Human(host), Token);

        Assert.Contains("request.filed", body, StringComparison.Ordinal);
        Assert.Contains(Text, body, StringComparison.Ordinal);
    }
}
