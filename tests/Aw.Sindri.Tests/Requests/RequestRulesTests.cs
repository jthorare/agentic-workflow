using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Requests;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Requests;

/// <summary>The pure rules of filing a request (API spec §3.2, §4.2, §9; P4 plan G2, G4, G6).</summary>
[Lane(TestLane.Unit)]
public sealed class RequestRulesTests
{
    [Fact]
    public void Validate_accepts_a_known_kind_with_text_up_to_2000_scalars()
    {
        Expect.True(RequestRules.Validate(RequestKind.Rescope, "Split the work.").IsSuccess);
        Expect.True(RequestRules.Validate(RequestKind.Handback, new string('x', 2000)).IsSuccess);
        Expect.True(RequestRules.Validate(RequestKind.Note, string.Concat(Enumerable.Repeat("\U0001F600", 2000))).IsSuccess);   // 4,000 UTF-16 units, 2,000 scalars
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\n")]
    public void Validate_refuses_empty_text(string? text) => Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(RequestKind.Note, text).Error.Code);

    [Fact]
    public void Validate_refuses_over_2000_scalars()
    {
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(RequestKind.Note, new string('x', 2001)).Error.Code);
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(RequestKind.Note, string.Concat(Enumerable.Repeat("\U0001F600", 2001))).Error.Code);
    }

    [Fact]
    public void Validate_refuses_an_unknown_kind() =>
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate((RequestKind)99, "text").Error.Code);

    [Fact]
    public void Cap_refuses_the_21st_in_a_repo()
    {
        Expect.True(RequestRules.Cap(openForRepo: 19, openTotal: 19).IsSuccess);
        var refused = RequestRules.Cap(openForRepo: 20, openTotal: 20).Error;
        Expect.Equal(SindriErrors.RequestsRateLimited.Code, refused.Code);
        Expect.True(refused.Message.Contains("20", StringComparison.Ordinal) && refused.Message.Contains("fix:", StringComparison.Ordinal));
    }

    [Fact]
    public void Cap_refuses_the_51st_overall()
    {
        Expect.True(RequestRules.Cap(openForRepo: 0, openTotal: 49).IsSuccess);
        Expect.Equal(SindriErrors.RequestsRateLimited.Code, RequestRules.Cap(openForRepo: 0, openTotal: 50).Error.Code);
    }

    [Fact]
    public void Every_p4_request_is_recorded_only() => Expect.True(RequestRules.RecordedOnly());
}
