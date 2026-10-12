using Aw.Sindri.Infrastructure;

namespace Aw.Sindri.Tests.Profile;

public sealed class SettingsTests
{
    [Fact, Trait("Lane", "unit")]
    public void Settings_default_to_a_home_apart_from_the_typescript_sindri()
    {
        var settings = SettingsFromEnvironment.Read(name => name == "AW_STATE_DIR" ? "/state" : null);

        Assert.Equal("/state/sindri-app", settings.Home);
        Assert.Equal("/state/sindri-app/profile/profile.yaml", settings.ProfilePath);
        Assert.Equal("/x/p.yaml", SettingsFromEnvironment.Read(n => n switch { "AW_SINDRI_HOME" => "/x", "AW_SINDRI_PROFILE" => "/x/p.yaml", _ => null }).ProfilePath);
    }
}
