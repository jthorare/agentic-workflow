using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class RecordKeyTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void Round_trips_through_the_Kernel_profile()
    {
        var id = RecordKey.From("linear:issue/3f2a-91bc");

        var back = AwJson.Deserialize<RecordKeyHolder>(AwJson.Serialize(new RecordKeyHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"Linear:issue/3f2a\"}")]
    [InlineData("{\"id\":\"linear-issue-3f2a\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<RecordKeyHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(RecordKey.TryParse("linear:issue/3f2a-91bc", provider: null, out _));
        Assert.False(RecordKey.TryParse("Linear:issue/3f2a", provider: null, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_workflow_run_id_with_at_and_colon_is_a_valid_run_record()
    {
        Assert.True(RecordKey.TryParse("sindri:run/nightly@1:1", provider: null, out var key));
        Assert.Equal("sindri:run/nightly@1:1", key.Value);
    }
}
