using Aw.Context.Core.Library;
using Aw.Context.Infrastructure.Docs;
using Aw.Context.Infrastructure.Store;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.Library;

/// <summary>The library status file: near misses round-trip as findings, and a file written before findings (bare paths) still reads.</summary>
[Trait("Lane", "integration")]
public sealed class FileLibraryStatusTests : IDisposable
{
    private static readonly DateTimeOffset _at = new(2026, 11, 6, 9, 30, 0, TimeSpan.Zero);

    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    private FileLibraryStatus Status() => new(new ContextStoreSettings(_home.Root));

    [Fact]
    public void A_near_miss_is_written_and_read_back_with_its_path_line_message_and_fix()
    {
        var miss = new DesignDocFinding("plans/quoted-one.md", new DesignDocError(DesignDocCodes.NearMiss, 2, "plans/quoted-one.md:2: aw-doc is \"1\", not the number 1, so the file isn't read.", "Write aw-doc: 1 with no quotes, on the line after the first ---."));
        Status().Write([new LibraryRootReport("work", "/srv/work", Problem: null, Docs: 3, [miss], _at)]);

        var report = Assert.Single(Status().Read());

        Assert.Equal((3, "/srv/work"), (report.Docs, report.Path));
        Assert.Equal([miss], report.NearMisses);
    }

    [Fact]
    public void A_status_file_with_bare_near_miss_paths_reads_them_as_019_until_the_next_pass()
    {
        File.WriteAllText(Path.Combine(_home.Root, "library-status.json"), """[{"root":"work","path":"/srv/work","problem":null,"docs":3,"nearMisses":["plans/a.md"],"lastRead":"2026-11-06T09:30:00Z"}]""");

        var report = Assert.Single(Status().Read());

        Assert.Equal(3, report.Docs);
        Assert.Equal([new DesignDocFinding("plans/a.md", new DesignDocError("SND-LIB-019", Line: null, "a near miss (read again on the next pass)", Fix: ""))], report.NearMisses);
    }
}
