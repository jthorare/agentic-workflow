using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Home;

/// <summary>The Home row record (home spec §3.1): the wire shape and the id in a path.</summary>
public sealed class HomeRowTests
{
    [Fact, Trait("Lane", "unit")]
    public void HomeRow_roundtrips_json()
    {
        var row = HomeRowSamples.Row(position: 3);

        var bytes = AwJson.Serialize(row, SliceHarness.Json);
        var read = AwJson.Deserialize<HomeRow>(bytes, SliceHarness.Json);

        Assert.Equal(Encoding.UTF8.GetString(bytes), Encoding.UTF8.GetString(AwJson.Serialize(read.Value, SliceHarness.Json)));   // lists compare by reference: compare what they write
        Assert.Equal((row.Id, row.Version, row.Kind, row.Group, row.State, row.Rank), (read.Value.Id, read.Value.Version, read.Value.Kind, read.Value.Group, read.Value.State, read.Value.Rank));
    }

    [Fact, Trait("Lane", "unit")]
    public void HomeRow_writes_the_spec_names()
    {
        var json = Encoding.UTF8.GetString(AwJson.Serialize(HomeRowSamples.Row(), SliceHarness.Json));

        Assert.Contains("\"kind\":\"review-requested\"", json, StringComparison.Ordinal);
        Assert.Contains("\"group\":\"needs-you\"", json, StringComparison.Ordinal);
        Assert.Contains("\"state\":\"open\"", json, StringComparison.Ordinal);
        Assert.Contains("\"rank\":{\"blocks\":2,\"weight\":5,\"sinceRank\":1,\"position\":0}", json, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Id_percent_encodes_in_path()
    {
        var id = HomeRowId.From("gh:pr:jthorare/agentic-workflow#3120");

        Assert.Equal("gh%3Apr%3Ajthorare%2Fagentic-workflow%233120", id.PathSegment());
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("no-colon")]
    [InlineData("GH:pr:x")]
    [InlineData("gh:pr: spaced")]
    public void An_id_without_a_source_or_with_a_space_is_refused(string value) => Assert.False(HomeRowId.TryFrom(value, out _));
}
