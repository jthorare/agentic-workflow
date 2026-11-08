using Aw.Kernel.Testing;
using Aw.Sindri.Core.Todos.Note;
using static Aw.Sindri.Tests.Todos.Note.NoteSamples;

namespace Aw.Sindri.Tests.Todos.Note;

/// <summary>Per-pass id minting (todo spec §4.1): a line gets its id once, and every re-write of the same pass reuses it.</summary>
[Lane(TestLane.Unit)]
public sealed class PassIdMinterTests
{
    [Fact]
    public void The_same_key_gets_the_same_id_without_asking_the_source_again()
    {
        var calls = 0;
        var minter = new PassIdMinter(() => Id(++calls));

        var first = minter.Mint("0:buy milk");
        var again = minter.Mint("0:buy milk");

        Assert.Equal(first, again);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Different_keys_get_the_sources_ids_in_order()
    {
        var calls = 0;
        var minter = new PassIdMinter(() => Id(++calls));

        Assert.Equal(Id(1), minter.Mint("0:buy milk"));
        Assert.Equal(Id(2), minter.Mint("1:buy milk"));
        Assert.Equal(Id(3), minter.Mint("2:call"));
    }

    [Fact]
    public void A_second_merge_with_the_same_minter_adopts_the_same_lines_under_the_same_ids()
    {
        var options = Options();
        var note = Doc("- [ ] buy milk");

        var first = NoteMerge.Merge(Base(), note, [], options);
        var again = NoteMerge.Merge(Base(), note, [], options);

        Same(first.Intents.Select(Show), again.Intents.Select(Show));
        Same(["adopt 9000 \"buy milk\" line 1"], first.Intents.Select(Show));
    }
}
