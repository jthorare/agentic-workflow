using Aw.Meta.Core.Ports;
using Xunit;

namespace Aw.Meta.Testing;

/// <summary>The IKeyValueStore contract: the same tests run against the in-memory fake and every real adapter.</summary>
public abstract class KeyValueStoreContract
{
    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh, empty store.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IKeyValueStore> Create();

    /// <summary>A key never set reads as null; a set value reads back; a later set replaces it.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Values_read_back_and_a_later_set_replaces_them()
    {
        var store = await Create();

        var before = await store.Get("k", _none);
        _ = await store.Set("k", "one", _none);
        var first = await store.Get("k", _none);
        _ = await store.Set("k", "two", _none);
        var second = await store.Get("k", _none);
        var other = await store.Get("other", _none);

        Expect.That(before.IsSuccess && before.Value is null, "an unset key is null");
        Expect.That(first.Value == "one" && second.Value == "two", "the value reads back and is replaced");
        Expect.That(other.Value is null, "keys are independent");
    }

    /// <summary>SetIf writes when the stored value is the expected one.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SetIf_writes_when_the_value_matches()
    {
        var store = await Create();
        _ = await store.Set("k", "one", _none);

        var wrote = await store.SetIf("k", "one", "two", _none);
        var after = await store.Get("k", _none);

        Expect.That(wrote.IsSuccess && wrote.Value, "a matching expected value writes");
        Expect.That(after.Value == "two", "the new value is stored");
    }

    /// <summary>SetIf refuses and leaves the value when the stored value differs, or the key is absent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SetIf_refuses_and_keeps_the_value_when_it_does_not_match()
    {
        var store = await Create();
        _ = await store.Set("k", "one", _none);

        var stale = await store.SetIf("k", "zero", "two", _none);
        var absent = await store.SetIf("missing", "zero", "two", _none);
        var after = await store.Get("k", _none);
        var missing = await store.Get("missing", _none);

        Expect.That(stale.IsSuccess && !stale.Value, "a stale expected value does not write");
        Expect.That(after.Value == "one", "the stored value is untouched");
        Expect.That(absent.IsSuccess && !absent.Value && missing.Value is null, "a non-null expected value never creates a key");
    }

    /// <summary>SetIf with a null expected value writes only when the key is absent.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SetIf_with_null_expected_writes_only_when_the_key_is_absent()
    {
        var store = await Create();

        var first = await store.SetIf("k", expected: null, "one", _none);
        var second = await store.SetIf("k", expected: null, "two", _none);
        var after = await store.Get("k", _none);

        Expect.That(first.IsSuccess && first.Value, "an absent key is written");
        Expect.That(second.IsSuccess && !second.Value, "a present key is not");
        Expect.That(after.Value == "one", "the first value stays");
    }
}
