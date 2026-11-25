using Aw.Meta.Infrastructure;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Health;

/// <summary>The folder the OOM line names comes from the environment the way install-meta.sh picks it: $XDG_CONFIG_HOME, else &lt;home&gt;/.config.</summary>
public sealed class OomDropInSettingsTests : IDisposable
{
    private readonly TempDirectory _state = new();

    public void Dispose() => _state.Dispose();

    [Fact, Trait("Lane", "unit")]
    public void A_set_XDG_CONFIG_HOME_is_the_user_config_folder()
    {
        var settings = SettingsFromEnvironment.Read(Environment(("XDG_CONFIG_HOME", "/x/cfg")));

        Assert.Equal("/x/cfg", settings.Paths.UserConfig);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_XDG_CONFIG_HOME_the_user_config_folder_is_home_dot_config()
    {
        var settings = SettingsFromEnvironment.Read(Environment(("XDG_CONFIG_HOME", "")));

        Assert.Equal(Path.Combine(settings.Paths.Home, ".config"), settings.Paths.UserConfig);
    }

    private Func<string, string?> Environment(params (string Name, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Name, v => (string?)v.Value, StringComparer.Ordinal);
        map["AW_STATE_DIR"] = _state.Path;
        return name => map.GetValueOrDefault(name);
    }
}
