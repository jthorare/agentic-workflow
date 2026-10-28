using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Extract;

/// <summary>The repo reader's contract (S-4): the fake and the git reader pass the same tests, and a build reads a commit, never the working tree.</summary>
public abstract class RepoReaderContract
{
    /// <summary>The files every reader's repo holds at its commit.</summary>
    public static IReadOnlyDictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["src/commands/registry.ts"] = "export type CommandId = 'pane.split' | 'pane.close';\n",
        ["README.md"] = "# Split panes\n",
        ["docs/guide.md"] = "Guide\n",
    };

    /// <summary>Files committed that extraction must never see: the built-in secret globs deny them.</summary>
    public static IReadOnlyDictionary<string, string> Denied { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [".env"] = "TOKEN=hunter2\n",
        ["config/secrets/token.txt"] = "hunter2\n",
        ["certs/server.pem"] = "-----BEGIN PRIVATE KEY-----\n",
    };

    /// <summary>Everything a reader's repo holds at its commit: <see cref="Files"/> and <see cref="Denied"/>.</summary>
    public static IReadOnlyDictionary<string, string> Committed { get; } = Files.Concat(Denied).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>The commit lists its files in ordinal order.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_head_commit_lists_its_files_in_ordinal_order()
    {
        const string test = nameof(The_head_commit_lists_its_files_in_ordinal_order);
        var reader = CreateReader(out var repo, out var commit);

        var head = Expect.Ok(await reader.Head(repo, None), test);
        var listed = Expect.Ok(await reader.ListFiles(repo, head, None), test);

        Expect.That(head == commit, test, $"HEAD was {head}");
        Expect.That(listed.SequenceEqual(Files.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal), test, $"listed {string.Join(", ", listed)}");
    }

    /// <summary>A file reads as it was committed.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_file_reads_as_committed()
    {
        const string test = nameof(A_file_reads_as_committed);
        var reader = CreateReader(out var repo, out var commit);

        var text = Expect.Ok(await reader.Read(repo, commit, "src/commands/registry.ts", None), test);

        Expect.That(text == Files["src/commands/registry.ts"], test, "the text changed");
    }

    /// <summary>A missing repo, commit or path is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_missing_repo_commit_or_path_is_unreadable()
    {
        const string test = nameof(A_missing_repo_commit_or_path_is_unreadable);
        var reader = CreateReader(out var repo, out var commit);
        var missing = new ContextRepo(ContextRepoName.From("nowhere"), Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));
        const string unknownCommit = "0123456789abcdef0123456789abcdef01234567";

        var results = new[]
        {
            (await reader.Head(missing, None)).IsError ? (await reader.Head(missing, None)).Error.Code : "ok",
            (await reader.ListFiles(repo, unknownCommit, None)).IsError ? (await reader.ListFiles(repo, unknownCommit, None)).Error.Code : "ok",
            (await reader.Read(repo, commit, "src/missing.ts", None)).IsError ? (await reader.Read(repo, commit, "src/missing.ts", None)).Error.Code : "ok",
            (await reader.Read(repo, commit, "../escape", None)).IsError ? (await reader.Read(repo, commit, "../escape", None)).Error.Code : "ok",
        };

        Expect.That(results.All(code => code == "SND-CONTEXT-048"), test, $"codes were {string.Join(", ", results)}");
    }

    /// <summary>A path the repo denies is never listed and never read (the profile's deny paths reach every file the extractor reads).</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_denied_path_is_never_listed_or_read()
    {
        const string test = nameof(A_denied_path_is_never_listed_or_read);
        var reader = CreateReader(out var repo, out var commit);
        var custom = repo with { DenyPaths = [.. repo.DenyPaths, "docs/**"] };

        var listed = Expect.Ok(await reader.ListFiles(custom, commit, None), test);
        var env = await reader.Read(custom, commit, ".env", None);
        var secret = await reader.Read(custom, commit, "config/secrets/token.txt", None);
        var docsRead = await reader.Read(custom, commit, "docs/guide.md", None);

        Expect.That(listed.SequenceEqual(["README.md", "src/commands/registry.ts"], StringComparer.Ordinal), test, $"listed {string.Join(", ", listed)}");
        Expect.That(env.IsError && secret.IsError && docsRead.IsError, test, "a denied path was read");
        Expect.That(new[] { env, secret, docsRead }.All(r => r.Error.Code == "SND-CONTEXT-048"), test, "a denied read wasn't SND-CONTEXT-048");
    }

    /// <summary>The range from the merge base lists exactly what the branch changed, and nothing 'main' changed after the branch point.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_lists_name_status_from_the_merge_base_to_head()
    {
        const string test = nameof(Changes_lists_name_status_from_the_merge_base_to_head);
        var reader = CreateChangeReader(out var repo, out var world);

        var changes = Expect.Ok(await reader.Changes(repo, new DiffRange(world.Base, world.Head), None), test);

        Expect.That((changes.Base, changes.Head, changes.MergeBase) == (world.Base, world.Head, world.MergeBase), test, $"shas were {changes.Base}, {changes.Head}, {changes.MergeBase}");
        Expect.That(changes.Files.SequenceEqual(world.Expected), test, $"files were {string.Join(", ", changes.Files.Select(f => f.Path + ":" + f.Change))}");
    }

    /// <summary>An abbreviated sha comes back as the full one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_answers_full_shas_for_abbreviated_ones()
    {
        const string test = nameof(Changes_answers_full_shas_for_abbreviated_ones);
        var reader = CreateChangeReader(out var repo, out var world);

        var changes = Expect.Ok(await reader.Changes(repo, new DiffRange(world.Base[..9], world.Head[..9]), None), test);

        Expect.That((changes.Base, changes.Head, changes.MergeBase) == (world.Base, world.Head, world.MergeBase), test, $"shas were {changes.Base}, {changes.Head}, {changes.MergeBase}");
    }

    /// <summary>A sha the clone doesn't hold is SND-CONTEXT-110, on either side.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_with_a_sha_the_clone_lacks_is_110()
    {
        const string test = nameof(Changes_with_a_sha_the_clone_lacks_is_110);
        var reader = CreateChangeReader(out var repo, out var world);

        var first = await reader.Changes(repo, new DiffRange(ChangeWorld.Absent, world.Head), None);
        var second = await reader.Changes(repo, new DiffRange(world.Base, ChangeWorld.Absent), None);

        Expect.That(first.IsError && second.IsError, test, "a missing commit was accepted");
        Expect.That(first.Error.Code == "SND-CONTEXT-110" && second.Error.Code == "SND-CONTEXT-110", test, $"codes were {first.Error.Code}, {second.Error.Code}");
    }

    /// <summary>Commits with no history in common are SND-CONTEXT-111.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_with_unrelated_histories_is_111()
    {
        const string test = nameof(Changes_with_unrelated_histories_is_111);
        var reader = CreateChangeReader(out var repo, out var world);

        var result = await reader.Changes(repo, new DiffRange(world.Unrelated, world.Head), None);

        Expect.That(result.IsError && result.Error.Code == "SND-CONTEXT-111", test, result.IsError ? result.Error.Code : "an answer");
    }

    /// <summary>A rename is a deletion plus an addition, whatever the repo's own diff settings say.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_treats_a_rename_as_delete_and_add()
    {
        const string test = nameof(Changes_treats_a_rename_as_delete_and_add);
        var reader = CreateChangeReader(out var repo, out var world);

        var changes = Expect.Ok(await reader.Changes(repo, new DiffRange(world.Head, world.RenameHead), None), test);

        Expect.That(changes.Files.SequenceEqual(world.RenameExpected), test, $"files were {string.Join(", ", changes.Files.Select(f => f.Path + ":" + f.Change))}");
    }

    /// <summary>A commit against itself changes nothing.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_of_head_against_itself_is_empty()
    {
        const string test = nameof(Changes_of_head_against_itself_is_empty);
        var reader = CreateChangeReader(out var repo, out var world);

        var changes = Expect.Ok(await reader.Changes(repo, new DiffRange(world.Head, world.Head), None), test);

        Expect.That(changes.Files.Count == 0 && changes.MergeBase == world.Head, test, $"files {changes.Files.Count}, merge base {changes.MergeBase}");
    }

    /// <summary>A missing checkout is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Changes_on_a_missing_checkout_is_048()
    {
        const string test = nameof(Changes_on_a_missing_checkout_is_048);
        var reader = CreateChangeReader(out _, out var world);
        var missing = new ContextRepo(ContextRepoName.From("nowhere"), Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));

        var result = await reader.Changes(missing, new DiffRange(world.Base, world.Head), None);

        Expect.That(result.IsError && result.Error.Code == "SND-CONTEXT-048", test, result.IsError ? result.Error.Code : "an answer");
    }

    /// <summary>Each present path answers its git blob id, the same id git gives the committed text.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task BlobIds_answers_the_git_blob_id_of_each_present_path()
    {
        const string test = nameof(BlobIds_answers_the_git_blob_id_of_each_present_path);
        var reader = CreateReader(out var repo, out var commit);

        var ids = Expect.Ok(await reader.BlobIds(repo, commit, ["README.md", "src/commands/registry.ts"], None), test);

        Expect.That(ids.Count == 2, test, $"answered {ids.Count} paths");
        Expect.That(ids["README.md"] == FakeRepoReader.BlobId(Files["README.md"]), test, "README.md's id is not the blob id of its text");
        Expect.That(ids["src/commands/registry.ts"] == FakeRepoReader.BlobId(Files["src/commands/registry.ts"]), test, "registry.ts's id is not the blob id of its text");
    }

    /// <summary>A missing path, a directory and a path the repo denies are left out; the others are still answered.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task BlobIds_leaves_out_a_missing_path_a_directory_and_a_denied_path()
    {
        const string test = nameof(BlobIds_leaves_out_a_missing_path_a_directory_and_a_denied_path);
        var reader = CreateReader(out var repo, out var commit);
        var custom = repo with { DenyPaths = [.. repo.DenyPaths, "docs/**"] };

        var ids = Expect.Ok(await reader.BlobIds(custom, commit, ["README.md", "src/missing.ts", "src/commands", "docs/guide.md", ".env"], None), test);

        Expect.That(ids.Keys.SequenceEqual(["README.md"], StringComparer.Ordinal), test, $"answered {string.Join(", ", ids.Keys)}");
    }

    /// <summary>A path inside a directory is found in the same read as a top-level one.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task BlobIds_finds_a_path_under_a_directory()
    {
        const string test = nameof(BlobIds_finds_a_path_under_a_directory);
        var reader = CreateReader(out var repo, out var commit);

        var ids = Expect.Ok(await reader.BlobIds(repo, commit, ["docs/guide.md"], None), test);

        Expect.That(ids.Count == 1 && ids["docs/guide.md"] == FakeRepoReader.BlobId(Files["docs/guide.md"]), test, $"answered {string.Join(", ", ids.Keys)}");
    }

    /// <summary>No paths is an empty answer, even for a commit the repo lacks.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task BlobIds_of_no_paths_is_empty()
    {
        const string test = nameof(BlobIds_of_no_paths_is_empty);
        var reader = CreateReader(out var repo, out var commit);

        var ids = Expect.Ok(await reader.BlobIds(repo, commit, [], None), test);

        Expect.That(ids.Count == 0, test, $"answered {ids.Count} paths");
    }

    /// <summary>A missing repo or a commit it lacks is SND-CONTEXT-048.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task BlobIds_on_a_missing_repo_or_commit_is_unreadable()
    {
        const string test = nameof(BlobIds_on_a_missing_repo_or_commit_is_unreadable);
        var reader = CreateReader(out var repo, out _);
        var missing = new ContextRepo(ContextRepoName.From("nowhere"), Path.Combine(Path.GetTempPath(), "aw-no-such-checkout"));

        var noRepo = await reader.BlobIds(missing, "0123456789abcdef0123456789abcdef01234567", ["README.md"], None);
        var noCommit = await reader.BlobIds(repo, "0123456789abcdef0123456789abcdef01234567", ["README.md"], None);

        Expect.That(noRepo.IsError && noCommit.IsError, test, "an unreadable repo or commit was answered");
        Expect.That(noRepo.Error.Code == "SND-CONTEXT-048" && noCommit.Error.Code == "SND-CONTEXT-048", test, $"codes were {noRepo.Error.Code}, {noCommit.Error.Code}");
    }

    /// <summary>A reader with a repo that holds the history of <see cref="ChangeWorld"/>.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="world">The commits and what they changed.</param>
    /// <returns>The reader.</returns>
    protected abstract IRepoReader CreateChangeReader(out ContextRepo repo, out ChangeWorld world);

    /// <summary>A reader with a repo that holds <see cref="Committed"/> at one commit.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">Its HEAD commit.</param>
    /// <returns>The reader.</returns>
    protected abstract IRepoReader CreateReader(out ContextRepo repo, out string commit);
}
