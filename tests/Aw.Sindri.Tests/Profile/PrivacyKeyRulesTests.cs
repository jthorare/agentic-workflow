using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The 'privacy' keys (issue 3787): no blank term, every deny pattern compiles, and the error never repeats the pattern.</summary>
[Lane(TestLane.Unit)]
public sealed class PrivacyKeyRulesTests
{
    [Fact]
    public void No_privacy_section_passes() => Assert.Null(PrivacyKeyRules.Check(privacy: null));

    [Fact]
    public void Terms_and_compiling_patterns_pass() =>
        Assert.Null(PrivacyKeyRules.Check(new PrivacySettings(["AcmeCare"], [@"ABC-\d+"])));

    [Fact]
    public void A_blank_term_is_refused_with_its_key_path()
    {
        var error = PrivacyKeyRules.Check(new PrivacySettings(["AcmeCare", "  "], DenyPatterns: null));

        Assert.NotNull(error);
        Assert.Equal(SindriErrors.ProfileInvalid.Code, error.Code);
        Assert.Contains("privacy.denyTerms[1]", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pattern_that_does_not_compile_is_refused_without_repeating_it()
    {
        var error = PrivacyKeyRules.Check(new PrivacySettings(DenyTerms: null, [@"ABC-\d+", "(secretproject"]));

        Assert.NotNull(error);
        Assert.Contains("privacy.denyPatterns[1]", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secretproject", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_profile_with_a_bad_pattern_fails_the_profile_check()
    {
        var profile = new SindriProfile(SindriProfile.CurrentSchemaVersion, "owner", Privacy: new PrivacySettings(DenyTerms: null, ["("]));

        Assert.Equal(SindriErrors.ProfileInvalid.Code, ProfileRules.Check(profile).Error.Code);
    }
}
