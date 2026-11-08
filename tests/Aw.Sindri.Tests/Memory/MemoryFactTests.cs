using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Memory;
using Aw.Sindri.Testing.Memory;

namespace Aw.Sindri.Tests.Memory;

/// <summary>The fact rules in Core: text length, expiry.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryFactTests
{
    [Fact]
    public void Fact_text_over_2000_characters_is_refused()
    {
        var accepted = MemoryFact.CheckText(new string('x', MemoryFact.MaxTextLength));
        var refused = MemoryFact.CheckText(new string('x', MemoryFact.MaxTextLength + 1));

        Assert.True(accepted.IsSuccess);
        Assert.True(refused.IsError);
        Assert.Equal(SindriErrors.MemoryFactInvalid.Code, refused.Error.Code);
    }

    [Fact]
    public void An_empty_fact_text_is_refused()
        => Assert.Equal(SindriErrors.MemoryFactInvalid.Code, MemoryFact.CheckText("  ").Error.Code);

    [Fact]
    public void A_fact_expires_at_its_expiry_and_never_without_one()
    {
        var at = KnownInstants.Epoch.AddHours(1);

        Assert.True(MemorySamples.Fact(expiresAt: at).IsExpired(at));
        Assert.False(MemorySamples.Fact(expiresAt: at).IsExpired(at.AddMinutes(-1)));
        Assert.False(MemorySamples.Fact().IsExpired(at.AddYears(10)));
    }
}
