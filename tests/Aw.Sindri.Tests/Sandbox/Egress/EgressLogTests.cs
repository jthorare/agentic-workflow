using Aw.Sindri.Infrastructure.Sandbox;

namespace Aw.Sindri.Tests.Sandbox.Egress;

public sealed class EgressLogTests
{
    [Fact, Trait("Lane", "integration")]
    public void Egress_log_counts_per_host_without_urls()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "egress.log");
        var log = new EgressLog(path);

        log.Record("API.anthropic.com", 443, EgressVerdict.Allowed);
        log.Record("api.anthropic.com", 443, EgressVerdict.Allowed);
        log.Record("evil.example", 443, EgressVerdict.Denied);
        log.Flush();

        Assert.Equal(2, log.Count("api.anthropic.com", 443, EgressVerdict.Allowed));
        Assert.Equal(["api.anthropic.com\t443\tallowed\t2", "evil.example\t443\tdenied\t1"], File.ReadAllLines(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Concurrent_record_and_flush_leave_a_file_equal_to_the_counts()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "egress.log");
        var log = new EgressLog(path);

        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
        {
            for (var n = 0; n < 50; n++)
            {
                log.Record("api.anthropic.com", 443, EgressVerdict.Allowed);
                log.Flush();
            }
        }, TestContext.Current.CancellationToken)));
        log.Flush();

        Assert.Equal(["api.anthropic.com\t443\tallowed\t1600"], await File.ReadAllLinesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_long_host_is_sanitised_then_truncated()
    {
        var log = new EgressLog("/nonexistent/egress.log");
        var host = "A\tB" + new string('x', 300);

        log.Record(host, 443, EgressVerdict.Denied);

        Assert.Equal(1, log.Count(("a_b" + new string('x', 300))[..253], 443, EgressVerdict.Denied));
    }
}
