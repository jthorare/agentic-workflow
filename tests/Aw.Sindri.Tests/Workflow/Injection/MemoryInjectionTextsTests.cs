using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Infrastructure.Workflows;

namespace Aw.Sindri.Tests.Workflow.Injection;

/// <summary>The in-memory text cache: keyed by instance, state, run and input; bounded; dropped when a newer run or the Step's end arrives.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryInjectionTextsTests
{
    private static InjectionKey Key(string address, string run) => new(address, "checks", run, 1);

    private static FilledTexts Text(string text) => new(new Dictionary<string, string>(StringComparer.Ordinal) { ["s"] = text });

    [Fact]
    public void A_kept_text_is_read_back_by_its_key_only()
    {
        var cache = new MemoryInjectionTexts();
        cache.Put(Key("T-1/s/1", "1.1"), Text("kept"));

        Assert.Equal("kept", cache.Get(Key("T-1/s/1", "1.1"))!.Sections["s"]);
        Assert.Null(cache.Get(Key("T-1/s/1", "1.2")));
        Assert.Null(cache.Get(Key("T-2/s/1", "1.1")));
    }

    [Fact]
    public void MemoryInjectionTexts_drops_on_newer_token()
    {
        var cache = new MemoryInjectionTexts();
        cache.Put(Key("T-1/s/1", "1.1"), Text("old"));
        cache.Put(Key("T-2/s/1", "1.1"), Text("other instance"));

        cache.Put(Key("T-1/s/1", "1.2"), Text("new"));

        Assert.Null(cache.Get(Key("T-1/s/1", "1.1")));
        Assert.NotNull(cache.Get(Key("T-1/s/1", "1.2")));
        Assert.NotNull(cache.Get(Key("T-2/s/1", "1.1")));
    }

    [Fact]
    public void Drop_forgets_every_run_of_the_instance()
    {
        var cache = new MemoryInjectionTexts();
        cache.Put(Key("T-1/s/1", "1.1"), Text("a"));

        cache.Drop("T-1/s/1");

        Assert.Null(cache.Get(Key("T-1/s/1", "1.1")));
    }

    [Fact]
    public void MemoryInjectionTexts_keeps_more_than_the_old_64_entries()
    {
        var cache = new MemoryInjectionTexts();
        for (var i = 0; i < 200; i++)
        {
            cache.Put(Key(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"T-{i}/s/1"), "1.1"), Text("x"));
        }

        Assert.NotNull(cache.Get(Key("T-0/s/1", "1.1")));
        Assert.NotNull(cache.Get(Key("T-199/s/1", "1.1")));
    }

    [Fact]
    public void MemoryInjectionTexts_bounds_the_bytes()
    {
        var cache = new MemoryInjectionTexts();
        var big = new string('x', MemoryInjectionTexts.MaxBytes / 2);
        cache.Put(Key("T-1/s/1", "1.1"), Text(big));
        cache.Put(Key("T-2/s/1", "1.1"), Text(big));
        cache.Put(Key("T-3/s/1", "1.1"), Text(big));

        Assert.Null(cache.Get(Key("T-1/s/1", "1.1")));
        Assert.NotNull(cache.Get(Key("T-3/s/1", "1.1")));
    }

    [Fact]
    public void A_typed_result_is_sized_in_utf8_bytes_like_the_sections()
    {
        var result = System.Text.Json.JsonDocument.Parse("{\"note\":\"\u00e9\u00e9\u00e9\u00e9\"}").RootElement.Clone();   // 4 two-byte characters: the raw text has 4 fewer chars than bytes
        var texts = new FilledTexts(new Dictionary<string, string>(StringComparer.Ordinal), result);

        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(result.GetRawText()), MemoryInjectionTexts.Size(texts));
    }
}
