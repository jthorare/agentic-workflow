using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Infrastructure.Reviews;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>The fix-task files on disk (PR review page spec §8.2; the hook reads them): one 0600 file per head, named by the sha256 of the lowercased head, tasks ordered by item id.</summary>
[Lane(TestLane.Integration)]
public sealed class FixTaskFilesTests : FixTaskFilesContract, IDisposable
{
    private readonly TempHome _home = new();
    private readonly FixTaskFiles _files;

    public FixTaskFilesTests()
    {
        _files = new FixTaskFiles(new FakeSindriHome(_home.Path));
    }

    protected override IFixTaskFiles Files => _files;

    private string Folder => Path.Combine(_home.Path, "fix-tasks");

    public void Dispose() => _home.Dispose();

    protected override IReadOnlyDictionary<string, int> Open(string head)
    {
        var path = Path.Combine(Folder, FixHead.FileName(head));
        return File.Exists(path)
            ? ((JsonArray)JsonNode.Parse(File.ReadAllText(path))!["tasks"]!).ToDictionary(static t => t!["item"]!.GetValue<string>(), static t => t!["pr"]!.GetValue<int>(), StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);
    }

    [Fact]
    public void The_file_name_is_the_sha256_of_the_head_string()
    {
        const string head = "acme/api:feat/x";

        Expect.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(head))) + ".json", FixHead.FileName(head));
    }

    [Fact]
    public void The_head_lowercases_owner_and_repo_and_keeps_the_branch()
        => Expect.Equal("acme/api:Feat/X", FixHead.Of("Acme/API", "Feat/X"));

    [Fact]
    public async Task The_file_holds_version_head_and_tasks_and_is_owner_only()
    {
        _ = (await Files.Add("acme/api:feat/x", Id('a'), 2071, Token)).Value;

        var path = Path.Combine(Folder, FixHead.FileName("acme/api:feat/x"));
        var body = JsonNode.Parse(await File.ReadAllTextAsync(path, Token))!;
        Expect.Equal(1, body["version"]!.GetValue<int>());
        Expect.Equal("acme/api:feat/x", body["head"]!.GetValue<string>());
        if (!OperatingSystem.IsWindows())
        {
            Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
        }
    }

    [Fact]
    public async Task The_last_remove_deletes_the_file()
    {
        _ = (await Files.Add("acme/api:feat/x", Id('a'), 2071, Token)).Value;

        _ = (await Files.Remove(Id('a'), Token)).Value;

        Assert.False(File.Exists(Path.Combine(Folder, FixHead.FileName("acme/api:feat/x"))));
    }

    [Fact]
    public async Task A_task_row_with_the_wrong_value_types_is_skipped_not_thrown_on()
    {
        const string head = "acme/api:feat/x";
        Directory.CreateDirectory(Folder);
        await File.WriteAllTextAsync(Path.Combine(Folder, FixHead.FileName(head)), $$"""{"version":1,"head":"{{head}}","tasks":[{"item":5,"pr":1},{"item":"{{Id('a').Value}}","pr":"x"},{"item":"{{Id('b').Value}}","pr":7}]}""", Token);

        var added = await Files.Add(head, Id('c'), 2071, Token);

        Assert.True(added.IsSuccess);
        Assert.Equal([(Id('b').Value, 7), (Id('c').Value, 2071)], Open(head).OrderBy(static t => t.Key, StringComparer.Ordinal).Select(static t => (t.Key, t.Value)));
    }

    [Fact]
    public async Task The_file_bytes_equal_the_golden_the_hook_test_reads()
    {
        _ = (await Files.Add("acme/api:feat/x", ItemId.From("ITM-0192a1b2c3d4e5f6a7b8c9d0e1f2a301"), 2071, Token)).Value;

        var written = await File.ReadAllBytesAsync(Path.Combine(Folder, FixHead.FileName("acme/api:feat/x")), Token);

        Assert.Equal(await File.ReadAllBytesAsync(Golden.SourcePath(typeof(FixTaskFilesTests).Assembly, "Golden/FixTasks/acme-api-feat-x.json"), Token), written);
    }
}
