namespace Aw.Sindri.Ui.Api.Home;

/// <summary>One object behind a row's blocks count.</summary>
/// <param name="Id">The blocked object's id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Via">The edge that reaches it, such as 'closes'.</param>
internal sealed record HomeBlockedView(string Id, string Title, string Via);
