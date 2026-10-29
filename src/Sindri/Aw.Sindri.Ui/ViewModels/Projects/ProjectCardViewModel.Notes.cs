#pragma warning disable CA1822, MA0041 // stubs: the red commit, replaced by the implementation
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Projects;

internal sealed partial class ProjectCardViewModel
{
    public string NotesText => throw new NotSupportedException();

    public bool HasNotes => throw new NotSupportedException();

    public bool IsEditing => throw new NotSupportedException();

    public string Draft { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    public string Counter => throw new NotSupportedException();

    public bool OverLimit => throw new NotSupportedException();

    public bool CanSave => throw new NotSupportedException();

    public bool IsSaving => throw new NotSupportedException();

    public string SaveText => throw new NotSupportedException();

    public string? SaveError => throw new NotSupportedException();

    public bool IsConflict => throw new NotSupportedException();

    public LiveAnnouncer Voice => throw new NotSupportedException();

    internal void BeginEdit() => throw new NotSupportedException();

    internal void CancelEdit() => throw new NotSupportedException();

    internal Task Save(CancellationToken cancellationToken) => throw new NotSupportedException();

    internal Task Reload(CancellationToken cancellationToken) => throw new NotSupportedException();
}
