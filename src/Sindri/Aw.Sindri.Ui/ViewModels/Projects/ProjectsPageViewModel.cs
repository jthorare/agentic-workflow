#pragma warning disable CA1822, MA0041, MA0182, S3237, IDE0060, CA1801, S1172 // red stubs: replaced by the implementation commit
using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>The Projects page (projects page spec §3): the owner's projects in state groups, two folds, and the served sentence for an empty or unavailable list. It reads while it is the page shown.</summary>
internal sealed class ProjectsPageViewModel : ObservableObject, IDisposable
{
    internal ProjectsPageViewModel(ISindriApi api, TimeProvider clock)
    {
        throw new NotSupportedException();
    }

    /// <summary>The page's name.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>True until the first answer.</summary>
    public bool IsLoading => throw new NotSupportedException();

    /// <summary>The sources' freshness and the Linear viewer, joined with " · ".</summary>
    public string Freshness => throw new NotSupportedException();

    /// <summary>True when a source is failing: the line is tinted.</summary>
    public bool FreshnessFailing => throw new NotSupportedException();

    /// <summary>The served empty sentence, as served; null when none.</summary>
    public string? Sentence => throw new NotSupportedException();

    /// <summary>True when there are no cards and a sentence: the dashed block.</summary>
    public bool IsEmptyBlock => throw new NotSupportedException();

    /// <summary>True when there are cards and a sentence: the banner.</summary>
    public bool IsBanner => throw new NotSupportedException();

    /// <summary>True when the dashed block offers Add project (not while Linear is syncing).</summary>
    public bool ShowsAddInEmpty => throw new NotSupportedException();

    /// <summary>True when Context is failing: cards show their name and origin chip only.</summary>
    public bool NamesOnly => throw new NotSupportedException();

    /// <summary>The state groups that have cards, in order.</summary>
    public ObservableCollection<ProjectGroupViewModel> Groups => throw new NotSupportedException();

    /// <summary>Done and canceled projects.</summary>
    public ProjectFoldViewModel Finished => throw new NotSupportedException();

    /// <summary>Projects the owner hid.</summary>
    public ProjectFoldViewModel Hidden => throw new NotSupportedException();

    /// <summary>How many cards sit across: 1, 2 or 3.</summary>
    public int Columns => throw new NotSupportedException();

    /// <summary>The last read's failure line; empty when the last read worked.</summary>
    public string Error => throw new NotSupportedException();

    /// <summary>The catch-up read started by the last show.</summary>
    internal Task CatchUp => throw new NotSupportedException();

    /// <summary>True while the page is the one shown.</summary>
    internal bool IsActive => throw new NotSupportedException();

    /// <summary>The page's width changed.</summary>
    /// <param name="width">The width in px.</param>
    internal void SetPageWidth(double width) => throw new NotSupportedException();

    /// <summary>Re-reads the list (on connect and on every event); while the page isn't shown it only remembers where the API is.</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the list is shown.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>The page was shown or hidden; becoming the page shown reads at once.</summary>
    /// <param name="shown">True while the Projects page is the page.</param>
    internal void SetShown(bool shown) => throw new NotSupportedException();

    /// <summary>Cancels the read started when the page was shown.</summary>
    public void Dispose() => throw new NotSupportedException();
}
