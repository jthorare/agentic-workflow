using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Tests.Snapshot;

public sealed class InputRulesTests
{
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");
    private static readonly SnapshotLimits _limits = new([], MaxFileBytes: 100, MaxTotalBytes: 1_000);

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/A.cs")]
    [InlineData("a/b/c.txt")]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData("dir/.gitignore")]
    [InlineData("name with spaces.txt")]
    [InlineData("a/..b/c")]
    public void An_ordinary_path_is_safe(string path) => Assert.True(InputRules.IsSafePath(path));

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("/etc/passwd")]
    [InlineData("a//b")]
    [InlineData("a/")]
    [InlineData("../x")]
    [InlineData("a/../b")]
    [InlineData(".git/config")]
    [InlineData("a/.GIT/hooks/pre-commit")]
    [InlineData("a/.Git")]
    [InlineData("a\\b")]
    [InlineData("a\0b")]
    public void A_path_with_an_empty_dotdot_dotgit_backslash_or_nul_component_or_an_absolute_path_is_unsafe(string path)
        => Assert.False(InputRules.IsSafePath(path));

    [Theory, Trait("Lane", "unit")]
    [InlineData(".env")]
    [InlineData(".env.local")]
    [InlineData("config/.ENV.production")]
    [InlineData("keys/server.PEM")]
    [InlineData("deploy/id_rsa")]
    [InlineData("deploy/id_rsa.pub")]
    [InlineData("infra/terraform.tfstate")]
    [InlineData("infra/prod.tfvars")]
    [InlineData("cfg/credentials.json")]
    [InlineData("home/.npmrc")]
    [InlineData("home/.netrc")]
    [InlineData("app/secrets/db.txt")]
    [InlineData("cert.p12")]
    [InlineData("cert.pfx")]
    [InlineData("a/b/tls.key")]
    public void The_built_in_secret_globs_deny_without_regard_to_case(string path) => Assert.True(InputRules.IsDenied(path, []));

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/environment.cs")]
    [InlineData("docs/keys.md")]
    [InlineData("src/secretsanta/a.cs")]
    [InlineData("pem.txt")]
    public void A_path_that_only_looks_like_a_secret_is_not_denied(string path) => Assert.False(InputRules.IsDenied(path, []));

    [Fact, Trait("Lane", "unit")]
    public void Added_globs_deny_more_and_the_built_in_ones_stay()
    {
        string[] added = ["vendor/**", "*.gen.cs", "docs/?.md"];

        Assert.True(InputRules.IsDenied("vendor/lib/a.cs", added));
        Assert.True(InputRules.IsDenied("VENDOR/x", added));
        Assert.True(InputRules.IsDenied("Model.GEN.cs", added));
        Assert.False(InputRules.IsDenied("src/Model.gen.cs", added));   // '*' stays inside one segment
        Assert.True(InputRules.IsDenied("docs/a.md", added));
        Assert.False(InputRules.IsDenied("docs/ab.md", added));
        Assert.True(InputRules.IsDenied(".env", added));
    }

    [Fact, Trait("Lane", "unit")]
    public void Apply_keeps_regular_files_in_path_order_with_their_blob_and_size()
    {
        var result = InputRules.Apply(_commit, [Regular("src/B.cs", "bbb", 20), Regular("a.txt", "aaa", 10), Entry("100755", "run.sh", "ccc", 5)], _limits);

        var inventory = result.Value;
        Assert.Equal(_commit, inventory.Commit);
        Assert.Equal(["a.txt", "run.sh", "src/B.cs"], inventory.Files.Select(f => f.Path).ToList());
        Assert.Equal(new SnapshotFile("src/B.cs", "bbb", 20), inventory.Files[2]);
        Assert.Empty(inventory.Skipped);
    }

    [Fact, Trait("Lane", "unit")]
    public void Apply_counts_each_skipped_entry_once_by_the_first_reason_that_fits()
    {
        TreeEntry[] entries =
        [
            Regular("ok.cs", "1", 1),
            Entry("120000", "link", "2", 4),
            Entry("120000", ".env", "3", 4),            // a symlink first, even though it is also denied
            Entry("160000", "vendor/module", "4", size: null),
            Regular("keys/server.pem", "5", 1),
            Regular("a/../b.cs", "6", 1),
            Regular("big.bin", "7", 101),
            Regular("gone.cs", "8", size: null),
            Regular("a/.git/x", "9", 999),              // unsafe first, even though it is also too large
            Entry("100664", "odd-mode.cs", "a", 1),
        ];

        var skipped = InputRules.Apply(_commit, entries, _limits).Value.Skipped;

        Assert.Equal(2, skipped[SkipReason.Symlink]);
        Assert.Equal(1, skipped[SkipReason.Submodule]);
        Assert.Equal(1, skipped[SkipReason.Denied]);
        Assert.Equal(3, skipped[SkipReason.UnsafePath]);   // '..', '.git' and the mode the spec doesn't name
        Assert.Equal(1, skipped[SkipReason.TooLarge]);
        Assert.Equal(1, skipped[SkipReason.Missing]);
    }

    [Fact, Trait("Lane", "unit")]
    public void Apply_uses_the_limits_added_globs()
    {
        var inventory = InputRules.Apply(_commit, [Regular("gen/a.cs", "1", 1), Regular("src/a.cs", "2", 1)], _limits with { DenyPaths = ["gen/**"] }).Value;

        Assert.Equal("src/a.cs", Assert.Single(inventory.Files).Path);
        Assert.Equal(1, inventory.Skipped[SkipReason.Denied]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_total_cap_counts_only_kept_code_files_and_stops_with_SND_CODEIDX_003_one_byte_over()
    {
        var atCap = InputRules.Apply(_commit, [Regular("a.cs", "1", 60), Regular("b.ts", "2", 40), Regular("big.cs", "3", 5_000)], _limits with { MaxTotalBytes = 100 });
        var over = InputRules.Apply(_commit, [Regular("a.cs", "1", 60), Regular("b.ts", "2", 41)], _limits with { MaxTotalBytes = 100 });

        Assert.False(atCap.IsError);
        Assert.Equal("SND-CODEIDX-003", over.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("src/Util/Strings.cs", "src/Util/**", true)]
    [InlineData("src/util/strings.cs", "SRC/UTIL/*.cs", true)]
    [InlineData("src/Other/Strings.cs", "src/Util/**", false)]
    [InlineData("a/b/c.cs", "**/c.cs", true)]
    [InlineData("a.cs", "", false)]
    public void MatchesAny_applies_a_glob_ignoring_case(string path, string glob, bool expected)
        => Assert.Equal(expected, InputRules.MatchesAny(path, [glob]));

    private static TreeEntry Regular(string path, string blob, long? size) => Entry("100644", path, blob, size);

    private static TreeEntry Entry(string mode, string path, string blob, long? size) => new(mode, path, blob, size);
}
