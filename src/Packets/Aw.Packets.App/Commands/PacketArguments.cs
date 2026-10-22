using Aw.Packets.Core.Model;

namespace Aw.Packets.App.Commands;

/// <summary>The arguments 'validate' and 'freeze' share: &lt;packet-dir&gt; [--repo &lt;dir&gt;] [--json], and the flags only one of them takes.</summary>
/// <param name="PacketDirectory">The packet directory.</param>
/// <param name="RepoDirectory">The repository directory, when given.</param>
/// <param name="Json">True for '--json'.</param>
/// <param name="By">Who freezes it ('--by', 'freeze' only): the machine unless the owner asked.</param>
/// <param name="SizesFile">The sizes file ('--sizes', 'validate' only), when given.</param>
/// <param name="SealFile">The seal file ('--seal', 'validate --kind phase' only), when given.</param>
internal sealed record PacketArguments(string PacketDirectory, string? RepoDirectory, bool Json, FreezeBy By = FreezeBy.Machine, string? SizesFile = null, string? SealFile = null)
{
    internal const string Shape = "<packet-dir> [--repo <dir>] [--json]";
    internal const string ValidateShape = "<packet-dir> [--repo <dir>] [--sizes <file>] [--json]";
    internal const string SealShape = "<packet-dir> [--repo <dir>] [--seal <file>] [--json]";
    internal const string FreezeShape = "<packet-dir> [--repo <dir>] [--by owner|machine] [--json]";

    /// <summary>Binds the arguments.</summary>
    /// <param name="args">The arguments after the command name and '--kind'.</param>
    /// <param name="takesBy">True for 'freeze', which takes '--by owner|machine'.</param>
    /// <param name="takesSizes">True for 'validate', which takes '--sizes &lt;file&gt;'.</param>
    /// <param name="takesSeal">True for a phase packet's 'validate', which takes '--seal &lt;file&gt;'.</param>
    /// <returns>The arguments; null for an unknown, repeated or valueless flag, or no (or a second) packet directory.</returns>
    internal static PacketArguments? Parse(IReadOnlyList<string> args, bool takesBy = false, bool takesSizes = false, bool takesSeal = false)
    {
        string[] flags = takesBy ? ["--repo", "--by"] : takesSizes ? ["--repo", "--sizes"] : takesSeal ? ["--repo", "--seal"] : ["--repo"];
        if (SimpleArguments.Parse(args, flags) is not { } parsed)
        {
            return null;
        }

        var by = parsed.Values.TryGetValue("--by", out var given) ? FreezeByArgument.Parse(given) : FreezeBy.Machine;
        return by is { } who
            ? new PacketArguments(parsed.PacketDirectory, parsed.Values.GetValueOrDefault("--repo"), parsed.Json, who, parsed.Values.GetValueOrDefault("--sizes"), parsed.Values.GetValueOrDefault("--seal"))
            : null;
    }
}
