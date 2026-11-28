namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The detection <c lang="text">sindri launch init</c> runs, and the launcher runs in memory when there is no launcher.json (launcher spec §4).</summary>
internal static class TerminalDetection
{
    /// <summary>No GUI session: print; else Warp when present; else on macOS the registered default terminal; else the terminal <c lang="text">$TERM_PROGRAM</c> names in terminals.json; else <c lang="text">$TERMINAL</c>, then <c lang="text">x-terminal-emulator</c> (owner, 2026-10-17); else the first of terminals.json on the login PATH; else print.</summary>
    /// <param name="inputs">The environment and probes.</param>
    /// <param name="cancellationToken">Cancels the PATH probes.</param>
    /// <returns>The choice and why.</returns>
    internal static async ValueTask<DetectedTerminal> Detect(DetectionInputs inputs, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (!ViewerProcess.HasGuiSession(inputs.GetEnv, inputs.IsMac))
        {
            return new DetectedTerminal("print", "print", Template: null, "no GUI session");
        }

        if (await inputs.WarpPresent(cancellationToken).ConfigureAwait(false))
        {
            return new DetectedTerminal("warp", "Warp", Template: null, "Warp is installed");
        }

        if (inputs.IsMac)
        {
            return new DetectedTerminal("default-terminal", "Default terminal", Template: null, "macOS opens .command files in the registered default terminal");
        }

        var named = inputs.GetEnv("TERM_PROGRAM");
        if (named is { Length: > 0 } && inputs.Templates.FirstOrDefault(t => t.TermProgram.Contains(named, StringComparer.OrdinalIgnoreCase)) is { } current)
        {
            return Exec(current, "from $TERM_PROGRAM");
        }

        if (await FromTerminalEnv(inputs, cancellationToken).ConfigureAwait(false) is { } configured)
        {
            return configured;
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

    /// <summary><c lang="text">$TERMINAL</c> then <c lang="text">x-terminal-emulator</c>: terminals.json's template when the program is one it knows, else the xterm-style argv most terminals accept.</summary>
    private static async ValueTask<DetectedTerminal?> FromTerminalEnv(DetectionInputs inputs, CancellationToken cancellationToken)
    {
        if (inputs.GetEnv("TERMINAL") is { Length: > 0 } named && await inputs.OnPath(named, cancellationToken).ConfigureAwait(false))
        {
            var program = Path.GetFileName(named);
            return inputs.Templates.FirstOrDefault(t => string.Equals(t.Bin, program, StringComparison.Ordinal)) is { } known
                ? Exec(known, "from $TERMINAL")
                : Exec(Generic(named, program), "from $TERMINAL");
        }

        const string Alternatives = "x-terminal-emulator";
        return await inputs.OnPath(Alternatives, cancellationToken).ConfigureAwait(false) ? Exec(Generic(Alternatives, Alternatives), "x-terminal-emulator is on the login PATH") : null;
    }

    private static TerminalTemplate Generic(string bin, string name) => new(name, name, bin, [], [bin, "-T", "{title}", "-e", "{cmd...}"]);

    private static DetectedTerminal Exec(TerminalTemplate template, string reason) => new("exec-template", template.Name, template, reason);
}
