using Aw.Context.Core.Code;

namespace Aw.Context.Tests.Code;

public sealed class TestPathsTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("tests/sharing.test.ts", true)]
    [InlineData("src/sharing/links.spec.ts", true)]
    [InlineData("src/sharing/test/helper.ts", true)]
    [InlineData("src/__tests__/links.ts", true)]
    [InlineData("pkg/links_test.go", true)]
    [InlineData("src/LinkServiceTests.cs", true)]
    [InlineData("src/LinkServiceTest.java", true)]
    [InlineData("tests/test_links.py", true)]
    [InlineData("src/sharing/links.ts", false)]
    [InlineData("src/sharing/testing.ts", false)]
    [InlineData("src/FooTest.cs", true)]
    [InlineData("src/foo.test.ts", true)]
    [InlineData("pkg/foo_test.go", true)]
    [InlineData("src/Links.Tests/Links.cs", false)]
    [InlineData("Tests/Links.cs", true)]
    [InlineData("src/latest.ts", false)]
    [InlineData("src/contest.js", false)]
    [InlineData("src/attest.py", false)]
    public void TestPath_configured_globs_mark_the_usual_test_files(string path, bool expected) =>
        Assert.Equal(expected, TestPaths.IsTest(path, _usual));

    // why: the engine has no built-in list; these are what a profile would write
    private static readonly string[] _usual =
        ["**/test/**", "**/tests/**", "**/Test/**", "**/Tests/**", "**/__tests__/**", "**/spec/**", "**/specs/**", "**/*.test.*", "**/*.spec.*", "**/*_test.*", "**/*Test.*", "**/*Tests.*", "**/test_*.*"];

    [Fact, Trait("Lane", "unit")]
    public void TestPath_only_the_written_globs_count()
    {
        string[] globs = ["checks/**"];

        Assert.True(TestPaths.IsTest("checks/links.ts", globs));
        Assert.False(TestPaths.IsTest("tests/links.test.ts", globs));
    }

    [Fact, Trait("Lane", "unit")]
    public void TestPath_globs_match_case_sensitively()
    {
        Assert.False(TestPaths.IsTest("src/Links.TEST.ts", _usual));
        Assert.False(TestPaths.IsTest("src/TESTS/links.ts", _usual));
    }

    [Fact, Trait("Lane", "unit")]
    public void TestPath_no_globs_mark_nothing() =>
        Assert.False(TestPaths.IsTest("tests/links.test.ts", []));
}
