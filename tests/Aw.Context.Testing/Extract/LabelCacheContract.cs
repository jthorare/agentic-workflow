using Aw.Context.Core.Extract;
using Aw.Context.Testing.Map;
using Xunit;

namespace Aw.Context.Testing.Extract;

/// <summary>The label cache's contract: the fake and the SQLite store pass the same tests.</summary>
public abstract class LabelCacheContract
{
    private static CancellationToken None => TestContext.Current.CancellationToken;

    /// <summary>Builds an empty cache.</summary>
    /// <returns>The cache.</returns>
    protected abstract ILabelCache CreateCache();

    /// <summary>A key nobody saved is a miss.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_key_nobody_saved_is_a_miss()
    {
        const string test = nameof(A_key_nobody_saved_is_a_miss);
        var cache = CreateCache();

        Expect.That(Expect.Ok(await cache.Find(["k1"], None), test).Count == 0, test, "an unsaved key was found");
    }

    /// <summary>Saved labels read back by key, and a key saved again takes the new label.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Saved_labels_read_back_and_a_second_save_replaces_the_label()
    {
        const string test = nameof(Saved_labels_read_back_and_a_second_save_replaces_the_label);
        var cache = CreateCache();

        _ = Expect.Ok(await cache.Save([KeyValuePair.Create("k1", "Tab groups"), KeyValuePair.Create("k2", "Split panes")], None), test);
        var first = Expect.Ok(await cache.Find(["k1", "k2", "k3"], None), test);
        _ = Expect.Ok(await cache.Save([KeyValuePair.Create("k1", "Tabs")], None), test);
        var second = Expect.Ok(await cache.Find(["k1", "k2"], None), test);

        Expect.That(first.Count == 2 && first["k1"] == "Tab groups" && first["k2"] == "Split panes", test, "the saved labels didn't read back");
        Expect.That(second["k1"] == "Tabs" && second["k2"] == "Split panes", test, "the second save didn't replace the label");
    }
}
