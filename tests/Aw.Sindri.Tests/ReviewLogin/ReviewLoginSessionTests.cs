using System.Net;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ReviewLogin;

/// <summary>POST /sindri/reviews/login is the owner's: a launched session is refused with SND-AUTH-002 and nothing changes (the profile file and the approved hash stay as they were).</summary>
public sealed class ReviewLoginSessionTests
{
    [Fact, Trait("Lane", "integration")]
    public async Task A_session_post_leaves_the_profile_file_and_the_approved_hash_unchanged()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        await ViewScenarios.Approve(host, ct);
        var file = Path.Combine(host.Home, "profile", "profile.yaml");
        var before = await File.ReadAllTextAsync(file, ct);
        var approvedBefore = (await host.App.Services.GetRequiredService<IApprovedProfile>().Latest(ct)).Value!.Hash;

        var answer = await host.Http.Ask(Post("/sindri/reviews/login", """{"login":"joi-example"}""", ItemScenarios.Session("demo")));

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
        Assert.Contains("SND-AUTH-002", answer.Body, StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllTextAsync(file, ct));
        Assert.Equal(approvedBefore, (await host.App.Services.GetRequiredService<IApprovedProfile>().Latest(ct)).Value!.Hash);
    }
}
