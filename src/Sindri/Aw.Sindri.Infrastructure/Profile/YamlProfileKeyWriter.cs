using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary><see cref="IProfileKeyWriter"/> over the live profile file: <see cref="ProfileKeyText"/> edits the text and the file is replaced atomically with its mode kept.</summary>
/// <param name="settings">Where the profile is.</param>
/// <param name="cache">Told right after a write, so the next read parses the file.</param>
internal sealed class YamlProfileKeyWriter(SindriSettings settings, Core.Profile.IProfileCache cache) : IProfileKeyWriter
{
    public Result<ProfileKeyEdit> Plan(string keyPath, string value) => Plan(keyPath, value, asString: false);

    public Result<ProfileKeyEdit> PlanText(string keyPath, string value) => Plan(keyPath, value, asString: true);

    public Result<ProfileKeyEdit> PlanTexts(IReadOnlyList<(string KeyPath, string Value)> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Edit(text =>
        {
            foreach (var (keyPath, value) in values)
            {
                var edited = ProfileKeyText.Edit(text, keyPath, value, asString: true);
                if (edited.IsError)
                {
                    return edited;
                }

                text = edited.Value;
            }

            return text;
        });
    }

    public Result<ProfileKeyEdit> PlanRemove(string keyPath)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        return Edit(text => ProfileKeyText.Remove(text, keyPath));
    }

    private Result<ProfileKeyEdit> Plan(string keyPath, string value, bool asString)
    {
        ArgumentNullException.ThrowIfNull(keyPath);
        ArgumentNullException.ThrowIfNull(value);
        return Edit(text => ProfileKeyText.Edit(text, keyPath, value, asString));
    }

    // reads the profile file once, applies the text edit and says whether the file would change
    private Result<ProfileKeyEdit> Edit(Func<string, Result<string>> edit)
    {
        var path = settings.ProfilePath;
        string before;
        try
        {
            before = File.ReadAllText(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return SindriErrors.ProfileMissing.With($"{path} can't be read: {e.Message} (fix: run 'sindri profile init' when there is no profile)");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileUnreadable.With($"{path} can't be read: {e.Message} (fix: check the file's permissions, e.g. chmod 600 {path})");
        }

        var edited = edit(before);
        return edited.IsError ? edited.Error : new ProfileKeyEdit(path, edited.Value, !string.Equals(before, edited.Value, StringComparison.Ordinal), before);
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
            // why: a hand edit or 'profile set' that landed after the plan read must not be overwritten
            if (edit.Before is not null && !string.Equals(File.ReadAllText(edit.Path), edit.Before, StringComparison.Ordinal))
            {
                return SindriErrors.ProfileHashMismatch.With($"{edit.Path} changed since it was read (fix: reload and try again)");
            }

            YamlProfileLines.WriteKeepingMode(edit.Path, edit.Text);
            cache.Invalidate();
            return edit.Path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ProfileUnwritable.With($"{edit.Path} can't be written: {e.Message} (fix: check the file's permissions, e.g. chmod 600 {edit.Path})");
        }
    }
}
