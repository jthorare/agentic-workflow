using Aw.Sindri.Core.Profile;
using Aw.Sindri.Tests.ApiGoldens;
using Microsoft.Extensions.DependencyInjection;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ReviewLogin;

/// <summary>The login save approves in one click only when the live file is exactly the approved profile plus the login: a change the profile diff doesn't list never rides along.</summary>
public sealed class ReviewLoginApprovalTests
{
    private const string Route = "/sindri/reviews/login";

    [Fact, Trait("Lane", "integration")]
    public async Task A_change_the_diff_misses_is_not_approved_with_the_login()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        await ViewScenarios.Approve(host, ct);
        var file = Path.Combine(host.Home, "profile", "profile.yaml");
        // an empty settings entry: a real change to the profile hash that ProfileDiff lists no key for
        await File.WriteAllTextAsync(file, GoldenHost.ProfileYaml + "review:\n  settings:\n    extra: {}\n", ct);
        var approvedBefore = (await host.App.Services.GetRequiredService<IApprovedProfile>().Latest(ct)).Value!.Hash;

        var answer = await host.Http.Ask(Post(Route, """{"login":"joi-example"}""", ItemScenarios.Human(host)));

        Assert.Contains("\"approved\":false", answer.Body, StringComparison.Ordinal);
        var live = (await host.App.Services.GetRequiredService<IProfileSource>().Load(ct)).Value;
        var approved = (await host.App.Services.GetRequiredService<IApprovedProfile>().Latest(ct)).Value!;
        Assert.Equal(["review.login"], ProfileDiff.Between(approved.Profile, live.Profile));   // the diff alone would have passed it
        Assert.Equal(approvedBefore, approved.Hash);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task An_all_digit_login_is_saved_quoted_and_read_back_as_the_same_string()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var host = await GoldenHost.Start(temporary: true, ct);
        await ViewScenarios.Approve(host, ct);

        var answer = await host.Http.Ask(Post(Route, """{"login":"12345"}""", ItemScenarios.Human(host)));

        Assert.Contains("\"approved\":true", answer.Body, StringComparison.Ordinal);
        Assert.Contains("login: \"12345\"", await File.ReadAllTextAsync(Path.Combine(host.Home, "profile", "profile.yaml"), ct), StringComparison.Ordinal);
        var live = (await host.App.Services.GetRequiredService<IProfileSource>().Load(ct)).Value;
        Assert.Equal("12345", live.Profile.Review?.Login);
    }
}
