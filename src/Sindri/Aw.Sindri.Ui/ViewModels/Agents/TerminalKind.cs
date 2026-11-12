namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>What the pane shows, which decides where focus starts and what Stop and Close offer (agents spec §15.3).</summary>
internal enum TerminalKind
{
    /// <summary>A lane's session.</summary>
    Lane,

    /// <summary>Any other listed session on a tmux host.</summary>
    Listed,

    /// <summary>A one-off session started from the New agent form.</summary>
    OneOff,
}
