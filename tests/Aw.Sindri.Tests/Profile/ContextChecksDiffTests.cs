using System.Text.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>'context.checks.*' (map structure checks' limits): a changed limit shows in the profile diff by its key path.</summary>
public sealed class ContextChecksDiffTests
{
    private static SindriProfile With(string context) => new SindriProfile(1, "thor") with { Context = JsonDocument.Parse(context).RootElement.Clone() };

    [Fact, Trait("Lane", "unit")]
    public void A_changed_checks_limit_is_named_in_the_diff()
    {
        var diff = ProfileDiff.Between(With("""{"checks":{"mode":"refuse"}}"""), With("""{"checks":{"mode":"refuse","isolated":{"refusePercent":70}}}"""));

        Assert.Equal(["context.checks.isolated.refusePercent"], diff);
    }
}
