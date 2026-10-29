using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Tests.Ids;

public sealed class ProjectIdTests
{
    private const string Valid = "prj_01J9Z3K5M7Q2R4T6V8WXYZ0123";
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(IdsJsonContext.Default);

    [Fact, Trait("Lane", "unit")]
    public void ProjectId_round_trips_json()
    {
        var id = ProjectId.From(Valid);

        var back = AwJson.Deserialize<ProjectIdHolder>(AwJson.Serialize(new ProjectIdHolder(id), _json), _json);

        Assert.True(back.IsSuccess);
        Assert.Equal(id, back.Value.Id);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("{\"id\":\"prj_01j9z3k5m7q2r4t6v8wxyz0123\"}")]
    [InlineData("{\"id\":\"prj_01J9Z3K5M7Q2R4T6V8WXYZ012\"}")]
    [InlineData("{\"id\":\"prj_01J9Z3K5M7Q2R4T6V8WXYZ01234\"}")]
    [InlineData("{\"id\":\"prj_01J9Z3K5M7Q2R4T6V8WXYZ012I\"}")]
    [InlineData("{\"id\":\"prj_81J9Z3K5M7Q2R4T6V8WXYZ0123\"}")]
    [InlineData("{\"id\":\"ITM-0192f0c1a2b37c4d8e9f00112233aabb\"}")]
    [InlineData("{}")]
    [InlineData("{\"id\":null}")]
    public void Invalid_missing_or_null_is_AW_KERNEL_001(string json)
    {
        var read = AwJson.Deserialize<ProjectIdHolder>(Encoding.UTF8.GetBytes(json), _json);

        Assert.True(read.IsError);
        Assert.Equal(KernelErrors.Invalid.Code, read.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_new_id_is_prj_and_26_ulid_digits_and_two_ids_differ()
    {
        var ids = new SequentialIdGenerator();

        var first = ProjectId.New(ids);
        var second = ProjectId.New(ids);

        Assert.True(ProjectId.TryParse(first.Value, provider: null, out _));
        Assert.Equal(4 + 26, first.Value.Length);
        Assert.NotEqual(first, second);
    }

    [Fact, Trait("Lane", "unit")]
    public void Ids_sort_in_the_order_the_port_made_them_when_the_guids_sort()
    {
        var early = ProjectId.New(new FixedGuid(new Guid(0x0192f0c1, 0x0000, 0x7000, 0x80, 0, 0, 0, 0, 0, 0, 0)));
        var late = ProjectId.New(new FixedGuid(new Guid(0x0192f0c2, 0x0000, 0x7000, 0x80, 0, 0, 0, 0, 0, 0, 0)));

        Assert.True(string.CompareOrdinal(early.Value, late.Value) < 0);
    }

    private sealed class FixedGuid(Guid value) : IIdGenerator
    {
        public Guid NewId() => value;
    }
}
