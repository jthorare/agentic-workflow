using Aw.Context.Core.Engine;
using Aw.Context.Core.Library;
using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;

namespace Aw.Context.Tests.Sync;

/// <summary>A library feed over a temp copy of tests/fixtures/library-vault (a vault named "harbor" that denies "drafts/**") and the real SQLite store.</summary>
internal sealed class LibraryFeedRig : IDisposable
{
    public const string Harbor = "harbor";

    private readonly string _folder = Directory.CreateTempSubdirectory("aw-library-vault-").FullName;

    public LibraryFeedRig(TempHome home, bool copyFixture = true)
    {
        Home = home;
        Store = new SqliteRecordStore(home.Database);
        if (copyFixture)
        {
            Copy(Path.Combine(RepoRoot(), "tests", "fixtures", "library-vault"), VaultPath);
        }
        else
        {
            _ = Directory.CreateDirectory(VaultPath);
        }
    }

    public TempHome Home { get; }

    public SqliteRecordStore Store { get; }

    public FakeLibraryStatus Status { get; } = new();

    public string VaultPath => Path.Combine(_folder, "vault");

    public static string Sha256(string path) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));

    public LibraryFeed Feed(params LibraryRoot[] roots) => new(new LibrarySetup(roots, ["acme/harbor-web"]), Store, Home.Scrubbers, Status, Home.Clock);

    public LibraryRoot Vault() => new() { Name = Harbor, Path = VaultPath, Deny = ["drafts/**"] };

    public string Write(string relative, string text)
    {
        var path = Path.Combine(VaultPath, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
        return path;
    }

    public async Task<IReadOnlyList<StoredRecord>> Stored(bool includeRemoved = false) =>
        (await Store.List(new RecordFilter(LibrarySource.Id, IncludeRemoved: includeRemoved), after: null, 100, TestContext.Current.CancellationToken)).Value;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AgenticWorkflow.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("AgenticWorkflow.slnx not found above the test binaries");
    }

    private static void Copy(string from, string to)
    {
        _ = Directory.CreateDirectory(to);
        foreach (var entry in new DirectoryInfo(from).EnumerateFileSystemInfos())
        {
            var target = Path.Combine(to, entry.Name);
            if (entry is DirectoryInfo dir)
            {
                Copy(dir.FullName, target);
            }
            else
            {
                _ = ((FileInfo)entry).CopyTo(target);
            }
        }
    }
}
