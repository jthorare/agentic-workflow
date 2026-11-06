using Aw.Forge.Core.Ports;
using Aw.Forge.Core.Pulls;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host.Forge;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Forge;

/// <summary>The real adapter over the forge package's file-backed forge, in a temporary state directory.</summary>
[Lane(TestLane.Integration)]
public sealed class ForgePullRequestsContractTests : PullRequestsContract, IDisposable
{
    private readonly List<DirectoryInfo> _directories = [];

    protected override PullRequestsUnderTest Create()
    {
        var directory = Directory.CreateTempSubdirectory("aw-sindri-pulls-");
        _directories.Add(directory);
        var created = ForgeAdapters.Create(new ForgeSettings("file", ["forge.test"]), name => name == "AW_STATE_DIR" ? directory.FullName : null, TimeProvider.System);
        var forge = created.Value;
        var seed = (IForgeSeed)forge;
        string SeedThread(string link, string body) => seed.SeedThread(new SeedThreadRequest(forge.Parse(link).Value, CommentKind.Thread, "reviewer", "MEMBER", IsBot: false, "src/a.cs", body)).Value;
        void SeedFiles(string link, IReadOnlyList<Core.Forge.PullFile> files, IReadOnlyDictionary<string, string> texts) =>
            _ = seed.SeedFiles(new SeedFilesRequest(forge.Parse(link).Value, [.. files.Select(f => new SeedFile(new Aw.Forge.Core.Pulls.PullFile(f.Path, f.PreviousPath, (Aw.Forge.Core.Pulls.PullFileStatus)(int)f.Status, f.Additions, f.Deletions, f.Patch), texts.GetValueOrDefault(f.Path)))])).Value;
        void SeedRuns(string link, IReadOnlyList<PullCheckRun> runs) =>
            _ = seed.SeedChecks(new SeedChecksRequest(forge.Parse(link).Value, runs.Any(r => r.Conclusion == "failure") ? ChecksState.Failing : ChecksState.Passing, [.. runs.Where(r => r.Conclusion == "failure").Select(r => r.Name)], new Dictionary<string, string>(StringComparer.Ordinal), Runs: [.. runs.Select(r => new CheckRun(r.Name, r.App, r.Conclusion, r.Sha))])).Value;
        return new PullRequestsUnderTest(new ForgePullRequests(forge), new PullRepo("forge.test", "acme", "widgets"), "elsewhere.test", SeedThread, SeedFiles, SeedRuns, CanPush: false);
    }

    public void Dispose()
    {
        foreach (var directory in _directories)
        {
            directory.Delete(recursive: true);
        }
    }
}
