using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class EventIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void Round_trips_through_the_Kernel_profile()
    {
        var id = EventId.From(new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1));

        var back = AwJson.Deserialize<EventIdHolder>(AwJson.Serialize(new EventIdHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"not-a-guid\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<EventIdHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(EventId.TryParse("00000000-0000-0000-0000-000000000001", provider: null, out _));
        Assert.False(EventId.TryParse("not-a-guid", provider: null, out _));
    }
}
