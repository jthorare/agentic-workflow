using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Connections;

/// <summary>Stub (red commit): the calls a connection row makes.</summary>
internal sealed class ConnectionActions(Aw.Sindri.Ui.Api.ISindriApi api, Func<Uri?> url, ILauncher launcher, TimeProvider clock)
{
    /// <summary>How often the sign-in poll reads the connection.</summary>
    internal static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(2);

    internal Aw.Sindri.Ui.Api.ISindriApi Api { get; } = api;

    internal Func<Uri?> Url { get; } = url;

    internal ILauncher Launcher { get; } = launcher;

    internal TimeProvider Clock { get; } = clock;
}
