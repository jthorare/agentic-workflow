namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>How a browser sign-in ended, as the app saw it.</summary>
internal enum SignInState
{
    /// <summary>The connection reads 'connected'.</summary>
    Connected,

    /// <summary>The wait ran out before it did (Sindri's own listener gives up after 5 minutes).</summary>
    TimedOut,

    /// <summary>The sign-in could not start, or the connection read 'error', or the read failed.</summary>
    Failed,
}
