using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListHomeDone;

/// <summary>The Done list: the rows marked done in the last <paramref name="KeepDays"/> days, the newest first (home spec §5.4).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="KeepDays">How many days back the list reaches.</param>
/// <param name="Rows">The done rows, the most recently done first.</param>
public sealed record ListHomeDoneResponse(AsOf AsOf, int KeepDays, IReadOnlyList<HomeRow> Rows);
