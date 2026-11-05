using Aw.Sindri.Core.ModelCheck;

namespace Aw.Sindri.Infrastructure.ModelCheck;

/// <summary>
/// Reads where the TLC runner is and the environment it gets (model-check design §3). The runner is the copy <c language="text">install-cs.sh</c> keeps beside the published sindri,
/// <c language="text">$AW_STATE_DIR/bin/cs/current/tlc/scripts/tlc-check.sh</c>, or the file <c language="text">AW_TLC_CHECK</c> names (tests). It is never the workspace's or the owner's checkout's.
/// </summary>
internal static class ModelCheckSetups
{
    private const string Heap = "2g";

    /// <summary>The setup for this process.</summary>
    /// <param name="environment">Reads an environment variable.</param>
    /// <returns>The setup; its runner is null when no runner file exists.</returns>
    internal static ModelCheckSetup Read(Func<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var state = Path.GetFullPath(SettingsFromEnvironment.StateRoot(environment));
        var installed = Path.Combine(state, "bin", "cs", "current", "tlc");
        var named = environment("AW_TLC_CHECK");
        var runner = !string.IsNullOrEmpty(named) ? named : Path.Combine(installed, "scripts", "tlc-check.sh");
        var found = File.Exists(runner) ? Path.GetFullPath(runner) : null;

        // tlc.sh sits in the same tree as the runner it belongs to: <tree>/scripts/tlc-check.sh and <tree>/skills/formal-verify/scripts/tlc.sh.
        var tree = found is null ? installed : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(found) ?? installed, ".."));
        return new ModelCheckSetup(
            found,
            environment("PATH") ?? string.Empty,
            environment("HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            state,
            Path.Combine(tree, "skills", "formal-verify", "scripts", "tlc.sh"),
            Heap,
            Path.Combine(state, "tmp", "model-check"));
    }
}
