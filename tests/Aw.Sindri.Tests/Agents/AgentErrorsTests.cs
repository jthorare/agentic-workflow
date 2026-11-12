using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Tests.Agents;

/// <summary>The SND-AGENT codes (Agents page spec §9): 001 to 011 are declared once, in the catalogue, with the kinds the spec's HTTP column implies.</summary>
public sealed class AgentErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_codes_001_to_011_are_declared_once_each_in_the_catalogue()
    {
        var codes = SindriErrors.All.Where(static e => e.Code.StartsWith("SND-AGENT-", StringComparison.Ordinal)).Select(static e => e.Code).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(Enumerable.Range(1, 11).Select(static n => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"SND-AGENT-{n:000}")), codes, StringComparer.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("SND-AGENT-001", ErrorKind.NotFound)]
    [InlineData("SND-AGENT-002", ErrorKind.Conflict)]
    [InlineData("SND-AGENT-003", ErrorKind.Invalid)]
    [InlineData("SND-AGENT-004", ErrorKind.Conflict)]
    [InlineData("SND-AGENT-005", ErrorKind.Invalid)]
    [InlineData("SND-AGENT-006", ErrorKind.Unavailable)]
    [InlineData("SND-AGENT-007", ErrorKind.Invalid)]
    [InlineData("SND-AGENT-008", ErrorKind.Unavailable)]
    [InlineData("SND-AGENT-009", ErrorKind.Conflict)]
    [InlineData("SND-AGENT-010", ErrorKind.Invalid)]
    [InlineData("SND-AGENT-011", ErrorKind.Timeout)]
    public void Each_code_has_the_kind_that_gives_its_http_status(string code, ErrorKind kind)
        => Assert.Equal(kind, SindriErrors.All.Single(e => e.Code == code).Kind);

    [Fact, Trait("Lane", "unit")]
    public void The_recoverable_codes_carry_a_fix_line()
    {
        foreach (var code in (string[])["SND-AGENT-001", "SND-AGENT-002", "SND-AGENT-004", "SND-AGENT-005", "SND-AGENT-006", "SND-AGENT-008", "SND-AGENT-009", "SND-AGENT-011"])
        {
            Assert.Contains("(fix:", SindriErrors.All.Single(e => e.Code == code).Title, StringComparison.Ordinal);
        }
    }
}
