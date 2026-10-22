using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The run page in the shell (<see cref="ShellViewModel"/>, continued; F3 run canvas, slice 3): a line run opens it over the Workflows page instead of the detail sheet.</summary>
internal sealed partial class ShellViewModel
{
    private const string LineRecipe = "workflow.line";

    private bool _runOpen;

    /// <summary>The run page (named for the page: the shell's Run method is the follow loop).</summary>
    public RunPageViewModel RunPage { get; private set; }

    /// <summary>True while the run page shows: a run was opened from Workflows and the page is still the Workflows page.</summary>
    public bool IsRunPageShown => _runOpen && IsWorkflows && IsPageVisible;

    // Everything the page needs once it exists (ShellViewModel's constructor calls this one line).
    [MemberNotNull(nameof(RunPage))]
    private void InitRunPage(ISindriApi api, IClipboard clipboard)
    {
        RunPage = new RunPageViewModel(api, Health.Catalog, clipboard, _clock);
        Health.Follow((url, token) => _runOpen ? RunPage.Refresh(url, token) : Task.CompletedTask);
        Health.FollowInstances((url, change, token) => _runOpen ? RunPage.Catch(url, change, token) : Task.FromResult(true));
        RunPage.LinkRequested += (_, e) => Navigate(e.Link);
        RunPage.BackRequested += (_, _) => CloseRunPage();
    }

    /// <summary>Closes the run page; the Workflows page shows again.</summary>
    internal void CloseRunPage()
    {
        if (!_runOpen)
        {
            return;
        }

        _runOpen = false;
        Changed(nameof(IsRunPageShown), nameof(IsPagesShown));
    }

    // True when the Workflows page lists the run at this address and it is a line run.
    private bool IsLineRun(string? address) =>
        address is not null && string.Equals(Workflows.Groups.SelectMany(g => g.Rows).FirstOrDefault(r => string.Equals(r.Address, address, StringComparison.Ordinal))?.Recipe, LineRecipe, StringComparison.Ordinal);

    // A line run opens its canvas over the Workflows page, not the sheet.
    private void OpenRunPage(string address)
    {
        if (IsSheetOpen)
        {
            CloseSheet();
        }

        GoTo(PageId.Workflows);
        _runOpen = true;
        Changed(nameof(IsRunPageShown), nameof(IsPagesShown));
        if (Health.ApiUrl() is { } url)
        {
            _ = RunPage.Open(url, address, KnownId(address), CancellationToken.None);
        }
    }

    // J, K and F on the run page belong to it and never to the Workflows list behind it.
    private bool RunPageKeyPressed(string key, bool inSheet) => !inSheet && IsRunPageShown && RunPage.Press(key);
}
