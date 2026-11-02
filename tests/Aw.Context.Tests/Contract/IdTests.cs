using System.Text;
using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Tests.Contract;

public sealed class IdTests
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(ContractJsonContext.Default);

    private static IdsHolder Valid() => new(SourceId.From("repo-docs"), ExternalId.From("split-panes/docs/architecture.md#L1-L12"),
        SourceVersion.From("3b18e512dba79e4c8300dd08aeb37f8e728b8dad"), RecordKey.From("repo:split-panes/docs/architecture.md"),
        SourceCursor.From("a41b07d"), QueryId.From("q_01JC"), ContextRepoName.From("split-panes"));

    [Fact, Trait("Lane", "unit")]
    public void Every_id_round_trips_through_the_Kernel_profile()
    {
        var ids = Valid();

        var back = AwJson.Deserialize<IdsHolder>(AwJson.Serialize(ids, _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(ids, back.Value);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("source", "Repo-Docs")]
    [InlineData("source", null)]
    [InlineData("external", "")]
    [InlineData("external", "bad\u0001id")]
    [InlineData("version", "has space")]
    [InlineData("key", "no-scheme")]
    [InlineData("key", "Repo:x")]
    [InlineData("cursor", "")]
    [InlineData("query", "01JC")]
    [InlineData("repo", "-x")]
    [InlineData("repo", null)]
    public void A_malformed_or_null_id_is_AW_KERNEL_001(string property, string? value)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(AwJson.Serialize(Valid(), _json))!;   // why: the serialized holder is an object
        node[property] = value;

        var read = AwJson.Deserialize<IdsHolder>(Encoding.UTF8.GetBytes(node.ToJsonString()), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("split-panes", true)]
    [InlineData("work.app_2", true)]
    [InlineData("-x", false)]
    [InlineData("a/b", false)]
    [InlineData("", false)]
    public void A_repo_name_is_letters_digits_and_dot_underscore_dash(string name, bool valid) =>
        Assert.Equal(valid, ContextRepoName.TryFrom(name, out _));

    [Fact, Trait("Lane", "unit")]
    public void Ids_refuse_text_over_their_length()
    {
        Assert.False(SourceId.TryFrom(new string('a', 41), out _));
        Assert.False(ExternalId.TryFrom(new string('a', 1025), out _));
        Assert.False(SourceVersion.TryFrom(new string('a', 129), out _));
        Assert.True(SourceId.TryParse("onyx", provider: null, out _));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_record_key_an_old_onyx_sync_wrote_still_reads()
    {
        Assert.True(RecordKey.TryFrom("onyx:confluence/page-42", out var key));
        Assert.Equal("onyx:confluence/page-42", key.Value);
    }
}
