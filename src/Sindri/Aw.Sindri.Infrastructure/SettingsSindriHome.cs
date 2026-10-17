using Aw.Sindri.Core.Control;

namespace Aw.Sindri.Infrastructure;

/// <summary>ISindriHome from the settings read at start-up.</summary>
internal sealed class SettingsSindriHome(SindriSettings settings) : ISindriHome
{
    /// <inheritdoc />
    public string Path => settings.Home;
}
