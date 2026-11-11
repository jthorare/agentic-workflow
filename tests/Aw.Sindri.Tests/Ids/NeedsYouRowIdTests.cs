using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class NeedsYouRowIdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void Round_trips_through_the_Kernel_profile()
    {
        var id = NeedsYouRowId.From("failure:service:heartbeat");

        var back = AwJson.Deserialize<NeedsYouRowIdHolder>(AwJson.Serialize(new NeedsYouRowIdHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"decision:DEC-XYZ\"}")]
    [InlineData("{\"id\":\"profile:other\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<NeedsYouRowIdHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Parses_valid_text_and_refuses_invalid_text()
    {
        Assert.True(NeedsYouRowId.TryParse("failure:service:heartbeat", provider: null, out _));
        Assert.False(NeedsYouRowId.TryParse("decision:DEC-XYZ", provider: null, out _));
    }

    // setup-once spec §7.3: the profile and the map have rows of their own kinds, so 'setup:' takes only the three steps that have none
    [Theory, Trait("Lane", "unit")]
    [InlineData("setup:repo", true)]
    [InlineData("setup:recipes", true)]
    [InlineData("setup:mcp", true)]
    [InlineData("setup:profile", false)]
    [InlineData("setup:map", false)]
    [InlineData("setup:unknown", false)]
    [InlineData("setup:", false)]
    [InlineData("setup:repo2", false)]
    [InlineData("setup:repo\n", false)]
    public void A_setup_row_id_names_repo_recipes_or_mcp_only(string text, bool valid) =>
        Assert.Equal(valid, NeedsYouRowId.TryParse(text, provider: null, out _));

    // #2543: an 'ask_owner' node in a 'for_each' body has one row per item, so only 'question:' takes an item part, counted from 1
    [Theory, Trait("Lane", "unit")]
    [InlineData("question:nightly@3:12:ask", true)]
    [InlineData("question:nightly@3:12:ask:1", true)]
    [InlineData("question:nightly@3:12:ask:42", true)]
    [InlineData("question:nightly@3:12:ask:0", false)]
    [InlineData("question:nightly@3:12:ask:01", false)]
    [InlineData("question:nightly@3:12:ask:", false)]
    [InlineData("question:nightly@3:12:ask:x", false)]
    [InlineData("workflow-notify:nightly@3:12:note:1", false)]
    public void A_question_row_id_may_name_its_loop_item(string text, bool valid) =>
        Assert.Equal(valid, NeedsYouRowId.TryParse(text, provider: null, out _));
}
