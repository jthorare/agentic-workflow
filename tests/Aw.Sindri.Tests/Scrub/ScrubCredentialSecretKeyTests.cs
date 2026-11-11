using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>#2722: the name exemption is for a pointer *SECRET key (ESO, DB) only. A name-shaped value under JWT, CLIENT, SESSION, WEBHOOK, APP, SIGNING or COOKIE + SECRET is the credential itself and still fails the staged scan.</summary>
public sealed class ScrubCredentialSecretKeyTests
{
    private const string RelPath = "config/app.env";
    private const string NameShapedValue = "dev-jwt-signing-key-v123";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> CredentialSecretLines() =>
    [
        "JWT" + "_" + "SEC" + "RET=" + NameShapedValue,
        "CLIENT" + "_" + "SEC" + "RET=" + NameShapedValue,
        "SESSION" + "_" + "SEC" + "RET=" + NameShapedValue,
        "WEBHOOK" + "_" + "SEC" + "RET=" + NameShapedValue,
        "APP" + "_" + "SEC" + "RET=" + NameShapedValue,
        "SIGNING" + "_" + "SEC" + "RET=" + NameShapedValue,
        "COOKIE" + "_" + "SEC" + "RET=" + NameShapedValue,
    ];

    public static TheoryData<string> PointerSecretLines() =>
    [
        "ESO" + "_" + "SEC" + "RET=" + NameShapedValue,
        "DB" + "_" + "SEC" + "RET=" + NameShapedValue,
    ];

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(CredentialSecretLines))]
    public async Task Staged_refuses_a_name_shaped_value_under_a_credential_secret_key_with_SND_SCRUB_002(string line)
    {
        using var repo = await TempGitRepo.Create();
        var file = Path.Combine(repo.Path, RelPath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, $"APP_NAME=demo\n{line}\n", Token);
        _ = await repo.Git("add", RelPath);

        var done = await ScrubRig.Service().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.Equal("SND-SCRUB-002", done.Error.Code);
        Assert.Contains("app.env:2", done.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(PointerSecretLines))]
    public async Task Staged_passes_a_name_shaped_value_under_a_pointer_secret_key(string line)
    {
        using var repo = await TempGitRepo.Create();
        var file = Path.Combine(repo.Path, RelPath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, $"APP_NAME=demo\n{line}\n", Token);
        _ = await repo.Git("add", RelPath);

        var done = await ScrubRig.Service().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
    }
}
