using System.Text.RegularExpressions;

namespace Aw.Sindri.Toolkit.Tests;

public sealed class ToolkitErrorsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Every_code_is_well_formed_and_unique()
    {
        Assert.All(ToolkitErrors.All, e => Assert.Matches(new Regex("^AW-TOOLKIT-[0-9]{3}$", RegexOptions.None, TimeSpan.FromSeconds(1)), e.Code));
        Assert.Equal(ToolkitErrors.All.Count, ToolkitErrors.All.Select(e => e.Code).Distinct(StringComparer.Ordinal).Count());
    }
}
