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
    public void Validate_accepts_a_known_kind_with_text_up_to_65536_scalars()
    {
        Expect.True(RequestRules.Validate(RequestKind.Rescope, "Split the work.").IsSuccess);
        Expect.True(RequestRules.Validate(RequestKind.Handback, new string('x', RequestRules.TextMax)).IsSuccess);
        Expect.True(RequestRules.Validate(RequestKind.Note, string.Concat(Enumerable.Repeat("\U0001F600", RequestRules.TextMax))).IsSuccess);   // twice as many UTF-16 units as scalars
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\n")]
    public void Validate_refuses_empty_text(string? text) => Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(RequestKind.Note, text).Error.Code);

    [Fact]
    public void Validate_refuses_over_65536_scalars()
    {
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(RequestKind.Note, new string('x', RequestRules.TextMax + 1)).Error.Code);
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate(RequestKind.Note, string.Concat(Enumerable.Repeat("\U0001F600", RequestRules.TextMax + 1))).Error.Code);
    }

    [Fact]
    public void Validate_refuses_an_unknown_kind() =>
        Expect.Equal(SindriErrors.RequestInvalid.Code, RequestRules.Validate((RequestKind)99, "text").Error.Code);

    [Fact]
    public void Cap_refuses_the_201st_in_a_repo()
    {
        Expect.True(RequestRules.Cap(openForRepo: 199, openTotal: 199).IsSuccess);
        var refused = RequestRules.Cap(openForRepo: 200, openTotal: 200).Error;
        Expect.Equal(SindriErrors.RequestsRateLimited.Code, refused.Code);
        Expect.True(refused.Message.Contains("200", StringComparison.Ordinal) && refused.Message.Contains("fix:", StringComparison.Ordinal));
    }

    [Fact]
    public void Cap_refuses_the_501st_overall()
    {
        Expect.True(RequestRules.Cap(openForRepo: 0, openTotal: 499).IsSuccess);
        Expect.Equal(SindriErrors.RequestsRateLimited.Code, RequestRules.Cap(openForRepo: 0, openTotal: 500).Error.Code);
    }

    [Fact]
    public void Every_p4_request_is_recorded_only() => Expect.True(RequestRules.RecordedOnly());
}
