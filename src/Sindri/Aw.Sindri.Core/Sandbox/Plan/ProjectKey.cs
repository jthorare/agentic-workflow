namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The directory name Claude Code gives a working directory under 'projects/': every character that is not a letter or a digit becomes a dash.</summary>
internal static class ProjectKey
{
    /// <summary>Makes the key.</summary>
    /// <param name="path">The working directory.</param>
    /// <returns>The key, for example '-home-owner-repos-web'.</returns>
    internal static string For(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return string.Create(path.Length, path, static (span, text) =>
        {
            for (var i = 0; i < text.Length; i++)
            {
                span[i] = char.IsAsciiLetterOrDigit(text[i]) ? text[i] : '-';
            }
        });
    }
}
