using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>#2644 review: the name exemption is for a *SECRET key only. A name-shaped value under a PASSWORD, TOKEN or API_KEY key is the credential itself and still fails the staged scan.</summary>
public sealed class ScrubCredentialKeyTests
{
    private const string RelPath = "config/app.env";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> NameShapedCredentialLines() =>
    [
        "DB_" + "PASSWORD=" + "staging-db-password-123",
        "API_" + "TOKEN=" + "team-dev-token-abc-v12",
        "API_" + "KEY=" + "team-dev-apikey-abc-v12",
    ];

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(NameShapedCredentialLines))]
    public async Task Staged_refuses_a_name_shaped_value_under_a_credential_key_with_SND_SCRUB_002(string line)
    {
        using var repo = await TempGitRepo.Create();
        var file = System.IO.Path.Combine(repo.Path, RelPath);
        _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, $"APP_NAME=demo\n{line}\n", Token);
        _ = await repo.Git("add", RelPath);

        var done = await ScrubRig.Service().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.Equal("SND-SCRUB-002", done.Error.Code);
        Assert.Contains("app.env:2", done.Error.Message, StringComparison.Ordinal);
    }
}
