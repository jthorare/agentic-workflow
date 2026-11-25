using Aw.Kernel.Testing;
using Aw.Sindri.Infrastructure.Workflow.Code;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Code;

/// <summary>The compiled-code cache stays bounded (#2727 follow-up, #2747): writing a new assembly removes the ones untouched for 30 days, the oldest past the file cap and leftover temporary files, and never one touched in the last hour.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeCachePruneTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("aw-code-prune-");
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    public void Dispose() => _directory.Delete(recursive: true);

    [Fact]
    public void An_assembly_untouched_for_31_days_is_removed_when_a_new_body_is_written()
    {
        var old = Plant("aaaa.dll", TimeSpan.FromDays(31));

        var written = Cache().Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(written.AssemblyPath));
    }

    [Fact]
    public void An_assembly_touched_10_days_ago_stays()
    {
        var recent = Plant("aaaa.dll", TimeSpan.FromDays(10));

        _ = Cache().Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void A_body_resolved_again_is_young_and_stays()
    {
        var cache = Cache();
        var used = cache.Resolve("return 1;", TestContext.Current.CancellationToken).AssemblyPath!;
        File.SetLastWriteTimeUtc(used, (_clock.GetUtcNow() - TimeSpan.FromDays(31)).UtcDateTime);

        _ = cache.Resolve("return 1;", TestContext.Current.CancellationToken);
        _ = cache.Resolve("return 2;", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(used));
    }

    [Fact]
    public void Past_the_file_cap_the_oldest_assemblies_go_first()
    {
        var five = Plant("aaaa.dll", TimeSpan.FromDays(5));
        var four = Plant("bbbb.dll", TimeSpan.FromDays(4));
        var three = Plant("cccc.dll", TimeSpan.FromDays(3));
        var two = Plant("dddd.dll", TimeSpan.FromDays(2));

        var written = Cache(maxFiles: 3).Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.False(File.Exists(five));
        Assert.False(File.Exists(four));
        Assert.True(File.Exists(three));
        Assert.True(File.Exists(two));
        Assert.True(File.Exists(written.AssemblyPath));
    }

    [Fact]
    public void An_assembly_touched_in_the_last_hour_is_never_removed_even_past_the_cap()
    {
        var fresh = Plant("aaaa.dll", TimeSpan.FromMinutes(10));

        _ = Cache(maxFiles: 1).Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public void A_leftover_temporary_file_older_than_an_hour_is_removed()
    {
        var leftover = Plant("aaaa.dll.0123.tmp", TimeSpan.FromHours(2));

        _ = Cache().Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.False(File.Exists(leftover));
    }

    [Fact]
    public void A_cache_hit_removes_nothing()
    {
        var cache = Cache();
        _ = cache.Resolve("return 1;", TestContext.Current.CancellationToken);
        var old = Plant("aaaa.dll", TimeSpan.FromDays(31));

        _ = cache.Resolve("return 1;", TestContext.Current.CancellationToken);

        Assert.True(File.Exists(old));
    }

    private CompiledCodeCache Cache(int maxFiles = CompiledCodeCache.DefaultMaxFiles) => new(new FakeCompiler(), _directory.FullName, _clock, maxFiles);

    private string Plant(string name, TimeSpan age)
    {
        var path = Path.Combine(_directory.FullName, name);
        File.WriteAllBytes(path, [1]);
        File.SetLastWriteTimeUtc(path, (_clock.GetUtcNow() - age).UtcDateTime);
        return path;
    }

    private sealed class FakeCompiler : ICodeCompiler
    {
        public string Fingerprint => "refs-1";

        public CodeCompilation Compile(string body, CancellationToken cancellationToken) => new([], [1, 2, 3]);
    }
}
