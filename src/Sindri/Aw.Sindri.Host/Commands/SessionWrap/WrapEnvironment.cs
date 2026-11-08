using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>The variables 'session wrap' sets before it starts the agent (launcher spec §3, L-12): the session is a Sindri child of this launch.</summary>
internal static class WrapEnvironment
{
    private static readonly string[] _passedOn = ["AW_SINDRI_ITEM", "AW_SINDRI_LAUNCH_SECRET", "AW_SINDRI_HOME"];

    /// <summary>The variables to set on top of the inherited environment.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="environment">Wrap's own environment.</param>
    /// <param name="wrapExecutable">The path of the executable wrap runs as.</param>
    /// <returns>AW_SINDRI_CHILD and AW_SINDRI_LAUNCH, whichever of the item, the launch secret and the home wrap itself was given, and, when wrap runs as the C# 'sindri', a PATH with its directory first so the agent's bare 'sindri' is this one and not the TS CLI.</returns>
    internal static IReadOnlyDictionary<string, string> For(LaunchId launch, Func<string, string?> environment, string wrapExecutable)
    {
        var set = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_SINDRI_CHILD"] = "1", ["AW_SINDRI_LAUNCH"] = launch.Value };
        foreach (var name in _passedOn)
        {
            if (environment(name) is { Length: > 0 } value)
            {
                set[name] = value;
            }
        }

        if (SindriDirectory(wrapExecutable) is { } own)
        {
            set["PATH"] = PathFirst(own, environment("PATH"));
        }

        return set;
    }

    /// <summary>The directory of <paramref name="wrapExecutable"/> when wrap runs as the C# 'sindri' (not as the dotnet host), else null.</summary>
    private static string? SindriDirectory(string wrapExecutable) =>
        string.Equals(Path.GetFileName(wrapExecutable), "sindri", StringComparison.Ordinal) ? Path.GetDirectoryName(wrapExecutable) : null;

    private static string PathFirst(string directory, string? inherited)
    {
        if (string.IsNullOrEmpty(inherited))
        {
            return directory;
        }

        return string.Equals(inherited.Split(Path.PathSeparator)[0], directory, StringComparison.Ordinal) ? inherited : directory + Path.PathSeparator + inherited;
    }
}
