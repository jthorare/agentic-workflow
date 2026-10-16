using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Requests;

/// <summary>How a request kind reads from a body (API spec §3.2 'FileRequest', §9): a name it doesn't know is SND-REQ-001 in the rules, not a binding failure; a number is still a binding failure.</summary>
[Lane(TestLane.Unit)]
public sealed class RequestKindJsonTests
{
    [Theory]
    [InlineData("rescope", RequestKind.Rescope)]
    [InlineData("add-task", RequestKind.AddTask)]
    [InlineData("note", RequestKind.Note)]
    [InlineData("handback", RequestKind.Handback)]
    public void Each_kind_round_trips_by_name(string name, RequestKind kind)
    {
        Expect.Equal(kind, JsonSerializer.Deserialize<RequestKind>($"\"{name}\"", SliceHarness.Json));
        Expect.Equal($"\"{name}\"", JsonSerializer.Serialize(kind, SliceHarness.Json));
    }

    [Fact]
    public void An_unknown_name_reads_as_an_undefined_kind_the_rules_refuse_with_SND_REQ_001()
    {
        var kind = JsonSerializer.Deserialize<RequestKind>("\"bogus\"", SliceHarness.Json);

        Expect.True(!Enum.IsDefined(kind));
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(kind, "text").Error.Code);
    }

    [Fact]
    public void A_number_is_still_a_binding_failure() =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RequestKind>("1", SliceHarness.Json));
}
