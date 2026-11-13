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
        _ = exact;   // red stub (#3443): the check comes with the fix
        var typed = await pane.Type(session, line, cancellationToken).ConfigureAwait(false);
        if (typed.IsError)
        {
            return typed;
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

    /// <summary>Whether the input box (from the last line starting with the prompt mark to the next rule) still holds the start of the line. A pane with no prompt reads as submitted.</summary>
    /// <param name="capture">The captured pane text.</param>
    /// <param name="line">The line that was typed.</param>
    /// <returns>True when the text is still being typed.</returns>
    public static bool InputHolds(string capture, string line)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentNullException.ThrowIfNull(line);
        var lines = capture.Split('\n');
        var start = Array.FindLastIndex(lines, static l => l.StartsWith('❯'));
        if (start < 0)
        {
            return false;
        }

        var box = new List<string>();
        for (var i = start; i < lines.Length && (i == start || !lines[i].StartsWith('─')); i++)
        {
            box.Add(lines[i]);
        }

        var squeezed = Squeeze(string.Concat(box).TrimStart('❯'));
        var probe = Squeeze(line);
        return squeezed.Contains(probe[..Math.Min(ProbeLength, probe.Length)], StringComparison.Ordinal);
    }

    private static string Squeeze(string text) => new([.. text.Where(static c => !char.IsWhiteSpace(c))]);
}
