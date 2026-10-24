using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Context;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Context page in the shell (<see cref="ShellViewModel"/>, continued; U9a owns this file after U9a0).</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Context page.</summary>
    public ContextPageViewModel Context { get; private set; }

    // Everything the page needs once it exists (ShellViewModel's constructor calls this one line); a stage adds its wiring here, not in the constructor.
    [MemberNotNull(nameof(Context))]
    private void InitContext(ISindriApi api, IClipboard clipboard)
    {
        Context = new ContextPageViewModel(new ContextPorts(api, Health.ApiUrl, clipboard, _clock, Health.Catalog, Health.Follow));
        Context.JobRequested += (_, e) => Navigate(e.Link);   // the Building overlay's "View build job"
    }
}
