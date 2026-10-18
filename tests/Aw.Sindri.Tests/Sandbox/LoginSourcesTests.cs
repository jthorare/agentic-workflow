using Aw.Kernel.Testing;
using Aw.Sindri.Core.Sandbox;

namespace Aw.Sindri.Tests.Sandbox;

/// <summary>A Step may ask for a stricter model login source and never a looser one (sandbox spec SB18, owner note 4).</summary>
[Lane(TestLane.Unit)]
public sealed class LoginSourcesTests
{
    [Theory]
    [InlineData(ModelLoginSource.OwnerCopy, ModelLoginSource.SeparateLogin, ModelLoginSource.SeparateLogin)]
    [InlineData(ModelLoginSource.OwnerCopy, ModelLoginSource.OwnerCopy, ModelLoginSource.OwnerCopy)]
    [InlineData(ModelLoginSource.SeparateLogin, ModelLoginSource.SeparateLogin, ModelLoginSource.SeparateLogin)]
    public void A_stricter_or_equal_ask_wins(ModelLoginSource configured, ModelLoginSource ask, ModelLoginSource expected) =>
        Assert.Equal(expected, LoginSources.Stricter(configured, ask));

    [Fact]
    public void A_looser_ask_cannot_loosen_the_profile() =>
        Assert.Equal(ModelLoginSource.SeparateLogin, LoginSources.Stricter(ModelLoginSource.SeparateLogin, ModelLoginSource.OwnerCopy));

    [Theory]
    [InlineData(ModelLoginSource.OwnerCopy)]
    [InlineData(ModelLoginSource.SeparateLogin)]
    public void No_ask_keeps_the_configured_source(ModelLoginSource configured) =>
        Assert.Equal(configured, LoginSources.Stricter(configured, ask: null));
}
