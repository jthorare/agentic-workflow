using Aw.Architecture.Tests.Rules;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-SETSID-001 (#2694): every perl POSIX::setsid() in src checks for -1, because perl treats -1 as true and "or die" never fires.</summary>
[Trait("Lane", "integration")]
public sealed class PerlSetsidTests
{
    [Fact]
    public void Every_perl_setsid_call_in_src_checks_for_minus_one()
    {
        var source = Path.Combine(Solution.RepoRoot, "src");
        Assert.True(Directory.Exists(source));
        var files = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(p => (Path: Path.GetRelativePath(Solution.RepoRoot, p).Replace('\\', '/'), Text: File.ReadAllText(p)))
            .ToList();
        Assert.NotEmpty(files);

        Verify.None(PerlSetsidRules.Check(files));
    }

    [Theory]
    [InlineData("perl -MPOSIX -e 'POSIX::setsid(); exec @ARGV'")]
    [InlineData("POSIX::setsid() or die \"setsid: $!\\n\"")]
    public void An_unchecked_setsid_call_is_reported(string line) =>
        Verify.Exactly(PerlSetsidRules.Check([("src/X/Stray.cs", line)]), PerlSetsidRules.Id, "src/X/Stray.cs:1 calls POSIX::setsid() without checking for -1");

    [Theory]
    [InlineData("POSIX::setsid() == -1 and die \"setsid: $!\\n\"; exec @ARGV")]
    [InlineData("POSIX::setsid()  ==  -1 and die")]
    public void A_setsid_call_checked_for_minus_one_is_not_reported(string line) =>
        Verify.None(PerlSetsidRules.Check([("src/X/Fine.cs", line)]));
}
