using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ReviewFix;

/// <summary>What the fix-task files do, for the files on disk and the fake alike (PR review page spec §8.2): Add lists an item once, Remove finds it under any head, an empty head is gone.</summary>
public abstract class FixTaskFilesContract
{
    protected static ItemId Id(char c) => ItemId.From("ITM-" + new string(c, 32));

    private const string Head = "acme/api:feat/x";

    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Gets the files under test.</summary>
    protected abstract IFixTaskFiles Files { get; }

    /// <summary>Gets the open items of <paramref name="head"/> by item id, empty when it has no file.</summary>
    /// <param name="head">The head string.</param>
    /// <returns>Item to PR number.</returns>
    protected abstract IReadOnlyDictionary<string, int> Open(string head);

    [Fact]
    public async Task Add_lists_an_item_once_ordered_by_id()
    {
        _ = (await Files.Add(Head, Id('b'), 2071, Token)).Value;
        _ = (await Files.Add(Head, Id('a'), 2071, Token)).Value;
        Assert.False((await Files.Add(Head, Id('b'), 2071, Token)).Value);

        Assert.Equal([Id('a').Value, Id('b').Value], [.. Open(Head).Keys.Order(StringComparer.Ordinal)]);
        Expect.Equal(2, Open(Head).Count);
    }

    [Fact]
    public async Task Remove_takes_the_item_off_its_head_and_the_last_one_ends_the_file()
    {
        Assert.True((await Files.Add(Head, Id('a'), 2071, Token)).Value);
        _ = (await Files.Add(Head, Id('b'), 2071, Token)).Value;
        _ = (await Files.Add("acme/web:main", Id('c'), 880, Token)).Value;

        Assert.True((await Files.Remove(Id('a'), Token)).Value);
        Assert.Equal([Id('b').Value], [.. Open(Head).Keys]);

        _ = (await Files.Remove(Id('b'), Token)).Value;
        Expect.Equal(0, Open(Head).Count);
        Assert.Equal([Id('c').Value], [.. Open("acme/web:main").Keys]);
    }

    [Fact]
    public async Task Remove_of_an_item_no_file_lists_is_fine()
        => Assert.False((await Files.Remove(Id('z'), Token)).Value);
}
