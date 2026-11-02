namespace Aw.Sindri.Infrastructure.Sandbox.Launch;

/// <summary>What the Step's policy step gave the launch: the extra arguments its form takes under a sandbox, and the policy file's move into the session home.</summary>
/// <param name="Args">The arguments to add right after the program (a sandboxed Codex Step's '-c' overrides, which the CLI takes before its prompt and subcommand); empty for most forms.</param>
/// <param name="From">The policy file's path in the argv the launcher built, or null when no file moved.</param>
/// <param name="To">Where the file is now, inside the session home: every <paramref name="From"/> in the argv becomes this.</param>
internal sealed record PolicyFiles(IReadOnlyList<string> Args, string? From = null, string? To = null)
{
    /// <summary>No extra arguments and no file.</summary>
    internal static PolicyFiles None { get; } = new([]);

    /// <summary>Gets the argv with the policy file's path pointing into the session home and the extra arguments right after the program, ahead of any prompt (the entry's template puts the prompt before its Step forms, so the end of the argv is not safe).</summary>
    /// <param name="argv">The argv the launcher built.</param>
    /// <returns>The argv to wrap.</returns>
    internal IReadOnlyList<string> Apply(IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        var moved = argv.Select(a => From is { } from && To is { } to ? a.Replace(from, to, StringComparison.Ordinal) : a).ToList();
        moved.InsertRange(Math.Min(1, moved.Count), Args);
        return moved;
    }
}
