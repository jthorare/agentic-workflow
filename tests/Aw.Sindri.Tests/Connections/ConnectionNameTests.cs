using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class ConnectionNameTests
{
    [Fact]
    public void A_service_and_a_label_in_lowercase_parse_and_print_back()
    {
        var parsed = ConnectionName.Parse("linear:work");

        Assert.True(parsed.IsSuccess);
        Assert.Equal("linear", parsed.Value.Service);
        Assert.Equal("work", parsed.Value.Label);
        Assert.Equal("linear:work", parsed.Value.ToString());
    }

    [Theory]
    [InlineData("github:my-org-2")]
    [InlineData("a:b")]
    [InlineData("0x:9")]
    public void Digits_and_inner_dashes_are_allowed(string text) => Assert.True(ConnectionName.Parse(text).IsSuccess);

    [Theory]
    [InlineData("Linear:work")]
    [InlineData("linear:Work")]
    [InlineData("linear")]
    [InlineData("linear:")]
    [InlineData(":work")]
    [InlineData("linear:work:extra")]
    [InlineData("-linear:work")]
    [InlineData("linear:-work")]
    [InlineData("lin ear:work")]
    [InlineData("")]
    public void A_bad_name_is_SND_CONN_008(string text)
    {
        var parsed = ConnectionName.Parse(text);

        Assert.True(parsed.IsError);
        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, parsed.Error.Code);
    }

    [Fact]
    public void A_part_of_33_characters_is_too_long_and_32_is_not()
    {
        var thirtyTwo = new string('a', 32);
        var thirtyThree = new string('a', 33);

        Assert.True(ConnectionName.Parse($"{thirtyTwo}:{thirtyTwo}").IsSuccess);
        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, ConnectionName.Parse($"{thirtyThree}:work").Error.Code);
        Assert.Equal(SindriErrors.ConnectionNameInvalid.Code, ConnectionName.Parse($"linear:{thirtyThree}").Error.Code);
    }
}
