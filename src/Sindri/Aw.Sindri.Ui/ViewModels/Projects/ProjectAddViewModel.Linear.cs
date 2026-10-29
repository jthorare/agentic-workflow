using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Projects;

namespace Aw.Sindri.Ui.ViewModels.Projects;

/// <summary>
/// The From Linear tab (<see cref="ProjectAddViewModel"/>, continued; projects page spec §3.2): the list is read once per opening of the sheet, the search
/// narrows it by name with no call, and Attach sends the row's key.
/// </summary>
internal sealed partial class ProjectAddViewModel
{
    private IReadOnlyList<ProjectLinearRowViewModel> _linearAll = [];
    private bool _linearLoaded;
    private bool _linearReading;
    private bool _attaching;
    private int _linearGeneration;

    /// <summary>What the From Linear search box holds; the rows are filtered by name as it changes.</summary>
    public string LinearSearch
    {
        get;
        set
        {
            Set(ref field, value);
            ApplyLinearSearch();
        }
    } = string.Empty;

    /// <summary>The rows the search leaves.</summary>
    public IReadOnlyList<ProjectLinearRowViewModel> LinearRows { get; private set => Set(ref field, value); } = [];

    /// <summary>"Loading…", the empty or no-match sentence, or empty while rows show or the list failed.</summary>
    public string LinearStatus { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Why the list could not be read, or why the last Attach was refused; empty otherwise.</summary>
    public string LinearError { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The latest read of the From Linear list, so a test can wait for it.</summary>
    internal Task LinearLoad { get; private set; } = Task.CompletedTask;

    /// <summary>The latest Attach a row's button started, so a test can wait for it.</summary>
    internal Task LinearAttach { get; private set; } = Task.CompletedTask;

    /// <summary>Attaches the Linear project a row names.</summary>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that ends when the answer is drawn.</returns>
    internal async Task Attach(ProjectLinearRowViewModel row, CancellationToken cancellationToken)
    {
        if (_attaching || _apiUrl() is not { } url)
        {
            return;
        }

        (_attaching, LinearError, row.IsAttaching) = (true, string.Empty, true);
        SetCanAttach(enabled: false);
        Result<ProjectAcceptedView> answer;
        try
        {
            answer = await _api.AttachProject(url, new ProjectAttach(Linear: row.Key), cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            (_attaching, row.IsAttaching) = (false, false);
            SetCanAttach(enabled: true);
        }

        if (answer.IsError)
        {
            LinearError = Labels.ProjectAddFailed(answer.Error);
            return;
        }

        Added?.Invoke(this, new ProjectDoneEventArgs(ProjectDone.Added, answer.Value.Project, Labels.ProjectAdded(row.Name), wentToFinished: false));
    }

    // Reads the list once per opening; a read that failed is tried again the next time the tab shows.
    private void StartLinearLoad()
    {
        if (!_linearLoaded && _apiUrl() is { } url)
        {
            _linearLoaded = true;
            LinearLoad = ReadLinear(url, ++_linearGeneration);
        }
    }

    private async Task ReadLinear(Uri url, int generation)
    {
        (_linearReading, LinearError) = (true, string.Empty);
        ApplyLinearSearch();
        var answer = await _api.LinearProjects(url, CancellationToken.None).ConfigureAwait(true);
        if (generation != _linearGeneration)
        {
            return;
        }

        _linearReading = false;
        if (answer.IsError)
        {
            (_linearLoaded, LinearError) = (false, Labels.ProjectAddLinearFailed(answer.Error));
        }
        else if (answer.Value.Error is { Length: > 0 } served)
        {
            (_linearLoaded, LinearError) = (false, served);
        }
        else
        {
            _linearAll = [.. answer.Value.Projects.Select(Row)];
        }

        ApplyLinearSearch();
    }

    private ProjectLinearRowViewModel Row(ProjectLinearChoice choice)
    {
        var row = new ProjectLinearRowViewModel(choice);
        row.AttachRequested += (_, _) => LinearAttach = Attach(row, CancellationToken.None);
        return row;
    }

    private void SetCanAttach(bool enabled)
    {
        foreach (var row in _linearAll)
        {
            row.CanAttach = enabled;
        }
    }

    // The search narrows the rows already read by name; it never calls the API.
    private void ApplyLinearSearch()
    {
        var term = LinearSearch.Trim();
        LinearRows = term.Length == 0 ? _linearAll : [.. _linearAll.Where(r => r.Name.Contains(term, StringComparison.OrdinalIgnoreCase))];
        LinearStatus = (_linearReading, LinearError.Length > 0, _linearAll.Count, LinearRows.Count) switch
        {
            (true, _, _, _) => Labels.ProjectAddLinearLoading,
            (_, true, _, _) => string.Empty,
            (_, _, 0, _) => _linearLoaded ? Labels.ProjectAddLinearEmpty : string.Empty,
            (_, _, _, 0) => Labels.ProjectAddLinearNoMatch,
            _ => string.Empty,
        };
    }
}
