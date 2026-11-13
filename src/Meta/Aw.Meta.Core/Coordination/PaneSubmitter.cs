using Aw.Kernel;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Submits a line to a Claude pane and checks it went in: a long line typed with its Enter in one call is pasted and the Enter swallowed, so the
/// text sits in the input box. The line is typed, Enter follows after a pause, and the pane is read back; while the line is still in the
/// input box Enter is pressed again, up to three more times.
/// </summary>
/// <param name="pane">The tmux session.</param>
/// <param name="delay">Waits between the steps.</param>
public sealed class PaneSubmitter(ILanePane pane, IDelay delay)
{
    /// <summary>How long to wait after typing and after Enter.</summary>
    public static readonly TimeSpan Pause = TimeSpan.FromSeconds(1.5);

    /// <summary>How many more times Enter is pressed when the line is still in the input box.</summary>
    public const int Retries = 3;

    private const int ProbeLength = 30;

    /// <summary>Types the line, presses Enter and verifies.</summary>
    /// <param name="session">The tmux session name.</param>
    /// <param name="line">The line.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="exact">Check the input box shows exactly the line before the first Enter (#3443).</param>
    /// <returns>Success, or AW-META-012 when tmux fails or the line never left the input box.</returns>
    public async ValueTask<Result<Unit>> Submit(string session, string line, CancellationToken cancellationToken, bool exact = false)
    {
        var typed = await pane.Type(session, line, cancellationToken).ConfigureAwait(false);
        if (typed.IsError)
        {
            return typed;
        }

        if (exact)
        {
            var checkedBox = await CheckTyped(session, line, cancellationToken).ConfigureAwait(false);
            if (checkedBox.IsError)
            {
                return checkedBox;
            }
        }

        for (var attempt = 0; attempt <= Retries; attempt++)
        {
            await delay.Wait(Pause, cancellationToken).ConfigureAwait(false);
            var entered = await pane.PressEnter(session, cancellationToken).ConfigureAwait(false);
            if (entered.IsError)
            {
                return entered;
            }

            await delay.Wait(Pause, cancellationToken).ConfigureAwait(false);
            var shown = await pane.Capture(session, cancellationToken).ConfigureAwait(false);
            if (shown.IsError)
            {
                return shown.Error;
            }

            if (!InputHolds(shown.Value, line))
            {
                return Results.Ok();
            }
        }

        return MetaErrors.CommandFailed.With($"the line is still in the input box of {session} after {Retries + 1} Enter presses");
    }

    // The lane's prompt can eat the first keys ("/cl" of "/clear" read as vim keys, #3443): the rest would go in as a prompt. Before any Enter,
    // the input box must hold exactly the line; a pane with no prompt mark can't be read and goes on as before.
    private async ValueTask<Result<Unit>> CheckTyped(string session, string line, CancellationToken cancellationToken)
    {
        await delay.Wait(Pause, cancellationToken).ConfigureAwait(false);
        var shown = await pane.Capture(session, cancellationToken).ConfigureAwait(false);
        if (shown.IsError)
        {
            return shown.Error;
        }

        return InputBox(shown.Value) is not { } box || string.Equals(Squeeze(box), Squeeze(line), StringComparison.Ordinal)
            ? Results.Ok()
            : MetaErrors.CommandFailed.With($"the input box of {session} shows '{box.Trim()}' instead of '{line}' after typing; no Enter was pressed, so nothing was submitted. Clear that input line in the pane, then run it again");
    }

    /// <summary>Whether the input box (from the last line starting with the prompt mark to the next rule) still holds the start of the line. A pane with no prompt reads as submitted.</summary>
    /// <param name="capture">The captured pane text.</param>
    /// <param name="line">The line that was typed.</param>
    /// <returns>True when the text is still being typed.</returns>
    public static bool InputHolds(string capture, string line)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(line);
        if (InputBox(capture) is not { } box)
        {
            return false;
        }

        var probe = Squeeze(line);
        return Squeeze(box).Contains(probe[..Math.Min(ProbeLength, probe.Length)], StringComparison.Ordinal);
    }

    // The input box's text: from the last line starting with the prompt mark to the next rule, without the mark. Null when no line starts with it.
    private static string? InputBox(string capture)
    {
        var lines = capture.Split('\n');
        var start = Array.FindLastIndex(lines, static l => l.StartsWith('❯'));
        if (start < 0)
        {
            return null;
        }

        var box = new List<string>();
        for (var i = start; i < lines.Length && (i == start || !lines[i].StartsWith('─')); i++)
        {
            box.Add(lines[i]);
        }

        return string.Concat(box).TrimStart('❯');
    }

    private static string Squeeze(string text) => new([.. text.Where(static c => !char.IsWhiteSpace(c))]);
}
