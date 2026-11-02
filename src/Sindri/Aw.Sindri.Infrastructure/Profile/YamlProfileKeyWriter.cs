using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary><see cref="IProfileKeyWriter"/> over the live profile file: <see cref="ProfileKeyText"/> edits the text and the file is replaced atomically with its mode kept.</summary>
/// <param name="settings">Where the profile is.</param>
internal sealed class YamlProfileKeyWriter(SindriSettings settings) : IProfileKeyWriter
{
    public Result<ProfileKeyEdit> Plan(string keyPath, string value)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        ArgumentNullException.ThrowIfNull(value);
        var path = settings.ProfilePath;
        string before;
        try
        {
            before = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message} (fix: run 'sindri profile init' when there is no profile)");
        }

        var edited = ProfileKeyText.Edit(before, keyPath, value);
        return edited.IsError ? edited.Error : new ProfileKeyEdit(path, edited.Value, !string.Equals(before, edited.Value, StringComparison.Ordinal));
    }

    public Result<string> Save(ProfileKeyEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (!edit.Changed)
        {
            return edit.Path;
        }

        try
        {
            YamlProfileLines.WriteKeepingMode(edit.Path, edit.Text);
            return edit.Path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileMissing.With($"{edit.Path} can't be written: {e.Message}");
        }
    }
}
