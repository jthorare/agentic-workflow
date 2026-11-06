namespace Aw.Context.Infrastructure.Records;

/// <summary>The Linear pickers' lists' SQL (migration 0032_s1l_linear_hub). Values are always bound, never formatted in.</summary>
internal static class LinearLookupSql
{
    internal const string TeamByKey = "SELECT id, key FROM linear_team WHERE key = @key";

    internal const string States = "SELECT id, name, type, position FROM linear_state WHERE team_id = @team ORDER BY position, name, id";

    internal const string Cycles = "SELECT id, number, name, starts_at, ends_at FROM linear_cycle WHERE team_id = @team ORDER BY starts_at, number, id";

    internal const string Members = "SELECT id, name, display_name FROM linear_member WHERE team_id = @team ORDER BY name COLLATE NOCASE, name, id";

    internal const string Labels = "SELECT id, name FROM linear_label WHERE team_id = @team ORDER BY name COLLATE NOCASE, name, id";

    internal const string DeleteTeam = "DELETE FROM linear_team WHERE id = @team OR key = @key";

    internal const string DeleteStates = "DELETE FROM linear_state WHERE team_id = @team";

    internal const string DeleteCycles = "DELETE FROM linear_cycle WHERE team_id = @team";

    internal const string DeleteMembers = "DELETE FROM linear_member WHERE team_id = @team";

    internal const string DeleteLabels = "DELETE FROM linear_label WHERE team_id = @team";

    internal const string InsertTeam = "INSERT INTO linear_team (id, key) VALUES (@team, @key)";

    internal const string InsertState = "INSERT INTO linear_state (team_id, id, name, type, position) VALUES (@team, @id, @name, @type, @position) ON CONFLICT (team_id, id) DO UPDATE SET name = excluded.name, type = excluded.type, position = excluded.position";

    internal const string InsertCycle = "INSERT INTO linear_cycle (team_id, id, number, name, starts_at, ends_at) VALUES (@team, @id, @number, @name, @starts, @ends) ON CONFLICT (team_id, id) DO UPDATE SET number = excluded.number, name = excluded.name, starts_at = excluded.starts_at, ends_at = excluded.ends_at";

    internal const string InsertMember = "INSERT INTO linear_member (team_id, id, name, display_name) VALUES (@team, @id, @name, @display) ON CONFLICT (team_id, id) DO UPDATE SET name = excluded.name, display_name = excluded.display_name";

    internal const string InsertLabel = "INSERT INTO linear_label (team_id, id, name) VALUES (@team, @id, @name) ON CONFLICT (team_id, id) DO UPDATE SET name = excluded.name";
}
