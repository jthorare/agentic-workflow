using System.Net;
using Aw.Kernel.Services;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;
using Aw.Sindri.Features.ListMessageRows;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.Callers;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.Messages;

/// <summary>
/// M1f (messages spec §8.2, W17): the read routes answer their goldens, and a session's read carries keys, kinds and counts, never text, names or topics.
/// The goldens are not recorded yet (<see cref="MessageScenarios"/>): each row checks the status before the golden, so AW_GOLDEN_UPDATE never writes a stub's 500.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class MessagesReadRouteTests
{
    // Text the seeded fake holds (M1f seeds it through FakeSlackServer); none of it may reach a session.
    private static readonly string[] _text = ["can you ship the fare table today?", "Sam Lee", "#harbor", "harbor"];

    public static TheoryData<string, string> ListRows()
    {
        var rows = new TheoryData<string, string>();
        foreach (var (slug, scenario) in MessageScenarios.Lists)
        {
            rows.Add(slug, scenario);
        }

        return rows;
    }

    [Theory(Timeout = 30_000)]
    [MemberData(nameof(ListRows))]
    public Task ApiGoldens_messages_lists(string slug, string scenario) => RedUntil.Phase("M1f", async () =>
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        await Probe(host);

        var answer = await host.Http.Ask(Get(MessageScenarios.Path(slug, scenario), Human(host.Token.Value)));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        ApiGolden.Match(answer.Body, host.Home, slug, scenario);
    });

    [Fact(Timeout = 30_000)]
    public Task Session_read_returns_no_text() => RedUntil.Phase("M1f", async () =>
    {
        await using var host = await GoldenHost.Start(temporary: true, TestContext.Current.CancellationToken);
        await Probe(host);

        Answer answer;
        using (AsSession())
        {
            answer = await host.Http.Ask(Get("/sindri/messages/lists/needs-reply", Human(host.Token.Value)));
        }

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Contains("\"rows\"", answer.Body, StringComparison.Ordinal);
        Assert.All(_text, t => Assert.DoesNotContain(t, answer.Body, StringComparison.Ordinal));
    });

    // why: over HTTP a stub's throw is a 500, so the list service is asked straight from the host's own composition first: while it is the M1s stub
    // this throws NotSupportedException (RedUntil skips); once M1f builds it the answer is ignored and the route is checked for real
    private static async Task Probe(GoldenHost host)
    {
        await using var scope = host.App.Services.CreateAsyncScope();
        _ = await scope.ServiceProvider.GetRequiredService<IService<ListMessageRowsRequest, ListMessageRowsResponse>>()
            .Handle(new ListMessageRowsRequest(MessageList.NeedsReply, After: null, Limit: 1), TestContext.Current.CancellationToken);
    }
}
