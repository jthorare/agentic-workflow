namespace Aw.Sindri.Ui.Api.Home;

/// <summary>GET /sindri/home/done (home spec §5.4): the rows done in the last <paramref name="KeepDays"/> days, the most recently done first.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="KeepDays">How many days back the list reaches.</param>
/// <param name="Rows">The done rows.</param>
internal sealed record HomeDoneView(AsOfView AsOf, int KeepDays, IReadOnlyList<HomeRowView> Rows);
