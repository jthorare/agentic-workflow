namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The detection <c lang="text">sindri launch init</c> runs, and the launcher runs in memory when there is no launcher.json (launcher spec §4).</summary>
internal static class TerminalDetection
{
    /// <summary>No GUI session: print; else Warp when present; else the terminal <c lang="text">$TERM_PROGRAM</c> names in terminals.json; else the first of terminals.json on the login PATH; else print.</summary>
    /// <param name="inputs">The environment and probes.</param>
    /// <param name="cancellationToken">Cancels the PATH probes.</param>
    /// <returns>The choice and why.</returns>
    internal static async ValueTask<DetectedTerminal> Detect(DetectionInputs inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!ViewerProcess.HasGuiSession(inputs.GetEnv))
        {
            return new DetectedTerminal("print", "print", Template: null, "no GUI session");
        }

        if (await inputs.WarpPresent(cancellationToken).ConfigureAwait(false))
        {
            return new DetectedTerminal("warp", "Warp", Template: null, "Warp is installed");
        }

        var named = inputs.GetEnv("TERM_PROGRAM");
        if (named is { Length: > 0 } && inputs.Templates.FirstOrDefault(t => t.TermProgram.Contains(named, StringComparer.OrdinalIgnoreCase)) is { } current)
        {
            return Exec(current, "from $TERM_PROGRAM");
        }

        foreach (var template in inputs.Templates)
        {
            if (await inputs.OnPath(template.Bin, cancellationToken).ConfigureAwait(false))
            {
                return Exec(template, "first one on the login PATH");
            }
        }

        return new DetectedTerminal("print", "print", Template: null, "no terminal found");
    }

    private static DetectedTerminal Exec(TerminalTemplate template, string reason) => new("exec-template", template.Name, template, reason);
}
