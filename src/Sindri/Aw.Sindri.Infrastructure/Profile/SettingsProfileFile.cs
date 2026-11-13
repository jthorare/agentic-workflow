using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>Whether the live profile file (<see cref="SindriSettings.ProfilePath"/>) is on disk.</summary>
/// <param name="settings">Where the file is.</param>
internal sealed class SettingsProfileFile(SindriSettings settings) : IProfileFile
{
    public bool Exists() => File.Exists(settings.ProfilePath);
}
