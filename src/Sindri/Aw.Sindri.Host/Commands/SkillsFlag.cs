using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// '--skills &lt;dir&gt;' on 'recipe validate' and 'recipe eval' (skill flows spec §3.2): the in-process verbs read their skill blocks from that folder instead of the usual one. The flag is taken out of the arguments before the verb reads them, and the folder
/// is handed to the composition as AW_SINDRI_SKILLS_DIR, the variable that already names the folder, so the verbs need no second way to learn it. 'skill run --skills' is a different thing (it is sent to the control plane) and is left to its own parser.
/// </summary>
internal static class SkillsFlag
{
    internal const string Variable = "AW_SINDRI_SKILLS_DIR";

    /// <summary>Takes '--skills &lt;dir&gt;' out of a 'recipe validate|eval' command line and points the environment at the folder.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="environment">Reads an environment variable.</param>
    /// <param name="currentDirectory">Relative folders resolve against it.</param>
    /// <returns>The arguments without the flag and the environment that names the folder; an error when the flag has no value.</returns>
    internal static Result<(string[] Args, Func<string, string?> Environment)> Apply(string[] args, Func<string, string?> environment, string currentDirectory)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(environment);
        var at = args is ["recipe", "validate" or "eval", ..] ? Array.IndexOf(args, "--skills") : -1;
        if (at < 0)
        {
            return (args, environment);
        }

        if (at + 1 >= args.Length || args[at + 1].StartsWith("--", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With("--skills needs a folder (usage: sindri recipe validate <file> [--skills <dir>] | sindri recipe eval <file> [--skills <dir>])");
        }

        var folder = Path.GetFullPath(args[at + 1], currentDirectory);
        string[] rest = [.. args[..at], .. args[(at + 2)..]];
        return (rest, name => string.Equals(name, Variable, StringComparison.Ordinal) ? folder : environment(name));
    }
}
