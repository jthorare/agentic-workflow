namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>Claude Code's 'sandbox' settings, as far as a Step uses them.</summary>
/// <param name="Enabled">Whether the CLI's own sandbox runs inside Sindri's.</param>
internal sealed record ClaudeSandbox(bool Enabled);
