using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Launch.Trust;

/// <summary>Claude Code's trust file against a temp directory (never HOME): the pre-trust of a Step's worktree touches one key and nothing else.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ClaudeCodeTrustTests : IDisposable
{
    private const string Worktree = "/repo/.claude/worktrees/sindri-WFI-1-run";
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-trust-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Config => Path.Combine(_dir, ".claude.json");

    private ClaudeCodeTrust Make() => new(Config);

    private JsonObject Read() => (JsonObject)JsonNode.Parse(File.ReadAllText(Config))!;

    private static bool Trusted(JsonObject root, string path = Worktree) =>
        root["projects"]?[path]?["hasTrustDialogAccepted"]?.GetValue<bool>() == true;

    [Fact]
    public async Task A_missing_file_is_created_with_only_the_trust_key()
    {
        var result = await Make().Trust(Worktree, CancellationToken.None);

        Expect.True(result.IsSuccess);
        var root = Read();
        Expect.True(Trusted(root));
        Expect.Equal(1, root.Count);
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Config));
    }

    [Fact]
    public async Task Every_other_key_and_the_file_mode_survive()
    {
        await File.WriteAllTextAsync(Config, """{"numStartups":7,"userID":"abc","projects":{"/other":{"hasTrustDialogAccepted":true,"allowedTools":["Bash"]}},"big":12345678901234567890}""", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(Config, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        var result = await Make().Trust(Worktree, CancellationToken.None);

        Expect.True(result.IsSuccess);
        var root = Read();
        Expect.True(Trusted(root));
        Expect.Equal(7, root["numStartups"]!.GetValue<int>());
        Expect.Equal("abc", root["userID"]!.GetValue<string>());
        Expect.True(Trusted(root, "/other"));
        Expect.Equal("Bash", root["projects"]!["/other"]!["allowedTools"]![0]!.GetValue<string>());
        Expect.True((await File.ReadAllTextAsync(Config, TestContext.Current.CancellationToken)).Contains("12345678901234567890", StringComparison.Ordinal));
        Expect.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, File.GetUnixFileMode(Config));
        Expect.Equal(0, Directory.GetFiles(_dir).Count(f => !f.EndsWith(".claude.json", StringComparison.Ordinal) && !f.EndsWith(".sindri.lock", StringComparison.Ordinal)));   // no temp file left
    }

    [Fact]
    public async Task An_existing_entry_keeps_its_other_fields()
    {
        await File.WriteAllTextAsync(Config, """{"projects":{"@WT":{"hasTrustDialogAccepted":false,"history":[1]}}}""".Replace("@WT", Worktree, StringComparison.Ordinal), TestContext.Current.CancellationToken);

        _ = await Make().Trust(Worktree, CancellationToken.None);

        var entry = Read()["projects"]![Worktree]!;
        Expect.True(entry["hasTrustDialogAccepted"]!.GetValue<bool>());
        Expect.Equal(1, entry["history"]![0]!.GetValue<int>());
    }

    [Fact]
    public async Task An_already_trusted_directory_writes_nothing()
    {
        await File.WriteAllTextAsync(Config, """{"projects":{"@WT":{"hasTrustDialogAccepted":true}}}""".Replace("@WT", Worktree, StringComparison.Ordinal), TestContext.Current.CancellationToken);
        var before = File.GetLastWriteTimeUtc(Config);
        File.SetLastWriteTimeUtc(Config, before.AddHours(-1));
        var stamp = File.GetLastWriteTimeUtc(Config);

        var result = await Make().Trust(Worktree, CancellationToken.None);

        Expect.True(result.IsSuccess);
        Expect.Equal(stamp, File.GetLastWriteTimeUtc(Config));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"projects":[]}""")]
    [InlineData("""{"projects":{"/repo/.claude/worktrees/sindri-WFI-1-run":5}}""")]
    public async Task An_unusable_file_is_refused_and_left_untouched(string content)
    {
        await File.WriteAllTextAsync(Config, content, TestContext.Current.CancellationToken);

        var result = await Make().Trust(Worktree, CancellationToken.None);

        Expect.True(result.IsError);
        Expect.Equal(SindriErrors.LaunchTrustUnavailable.Code, result.Error.Code);
        Expect.Equal(content, await File.ReadAllTextAsync(Config, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_writer_that_changes_the_file_after_the_read_is_not_overwritten()
    {
        await File.WriteAllTextAsync(Config, """{"numStartups":1}""", TestContext.Current.CancellationToken);
        var raced = false;
        var trust = new ClaudeCodeTrust(Config)
        {
            AfterRead = () =>
            {
                if (!raced)
                {
                    raced = true;
                    File.WriteAllText(Config, """{"numStartups":2,"theirs":true}""");   // a running Claude rewrote the file
                }
            },
        };

        var result = await trust.Trust(Worktree, CancellationToken.None);

        Expect.True(result.IsSuccess);
        var root = Read();
        Expect.True(Trusted(root));
        Expect.Equal(2, root["numStartups"]!.GetValue<int>());
        Expect.True(root["theirs"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_held_lock_fails_the_call_after_its_wait_and_writes_nothing()
    {
        await File.WriteAllTextAsync(Config, """{"a":1}""", TestContext.Current.CancellationToken);
        await using var held = new FileStream(Config + ".sindri.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var trust = new ClaudeCodeTrust(Config) { LockWait = TimeSpan.FromMilliseconds(200) };

        var result = await trust.Trust(Worktree, CancellationToken.None);

        Expect.True(result.IsError);
        Expect.Equal("""{"a":1}""", await File.ReadAllTextAsync(Config, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Untrust_removes_only_that_entry()
    {
        await File.WriteAllTextAsync(Config, """{"keep":1,"projects":{"@WT":{"hasTrustDialogAccepted":true},"/other":{"hasTrustDialogAccepted":true}}}""".Replace("@WT", Worktree, StringComparison.Ordinal), TestContext.Current.CancellationToken);

        var result = await Make().Untrust(Worktree, CancellationToken.None);

        Expect.True(result.IsSuccess);
        var root = Read();
        Expect.True(root["projects"]![Worktree] is null);
        Expect.True(Trusted(root, "/other"));
        Expect.Equal(1, root["keep"]!.GetValue<int>());
    }

    [Fact]
    public async Task Untrust_of_an_absent_entry_or_file_is_a_success_that_writes_nothing()
    {
        var result = await Make().Untrust(Worktree, CancellationToken.None);

        Expect.True(result.IsSuccess);
        Expect.True(!File.Exists(Config));
    }

    [Fact]
    public async Task Untrust_refuses_an_unparseable_file_untouched()
    {
        await File.WriteAllTextAsync(Config, "{oops", TestContext.Current.CancellationToken);

        var result = await Make().Untrust(Worktree, CancellationToken.None);

        Expect.True(result.IsError);
        Expect.Equal("{oops", await File.ReadAllTextAsync(Config, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_relative_path_is_invalid()
    {
        var result = await Make().Trust("relative/dir", CancellationToken.None);

        Expect.True(result.IsError);
    }
}
