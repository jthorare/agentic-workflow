namespace Aw.Sindri.Ui.Api.Linear;

/// <summary>A Linear team.</summary>
/// <param name="Id">The team's Linear id; null before the team's first sync.</param>
/// <param name="Key">The team key.</param>
/// <param name="Name">The team's name; null before the team's first sync.</param>
internal sealed record LinearTeam(string? Id, string Key, string? Name)
{
    /// <summary>The name to show: the team's name, else its key.</summary>
    public string DisplayName => Name ?? Key;
}
