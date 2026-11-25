using Aw.Sindri.Features.Scrub;
using Aw.Sindri.Tests.Workflow.Effects;

namespace Aw.Sindri.Tests.Scrub;

/// <summary>The staged scan (#2644): a k8s resource name under a *_SECRET key is a pointer and passes, a credential in the same spot still fails.</summary>
public sealed class ScrubResourceNameTests
{
    private const string RelPath = "k8s/overlays/demo/kustomization.yaml";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> ResourceNameLines() =>
    [
        "      - " + "ESO_" + "SECRET=" + "demo-background-effect-cluster-v2-env",
        "      - " + "ESO_" + "SECRET=" + "uat-worker-queue-cluster-v20-env",
        "  " + "ESO_" + "SECRET: " + "demo-background-effect-cluster-v2-env",
    ];

    public static TheoryData<string> CredentialLines() =>
    [
        "      - " + "ESO_" + "SECRET=" + "q7x2k9m4p1z8w3n6b5v0c7a2",
        "      - " + "ESO_" + "SECRET=" + "q7x2k-9m4p1-z8w3n-6b5v0-c7a2",
    ];

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(ResourceNameLines))]
    public async Task Staged_passes_a_k8s_resource_name_under_a_secret_key(string line)
    {
        using var repo = await Stage(line);

        var done = await ScrubRig.Service().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.True(done.IsSuccess, done.IsError ? done.Error.Message : null);
    }

    [Theory, Trait("Lane", "integration")]
    [MemberData(nameof(CredentialLines))]
    public async Task Staged_still_refuses_a_credential_under_a_secret_key_with_SND_SCRUB_002(string line)
    {
        using var repo = await Stage(line);

        var done = await ScrubRig.Service().Handle(new ScrubRequest(ScrubMode.Staged, Repo: repo.Path), Token);

        Assert.Equal("SND-SCRUB-002", done.Error.Code);
        Assert.Contains("kustomization.yaml:4", done.Error.Message, StringComparison.Ordinal);
    }

    private static async Task<TempGitRepo> Stage(string line)
    {
        var repo = await TempGitRepo.Create();
        var file = Path.Combine(repo.Path, RelPath);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var text = line.Contains('=', StringComparison.Ordinal)
            ? $"configMapGenerator:\n  - name: demo\n    literals:\n{line}\n"
            : $"configMapGenerator:\n  - name: demo\n{line}\n";
        await File.WriteAllTextAsync(file, text, Token);
        _ = await repo.Git("add", RelPath);
        return repo;
    }
}
