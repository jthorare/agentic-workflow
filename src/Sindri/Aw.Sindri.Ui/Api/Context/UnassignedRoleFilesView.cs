namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Role-like files no glob covers.</summary>
/// <param name="Count">Count, as served.</param>
/// <param name="Examples">Examples, as served: at most five paths, in path order.</param>
internal sealed record UnassignedRoleFilesView(int Count, IReadOnlyList<string> Examples);
