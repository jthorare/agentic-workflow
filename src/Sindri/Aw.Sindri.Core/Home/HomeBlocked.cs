using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Home;

/// <summary>One object behind a row's blocks count (home spec §3.1).</summary>
/// <param name="Id">The blocked object's id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Via">The edge that reaches it.</param>
public sealed record HomeBlocked(HomeRowId Id, string Title, string Via);
