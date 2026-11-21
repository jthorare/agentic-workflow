using Aw.Forge.Core;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Host.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class PullErrorsTests
{
    [Fact]
    public void A_repo_refusal_maps_to_its_own_code_and_keeps_the_forge_code_in_the_message()
    {
        var mapped = PullErrors.From(ForgeErrors.Refused.With("Resource not accessible"));

        Assert.Equal(SindriErrors.ForgeRepoRefused.Code, mapped.Code);
        Assert.StartsWith("AW-FORGE-009:", mapped.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rejected_credential_still_maps_to_SND_FORGE_002()
    {
        var mapped = PullErrors.From(ForgeErrors.Unauthorized.With("Bad credentials"));

        Assert.Equal(SindriErrors.ForgeRefused.Code, mapped.Code);
        Assert.StartsWith("AW-FORGE-003:", mapped.Message, StringComparison.Ordinal);
    }
}
