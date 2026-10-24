using System.Diagnostics;
using System.Text;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Infrastructure.Parsing;
using Aw.Kernel.Testing;

namespace Aw.CodeIndex.Tests.Parsing;

// The CI12 gate (spec §9, §11): the hostile files must end the way the manifest says, inside the guards, in this process.
public sealed class HostileParseTests : IDisposable
{
    private static readonly CSharpParser _parser = new();
    private static readonly string[] _boundedSkips = ["too-deep", "too-many-call-sites"];
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aw-hostile-" + RealTime.NewGuid().ToString("N"));
    private readonly IReadOnlyList<(string Path, long Size, string Outcome)> _manifest;

    public HostileParseTests()
    {
        _manifest = FixtureSupport.GenerateHostile(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private ParseOutcome ParseFile(string relative, CSharpParser? parser = null)
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(_directory, relative)));
        return (parser ?? _parser).Parse(relative, text, TestContext.Current.CancellationToken).Value;
    }

    [Fact, Trait("Lane", "integration")]
    public void The_three_depth_files_end_unparsed_too_deep_and_the_process_lives()
    {
        foreach (var row in _manifest.Where(r => r.Outcome == "too-deep"))
        {
            var outcome = ParseFile(row.Path);

            Assert.True(outcome.Skip == "too-deep", $"{row.Path} ended {outcome.Skip ?? "parsed"}");
            Assert.Empty(outcome.Symbols);
        }

        Assert.Equal(3, _manifest.Count(r => r.Outcome == "too-deep"));
    }

    [Fact, Trait("Lane", "integration")]
    public void The_other_deep_files_end_bounded_inside_the_time_limit()
    {
        foreach (var row in _manifest.Where(r => r.Outcome == "bounded"))
        {
            var clock = Stopwatch.StartNew();
            var outcome = ParseFile(row.Path);

            Assert.True(outcome.Skip is null || _boundedSkips.Contains(outcome.Skip), $"{row.Path} ended {outcome.Skip}");
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(15), $"{row.Path} took {clock.Elapsed}");
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void The_surviving_files_parse_without_a_skip()
    {
        foreach (var row in _manifest.Where(r => r.Outcome == "survives"))
        {
            Assert.True(ParseFile(row.Path).Skip is null, row.Path);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public void The_wide_files_still_give_their_symbols()
    {
        Assert.Contains(ParseFile("generated/huge-line.cs").Symbols, s => s.Name == "After");
        Assert.Contains(ParseFile("generated/at-cap-boundary.cs").Symbols, s => s.Name == "Inside");
        Assert.Contains(ParseFile("generated/long-identifier.cs").Symbols, s => s.Name.Length == 100_000);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_self_recursive_file_gives_symbols_and_calls_without_looping()
    {
        var outcome = ParseFile("static/self-recursive.cs");

        Assert.NotEmpty(outcome.Symbols);
        Assert.Contains(outcome.Symbols, s => s.CallSites.Count > 0);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_files_over_a_ceiling_end_with_that_ceilings_word()
    {
        var oldCeilings = new CSharpParser(ParseLimits.Default with { MaxSymbols = 5_000, MaxCallSites = 20_000 });

        Assert.Equal("too-many-symbols", ParseFile("generated/symbols-6000.cs", oldCeilings).Skip);
        Assert.Equal("too-many-call-sites", ParseFile("generated/call-sites-25000.cs", oldCeilings).Skip);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_generated_files_past_the_old_ceilings_parse_under_the_default_limits()
    {
        Assert.Null(ParseFile("generated/symbols-6000.cs").Skip);
        Assert.Null(ParseFile("generated/call-sites-25000.cs").Skip);
    }
}
