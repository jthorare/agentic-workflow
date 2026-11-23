namespace Aw.Sindri.Ui.Api.Home;

/// <summary>A row's rank (home spec §4).</summary>
/// <param name="Blocks">How many objects wait behind this one.</param>
/// <param name="Weight">The reason's weight, 0 to 9.</param>
/// <param name="SinceRank">The age rank, oldest first.</param>
/// <param name="Position">The row's place in the current order.</param>
internal sealed record HomeRankView(int Blocks, int Weight, int SinceRank, int Position);
