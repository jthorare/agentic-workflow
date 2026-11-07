using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>Settings' Connections → Linear card in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Linear card behind Settings' Connections group.</summary>
    public LinearConnectionViewModel LinearConnection { get; }

    private LinearConnectionViewModel InitLinearConnection(ISindriApi api) => new(Health.ApiUrl, api);
}
