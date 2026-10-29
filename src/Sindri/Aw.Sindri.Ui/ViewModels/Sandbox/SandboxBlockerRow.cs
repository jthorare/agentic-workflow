using Aw.Sindri.Ui.Api.Sandbox;

namespace Aw.Sindri.Ui.ViewModels.Sandbox;

/// <summary>One blocker on a card: the sentence, and the command to copy when there is one.</summary>
/// <param name="Text">The sentence, in the owner's words.</param>
/// <param name="Command">The copyable second line; null when there is none.</param>
/// <param name="IsWait">True for a gray or wait blocker, which is never red.</param>
internal sealed record SandboxBlockerRow(string Text, string? Command, bool IsWait)
{
    /// <summary>True when there is a command to copy.</summary>
    public bool HasCommand => Command is { Length: > 0 };

    /// <summary>The copy button's accessible name.</summary>
    public string CopyName => Command is { Length: > 0 } c ? Labels.SandboxCopyCommand(c) : string.Empty;

    /// <summary>The row for a served blocker.</summary>
    /// <param name="line">The blocker.</param>
    /// <returns>The row.</returns>
    internal static SandboxBlockerRow Of(SandboxBlockerLineView line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new(line.Text, line.Command, string.Equals(line.State, "gray", StringComparison.Ordinal) || string.Equals(line.State, "wait", StringComparison.Ordinal));
    }
}
