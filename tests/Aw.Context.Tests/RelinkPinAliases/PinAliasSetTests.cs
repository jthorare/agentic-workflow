using Aw.Context.Core.Engine;
using Aw.Context.Core.Linking;

namespace Aw.Context.Tests.RelinkPinAliases;

/// <summary>The hash the Sindri tick compares: the pins in id order, each pin's aliases in ordinal order; no pins is still a hash.</summary>
[Trait("Lane", "unit")]
public sealed class PinAliasSetTests
{
    private static PinAliases Pin(string id, params string[] aliases) => new(ConceptId.From(id), aliases);

    [Fact]
    public void The_hash_ignores_the_order_of_pins_and_of_aliases()
    {
        var one = PinAliasSet.Hash([Pin("billing", "payments", "invoices"), Pin("notify", "alerts")]);
        var other = PinAliasSet.Hash([Pin("notify", "alerts"), Pin("billing", "invoices", "payments")]);

        Assert.Equal(one, other);
    }

    [Fact]
    public void The_hash_changes_with_an_alias_a_pin_or_the_pin_an_alias_belongs_to()
    {
        var hashes = new[]
        {
            PinAliasSet.Hash([Pin("billing", "payments")]),
            PinAliasSet.Hash([Pin("billing", "payments", "invoices")]),
            PinAliasSet.Hash([Pin("billing", "payments"), Pin("notify")]),
            PinAliasSet.Hash([Pin("notify", "payments")]),
        };

        Assert.Equal(hashes.Length, hashes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void No_pins_hash_the_empty_text() =>
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", PinAliasSet.Hash([]));
}
