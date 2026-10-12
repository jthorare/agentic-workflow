namespace Aw.Sindri.Host.Commands;

/// <summary>The usage text.</summary>
internal static class Usage
{
    internal static readonly string Text = string.Join('\n',
        "usage: sindri [--headless]           start the control plane if it isn't running, then print its summary",
        "       sindri serve                  run the control plane in the foreground (what 'sindri' starts; launchd target)",
        "       sindri status [--json]        the running control plane's status (exit 1 when it isn't running)",
        "       sindri stop                   stop it and release the host lease",
        "       sindri profile validate [--json]",
        "       sindri profile approve [<hash>]   without a hash: show the hash and what changed; with it: approve (needs a terminal)",
        "       sindri doctor [--json]        lease, ledger, profile and api checks; runs without the control plane",
        "       sindri recipe validate <file> [--json]   check a recipe in-process: rule lines, then its hash (exit 0 pass, 1 fail, 2 error)",
        "       sindri recipe eval <file>     run a recipe's eval cases (arrives with P3; exits 2 until then)",
        "exit codes: 0 ok, 1 attention needed, 2 error");
}
