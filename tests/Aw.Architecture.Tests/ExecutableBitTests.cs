using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-EXEC-001 (#2217): no source file but Aw.Kernel.Data's ExecutableFiles tests an execute bit through File.GetUnixFileMode.</summary>
[Trait("Lane", "integration")]
public sealed class ExecutableBitTests
{
    private const string Helper = "src/Aw.Kernel.Data/ExecutableFiles.cs";

    [Fact]
    public void Only_the_helper_tests_an_execute_bit()
    {
        var source = Path.Combine(Solution.RepoRoot, "src");
        Assert.True(Directory.Exists(source));
        var files = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(p => (Path: Path.GetRelativePath(Solution.RepoRoot, p).Replace('\\', '/'), Text: File.ReadAllText(p)))
            .ToList();
        Assert.Contains(files, f => f.Path == Helper);

        Verify.None(ExecutableBitRules.Check(files, Helper));
    }

    [Theory]
    [InlineData("return (File.GetUnixFileMode(p) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute)) != UnixFileMode.None;")]
    [InlineData("File.Exists(p) && File.GetUnixFileMode(p).HasFlag(UnixFileMode.OtherExecute)")]
    [InlineData("(File.GetUnixFileMode(file) & AnyExecute) != UnixFileMode.None")]
    public void A_masked_execute_bit_is_reported(string line) =>
        Verify.Exactly(ExecutableBitRules.Check([("src/X/Stray.cs", line)], Helper), ExecutableBitRules.Id, "src/X/Stray.cs tests an execute bit");

    [Theory]
    [InlineData("(File.GetUnixFileMode(path) & GroupOrOther) != UnixFileMode.None")]
    [InlineData("File.GetUnixFileMode(directory) == (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)")]
    [InlineData("File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));")]
    public void A_permission_check_that_is_not_an_execute_test_is_not_reported(string line) =>
        Verify.None(ExecutableBitRules.Check([("src/X/Fine.cs", line)], Helper));

    [Fact]
    public void The_helper_itself_is_not_reported() =>
        Verify.None(ExecutableBitRules.Check([(Helper, "(File.GetUnixFileMode(path) & bits) != UnixFileMode.None && UnixFileMode.UserExecute")], Helper));
}
