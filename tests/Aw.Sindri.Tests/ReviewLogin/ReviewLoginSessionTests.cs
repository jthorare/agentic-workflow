using System.Net;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ReviewLogin;

/// <summary>POST /sindri/reviews/login is open to a session (agent-first PR 2): a launched session saves the login into the live profile, and the approved hash stays as it was (saving never approves).</summary>
public sealed class ReviewLoginSessionTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_session_post_saves_the_login_and_leaves_the_approved_hash_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        await ViewScenarios.Approve(host, ct);
        var file = Path.Combine(host.Home, "profile", "profile.yaml");
        var before = await File.ReadAllTextAsync(file, ct);
        var approvedBefore = (await host.App.Services.GetRequiredService<IApprovedProfile>().Latest(ct)).Value!.Hash;

        var answer = await host.Http.Ask(Post("/sindri/reviews/login", """{"login":"joi-example"}""", ItemScenarios.Session("demo")));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var after = await File.ReadAllTextAsync(file, ct);
        Assert.NotEqual(before, after, StringComparer.Ordinal);
        Assert.Contains("joi-example", after, StringComparison.Ordinal);
        Assert.Equal(approvedBefore, (await host.App.Services.GetRequiredService<IApprovedProfile>().Latest(ct)).Value!.Hash);
    }
}
