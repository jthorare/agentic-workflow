using Aw.Context.Core.Engine;
using Aw.Context.Infrastructure.Map;

namespace Aw.Context.Infrastructure.Sources;

/// <summary>Builds the library's <see cref="LibraryHost"/> from the environment at the host edge: the home folder, $AW_SINDRI_HOME and the 'aw' root's default ($AW_STATE_DIR, else ~/.agentic-workflow).</summary>
internal static class LibraryHostFromEnvironment
{
    internal static LibraryHost Read(Func<string, string?> environment)
    {
        var home = environment("HOME") is { Length: > 0 } set ? set : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var state = environment("AW_STATE_DIR") is { Length: > 0 } dir ? dir : Path.Combine(home, ".agentic-workflow");
        var sindri = environment("AW_SINDRI_HOME") is { Length: > 0 } sindriHome ? sindriHome : Path.Combine(state, "sindri-app");
        return new LibraryHost { Home = home, SindriHome = sindri, StateDefault = state, RealPath = static path => RealPath.Of(path) ?? path };
    }
}
