using Aw.Kernel.Testing;
using Aw.Sindri.Core.Checks;

namespace Aw.Sindri.Tests.Workflow.BuildShip;

/// <summary>The pure build rules (line spec §7.2): the write-scope globs, the test-path convention, the scope gate, the existing-test rule and the commit rule.</summary>
[Lane(TestLane.Unit)]
public sealed class BranchRulesTests
{
    private static readonly PhaseScope _scope = new("b0", ["src/**", "docs/*.md", "new: tools/gen.sh", "lib/?.cs"], ["tests/old.test.ts"]);

    [Theory]
    [InlineData("src/a.cs", true)]
    [InlineData("src/deep/er/a.cs", true)]
    [InlineData("src", true)]
    [InlineData("docs/readme.md", true)]
    [InlineData("docs/sub/readme.md", false)]
    [InlineData("tools/gen.sh", true)]
    [InlineData("tools/other.sh", false)]
    [InlineData("lib/a.cs", true)]
    [InlineData("lib/ab.cs", false)]
    [InlineData("other/src/a.cs", false)]
    public void A_path_is_in_scope_when_a_glob_or_a_new_entry_names_it(string path, bool allowed) =>
        Assert.Equal(allowed, ScopeGlobs.Allows(_scope.AllowedFiles, path));

    [Theory]
    [InlineData("tests/a.cs", true)]
    [InlineData("src/__tests__/a.ts", true)]
    [InlineData("src/a.test.ts", true)]
    [InlineData("src/a.spec.ts", true)]
    [InlineData("src/FooTests.cs", true)]
    [InlineData("src/test_foo.py", true)]
    [InlineData("src/foo_test.go", true)]
    [InlineData("src/contest.cs", false)]
    [InlineData("src/Tests.cs", false)]
    [InlineData("src/attestation.ts", false)]
    public void A_test_file_is_known_by_its_directory_or_its_name(string path, bool isTest) => Assert.Equal(isTest, TestPaths.IsTest(path));

    [Fact]
    public void A_path_outside_the_globs_is_out_of_scope_unless_it_is_a_new_test_file()
    {
        var changes = new BranchChanges(
            [new ChangedFile("src/ok.cs", ChangeKind.Added), new ChangedFile("etc/conf.yml", ChangeKind.Modified), new ChangedFile("etc/new.test.ts", ChangeKind.Added), new ChangedFile("etc/old.test.ts", ChangeKind.Modified)], [], []);

        var findings = BranchRules.OutOfScope(_scope, changes);

        Assert.Equal(["etc/conf.yml", "etc/old.test.ts"], findings.Select(static f => f.Path), StringComparer.Ordinal);
        Assert.All(findings, static f => Assert.Equal("BLD-SCOPE-001", f.Rule));
    }

    [Fact]
    public void An_existing_test_changed_or_deleted_is_a_finding_unless_test_change_names_it()
    {
        var changes = new BranchChanges(
            [new ChangedFile("tests/old.test.ts", ChangeKind.Modified), new ChangedFile("tests/other.test.ts", ChangeKind.Modified), new ChangedFile("tests/gone.test.ts", ChangeKind.Deleted), new ChangedFile("tests/new.test.ts", ChangeKind.Added)], [], []);

        var findings = BranchRules.ChangedTests(_scope, changes);

        Assert.Equal(["tests/other.test.ts", "tests/gone.test.ts"], findings.Select(static f => f.Path), StringComparer.Ordinal);
        Assert.Contains("deleted", findings[1].Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("feat: add a thing", "feat")]
    [InlineData("fix(parser): handle empty", "fix")]
    [InlineData("refactor!: drop the old api", "refactor")]
    [InlineData("test: red", "test")]
    [InlineData("Fix the thing", null)]
    [InlineData("feat:no space", null)]
    [InlineData("wip: half done", null)]
    [InlineData("feat: ", null)]
    [InlineData("feat(): x", null)]
    public void A_commit_subject_is_conventional_when_it_is_type_colon_description(string subject, string? type) => Assert.Equal(type, BranchRules.CommitType(subject));

    [Fact]
    public void A_merge_or_an_unconventional_commit_is_a_finding_that_names_its_short_sha()
    {
        var changes = new BranchChanges([], [], [new ChangedCommit("aaaaaaaa1111", "feat: ok", IsMerge: false), new ChangedCommit("bbbbbbbb2222", "Merge x", IsMerge: true), new ChangedCommit("cccccccc3333", "whatever", IsMerge: false)]);

        var findings = BranchRules.Commits(changes);

        Assert.Equal(2, findings.Count);
        Assert.Contains("bbbbbbbb", findings[0].Message, StringComparison.Ordinal);
        Assert.Contains("cccccccc", findings[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_phase_scope_reads_the_base_the_globs_and_the_test_change_paths()
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["packet.json"] = """{"baseSha":"abc"}""",
            ["phase.json"] = """{"allowedFiles":["src/**"],"testChange":[{"path":"a.test.ts","reason":"r"},"b.test.ts"]}""",
        };

        var scope = PhaseScope.From(files);

        Assert.Equal("abc", scope.Value.BaseSha);
        Assert.Equal(["src/**"], scope.Value.AllowedFiles);
        Assert.Equal(["a.test.ts", "b.test.ts"], scope.Value.TestChange);
    }

    [Fact]
    public void A_packet_without_a_base_or_allowed_files_is_an_error()
    {
        Assert.True(PhaseScope.From(new Dictionary<string, string>(StringComparer.Ordinal) { ["packet.json"] = "{}", ["phase.json"] = """{"allowedFiles":[]}""" }).IsError);
        Assert.True(PhaseScope.From(new Dictionary<string, string>(StringComparer.Ordinal) { ["packet.json"] = """{"baseSha":"a"}""", ["phase.json"] = "not json" }).IsError);
    }
}
